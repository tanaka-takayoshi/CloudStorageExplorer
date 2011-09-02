using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Livet;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using System.IO;
using System.Reactive.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;

namespace CloudStrageTool.Models
{
    public class S3Model : NotificationObject
    {
        static S3Model()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 50;
        }

        /*
         * NotificationObjectはプロパティ変更通知の仕組みを実装したオブジェクトです。
         */

        /*
         * ModelからViewModelへのイベントを発行する場合はNotificatorを使用してください。
         *
         * Notificatorはイベント代替手段です。コードスニペット lnev でCLRイベントと同時に定義できます。
         *
         * ViewModelへNotificatorを使用した通知を行う場合はViewModel側でViewModelHelperを使用して受信側の登録をしてください。
         */

        public BasicAWSCredentials GetCredentials()
        {
            return new BasicAWSCredentials(Properties.Settings.Default.AccessKey, Properties.Settings.Default.SecretAccessKey);
        }

        private AmazonS3Client s3Client;
        private AmazonS3Client S3Client 
        {
            get
            {
                if (s3Client == null)
                {
                    s3Client = new AmazonS3Client(GetCredentials());
                }
                return s3Client;
            }
        }

        public IEnumerable<S3BucketModel> GetS3Buckets()
        {
            var request = new ListBucketsRequest();
            return S3Client.ListBuckets(request)
                .Buckets
                .Select(bucket => new S3BucketModel() { BucketName = bucket.BucketName, CreationDate = bucket.CreationDate});
        }

