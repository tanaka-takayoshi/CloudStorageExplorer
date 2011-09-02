using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace CloudStrageTool.Models
{
    ///Joe Giardino, Microsoft 2011
    /// <summary>
    /// Extension class to provide ParallelUpload on CloudBlockBlobs.
    /// </summary>
    public static class ParallelUploadExtensions
    {
        /// <summary>
        /// Performs a parallel upload operation on a block blob using the associated serviceclient configuration
        /// </summary>
        /// <param name="blobRef">The reference to the blob.</param>
        /// <param name="sourceStream">The source data to upload.</param>
        /// <param name="options">BlobRequestOptions to use for each upload, can be null.</param>
        /// <summary>
        /// Performs a parallel upload operation on a block blob using the associated serviceclient configuration
        /// </summary>
        /// <param name="blobRef">The reference to the blob.</param>
        /// <param name="sourceStream">The source data to upload.</param>
        /// <param name="blockIdSequenceNumber">The intial block ID, each subsequent block will increment of this value </param>
        /// <param name="options">BlobRequestOptions to use for each upload, can be null.</param>
        public static void ParallelUpload(this CloudBlockBlob blobRef, Stream sourceStream, long blockIdSequenceNumber, BlobRequestOptions options)
        {
            // Parameter Validation & Locals
            if (null == blobRef.ServiceClient)
            {
                throw new ArgumentException("Blob Reference must have a valid service client associated with it");
            }

            if (sourceStream.Length - sourceStream.Position == 0)
            {
                throw new ArgumentException("Cannot upload empty stream.");
            }

            if (null == options)
            {
                options = new BlobRequestOptions()
                {
                    Timeout = blobRef.ServiceClient.Timeout,
                    RetryPolicy = RetryPolicies.RetryExponential(RetryPolicies.DefaultClientRetryCount, RetryPolicies.DefaultClientBackoff)
                };
            }

            bool moreToUpload = true;
            List<IAsyncResult> asyncResults = new List<IAsyncResult>();
            List<string> blockList = new List<string>();

            using (MD5 fullBlobMD5 = MD5.Create())
            {
                do
                {
                    int currentPendingTasks = asyncResults.Count;

                    for (int i = currentPendingTasks; i < blobRef.ServiceClient.ParallelOperationThreadCount && moreToUpload; i++)
                    {
                        // Step 1: Create block streams in a serial order as stream can only be read sequentially
                        string blockId = null;

                        // Dispense Block Stream
                        int blockSize = (int)blobRef.ServiceClient.WriteBlockSizeInBytes;
                        int totalCopied = 0, numRead = 0;
                        MemoryStream blockAsStream = null;
                        blockIdSequenceNumber++;

                        int blockBufferSize = (int)Math.Min(blockSize, sourceStream.Length - sourceStream.Position);
                        byte[] buffer = new byte[blockBufferSize];
                        blockAsStream = new MemoryStream(buffer);

                        do
                        {
                            numRead = sourceStream.Read(buffer, totalCopied, blockBufferSize - totalCopied);
                            totalCopied += numRead;
                        }
                        while (numRead != 0 && totalCopied < blockBufferSize);


                        // Update Running MD5 Hashes
                        fullBlobMD5.TransformBlock(buffer, 0, totalCopied, null, 0);
                        blockId = GenerateBase64BlockID(blockIdSequenceNumber);

                        // Step 2: Fire off consumer tasks that may finish on other threads
                        blockList.Add(blockId);
                        IAsyncResult asyncresult = blobRef.BeginPutBlock(blockId, blockAsStream, null, options, null, blockAsStream);
                        asyncResults.Add(asyncresult);

                        if (sourceStream.Length == sourceStream.Position)
                        {
                            // No more upload tasks
                            moreToUpload = false;
                        }
                    }

                    // Step 3: Wait for 1 or more put blocks to finish and finish operations
                    if (asyncResults.Count > 0)
                    {
                        int waitTimeout = options.Timeout.HasValue ? (int)Math.Ceiling(options.Timeout.Value.TotalMilliseconds) : Timeout.Infinite;
                        int waitResult = WaitHandle.WaitAny(asyncResults.Select(result => result.AsyncWaitHandle).ToArray(), waitTimeout);

                        if (waitResult == WaitHandle.WaitTimeout)
                        {
                            throw new TimeoutException(String.Format("ParallelUpload Failed with timeout = {0}", options.Timeout.Value));
                        }

                        // Optimize away any other completed operations
                        for (int index = 0; index < asyncResults.Count; index++)
                        {
                            IAsyncResult result = asyncResults[index];
                            if (result.IsCompleted)
                            {
                                // Dispose of memory stream
                                (result.AsyncState as IDisposable).Dispose();
                                asyncResults.RemoveAt(index);
                                blobRef.EndPutBlock(result);
                                index--;
                            }
                        }
                    }
                }
                while (moreToUpload || asyncResults.Count != 0);

                // Step 4: Calculate MD5 and do a PutBlockList to commit the blob
                fullBlobMD5.TransformFinalBlock(new byte[0], 0, 0);
                byte[] blobHashBytes = fullBlobMD5.Hash;
                string blobHash = Convert.ToBase64String(blobHashBytes);
                blobRef.Properties.ContentMD5 = blobHash;
                blobRef.PutBlockList(blockList, options);
            }
        }

        /// <summary>
        /// Generates a unique Base64 encoded blockID
        /// </summary>
        /// <param name="seqNo">The blocks sequence number in the given upload operation.</param>
        /// <returns></returns>
        private static string GenerateBase64BlockID(long seqNo)
        {
            // 9 bytes needed since base64 encoding requires 6 bits per character (6*12 = 8*9)
            byte[] tempArray = new byte[9];

            for (int m = 0; m < 9; m++)
            {
                tempArray[8 - m] = (byte)((seqNo >> (8 * m)) & 0xFF);
            }

            Convert.ToBase64String(tempArray);

            return Convert.ToBase64String(tempArray);
        }
    }
}