        public System.Tuple<IEnumerable<IObservable<PartUploadProgressChangedArgs>>, IObservable<bool>> UploadFile(string filePath, string bucket, string keyName)
        {
            var s3Client = new AmazonS3Client(GetCredentials());
            List<UploadPartResponse> uploadResponses = new List<UploadPartResponse>();

            // 1. Initialize.
            InitiateMultipartUploadRequest initRequest =
                new InitiateMultipartUploadRequest()
                .WithBucketName(bucket)
                .WithKey(keyName);

            InitiateMultipartUploadResponse initResponse =
                s3Client.InitiateMultipartUpload(initRequest);

            // 2. Upload Parts.
            long contentLength = new FileInfo(filePath).Length;
            long partSize = 5242880; // 5 MB

            var list = new List<UploadPartRequest>();
            long filePosition = 0;
            int i = 1;
            while (filePosition < contentLength)
            {
                // Create request to upload a part.
                var uploadRequest = new UploadPartRequest()
                    .WithBucketName(bucket)
                    .WithKey(keyName)
                    .WithUploadId(initResponse.UploadId)
                    .WithPartNumber(i)
                    .WithPartSize(partSize)
                    .WithFilePosition(filePosition)
                    .WithFilePath(filePath)
                    .WithTimeout(3600000);

                list.Add(uploadRequest);
                filePosition += partSize;
                ++i;
            }

            var q = list
                //.Select(req => s3Client.UploadPart(req))
                .Select(res => Observable.FromEvent<EventHandler<UploadPartProgressArgs>, PartUploadProgressChangedArgs>(
                            h => (s, e) => h(new PartUploadProgressChangedArgs(res, e)),
                            h => res.UploadPartProgressEvent += h,
                            h => res.UploadPartProgressEvent -= h));
            var q1 = Observable.Start<bool>(() =>
                {
                    try
                    {
                        var prs = Parallel.ForEach(list, res => uploadResponses.Add(s3Client.UploadPart(res)));

                        if (prs.IsCompleted)
                        {
                            CompleteMultipartUploadRequest compRequest =
                                        new CompleteMultipartUploadRequest()
                                        .WithBucketName(bucket)
                                        .WithKey(keyName)
                                        .WithUploadId(initResponse.UploadId)
                                        .WithPartETags(uploadResponses);

                            CompleteMultipartUploadResponse completeUploadResponse =
                                s3Client.CompleteMultipartUpload(compRequest);
                           // MessageBox.Show(completeUploadResponse.ResponseXml);
                            return true;
                        }
                        s3Client.AbortMultipartUpload(new AbortMultipartUploadRequest()
                            .WithBucketName(bucket)
                            .WithKey(keyName)
                            .WithUploadId(initResponse.UploadId));
                        return false;
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                        s3Client.AbortMultipartUpload(new AbortMultipartUploadRequest()
                            .WithBucketName(bucket)
                            .WithKey(keyName)
                            .WithUploadId(initResponse.UploadId));
                        return false;
                    }
                });

            //list.ToObservable(Scheduler.ThreadPool)
            //    .Do(res => { uploadResponses.Add(s3Client.UploadPart(res)); })
            //    .Finally(() =>
            //        {
            //            CompleteMultipartUploadRequest compRequest =
            //                new CompleteMultipartUploadRequest()
            //                .WithBucketName(bucket)
            //                .WithKey(keyName)
            //                .WithUploadId(initResponse.UploadId)
            //                .WithPartETags(uploadResponses);

            //            CompleteMultipartUploadResponse completeUploadResponse =
            //                s3Client.CompleteMultipartUpload(compRequest);
            //        })
            //        .Subscribe();
            return Tuple.Create(q, q1);
          //  return list;
            //return Observable.While(() => filePosition < contentLength,
            //    Observable.Return(new UploadPartRequest()
            //        .WithBucketName(bucket)
            //        .WithKey(keyName)
            //        .WithUploadId(initResponse.UploadId)
            //      //  .WithPartNumber(i)
            //      //  .WithPartSize(partSize)
            //      //  .WithFilePosition(filePosition)
            //        .WithFilePath(filePath), Scheduler.ThreadPool)
            //    .Do(req =>
            //    {
            //        req.PartNumber = i;
            //        req.PartSize = partSize;
            //        req.FilePosition = filePosition;
            //        uploadResponses.Add(s3Client.UploadPart(req));
            //        filePosition += partSize;
            //        ++i;
            //    }))
            //    //.Do(req =>
            //    //    {
            //    //        uploadResponses.Add(s3Client.UploadPart(req));
            //    //    })
            //    .Finally(() => 
            //    {
            //        CompleteMultipartUploadRequest compRequest =
            //        new CompleteMultipartUploadRequest()
            //        .WithBucketName(bucket)
            //        .WithKey(keyName)
            //        .WithUploadId(initResponse.UploadId)
            //        .WithPartETags(uploadResponses);

            //            CompleteMultipartUploadResponse completeUploadResponse =
            //                s3Client.CompleteMultipartUpload(compRequest);
            //    });
            //while (filePosition < contentLength)
            //{
            //    // Create request to upload a part.
            //    var uploadRequest = new UploadPartRequest()
            //        .WithBucketName(bucket)
            //        .WithKey(keyName)
            //        .WithUploadId(initResponse.UploadId)
            //        .WithPartNumber(i)
            //        .WithPartSize(partSize)
            //        .WithFilePosition(filePosition)
            //        .WithFilePath(filePath);

            //    yield return uploadRequest;

            //    uploadResponses.Add(s3Client.UploadPart(uploadRequest));

            //    filePosition += partSize;
            //    ++i;
            //}

            ////Observable.FromEvent<EventHandler<UploadPartProgressArgs>, PartUploadProgressChangedArgs>(
            ////    h => (s, e) => h(new PartUploadProgressChangedArgs(uploadRequest, e)),
            ////    h => uploadRequest.UploadPartProgressEvent += h,
            ////    h => uploadRequest.UploadPartProgressEvent -= h);
            //// Upload part and add response to our list.

            //// Step 3: complete.
            //CompleteMultipartUploadRequest compRequest =
            //    new CompleteMultipartUploadRequest()
            //    .WithBucketName(bucket)
            //    .WithKey(keyName)
            //    .WithUploadId(initResponse.UploadId)
            //    .WithPartETags(uploadResponses);

            //CompleteMultipartUploadResponse completeUploadResponse =
            //    s3Client.CompleteMultipartUpload(compRequest);

            //try
            //{
                

                
            //}
            //catch (Exception exception)
            //{
            //    Console.WriteLine("Exception occurred: {0}", exception.Message);
            //    s3Client.AbortMultipartUpload(new AbortMultipartUploadRequest()
            //        .WithBucketName(bucket)
            //        .WithKey(keyName)
            //        .WithUploadId(initResponse.UploadId));
            //}
            //return list.ToObservable();
        }
    }

    public class PartUploadProgressChangedArgs : EventArgs
    {
        public PartUploadProgressChangedArgs(UploadPartRequest req,UploadPartProgressArgs e)
        {
            PartNumber = req.PartNumber;
            PercentDone = e.PercentDone;
            TotalBytes = e.TotalBytes;
            TransferredBytes = e.TransferredBytes;
        }
        public int PartNumber { get; set; }
        public int PercentDone { get; set; }
        public long TotalBytes { get; set; }
        public long TransferredBytes { get; set; }
    }
}
