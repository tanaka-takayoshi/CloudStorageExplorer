using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using Livet;
using Livet.Commands;
using Livet.Messaging;
using Livet.Messaging.IO;
using Livet.Messaging.Windows;

using CloudStrageTool.Models;
using System.IO;

using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Windows;
using Amazon.S3.Model;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace CloudStrageTool.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        /*コマンド、プロパティの定義にはそれぞれ 
         * 
         *  lvcom   : ViewModelCommand
         *  lvcomn  : ViewModelCommand(CanExecute無)
         *  llcom   : ListenerCommand(パラメータ有のコマンド)
         *  llcomn  : ListenerCommand(パラメータ有のコマンド・CanExecute無)
         *  lprop   : 変更通知プロパティ
         *  
         * を使用してください。
         */

        /*ViewModelからViewを操作したい場合は、
         * Messengerプロパティからメッセージ(各種InteractionMessage)を発信してください。
         */

        /*
         * UIDispatcherを操作する場合は、DispatcherHelperのメソッドを操作してください。
         * UIDispatcher自体はApp.xaml.csでインスタンスを確保してあります。
         */

        /*
         * Modelからの変更通知などの各種イベントをそのままViewModelで購読する事はメモリリークの
         * 原因となりやすく推奨できません。ViewModelHelperの各静的メソッドの利用を検討してください。
         */

        S3Model s3Model;

        MainModel main;

        public MainWindowViewModel()
        {
            main = new MainModel();
            s3Model = new S3Model();

            Buckets = ViewModelHelper.CreateReadOnlyNotificationDispatcherCollection(
                main.Buckets,
                m => new S3BucketViewModel(m, this),
                DispatcherHelper.UIDispatcher);

            main.LoadAsync();
        }

        #region FileName変更通知プロパティ
        string _FileName = @"E:\Downloads\VC-Compiler-KB2519277.exe";

        public string FileName
        {
            get
            { return _FileName; }
            set
            {
                if (_FileName == value)
                    return;
                _FileName = value;
                RaisePropertyChanged("FileName");
                BeginUploadCommand.RaiseCanExecuteChanged();
            }
        }
        #endregion


        #region StatusText変更通知プロパティ
        string _StatusText;

        public string StatusText
        {
            get
            { return _StatusText; }
            set
            {
                if (_StatusText == value)
                    return;
                _StatusText = value;
                RaisePropertyChanged("StatusText");
            }
        }
        #endregion


        #region Text変更通知プロパティ
        string _Text;

        public string Text
        {
            get
            { return _Text; }
            set
            {
                if (_Text == value)
                    return;
                _Text = value;
                RaisePropertyChanged("Text");
            }
        }
        #endregion


        S3BucketViewModel _SelectedBucket;

        public S3BucketViewModel SelectedBucket
        {
            get
            { return _SelectedBucket; }
            set
            {
                if (_SelectedBucket == value)
                    return;
                _SelectedBucket = value;
                RaisePropertyChanged("SelectedBucket");
            }
        }



        string _KeyName;

        public string KeyName
        {
            get
            { return _KeyName; }
            set
            {
                if (_KeyName == value)
                    return;
                _KeyName = value;
                RaisePropertyChanged("KeyName");
            }
        }
      
        #region BeginUploadCommand
        ViewModelCommand _BeginUploadCommand;

        public ViewModelCommand BeginUploadCommand
        {
            get
            {
                if (_BeginUploadCommand == null)
                    _BeginUploadCommand = new ViewModelCommand(BeginUpload, CanBeginUpload);
                return _BeginUploadCommand;
            }
        }

        private bool CanBeginUpload()
        {
            return FileName != null;
        }

        private void BeginUpload()
        {
            StatusText = "start";
            Text = "";
            var bucketName = SelectedBucket.BucketName;
            var key = KeyName;

            var tuple = s3Model.UploadFile(FileName, bucketName, key);
            foreach (var res in tuple.Item1)
            {
                res.Subscribe(
                             rs => { Text += rs.PartNumber + ":" + rs.PercentDone + "%" + Environment.NewLine; },
                              e => { MessageBox.Show(e.Message); });
            }
            tuple.Item2.ObserveOnDispatcher()
                .Subscribe(res => { StatusText = "end"; });
            //s3Model.UploadFile(FileName, bucketName, key)
            //    //.ToObservable(Scheduler.ThreadPool)
            //    .ForEach(res =>
            //        {

            //            res.Subscribe(
            //                 rs => { Text += rs.PartNumber + ":" + rs.PercentDone + "%" + Environment.NewLine; },
            //                  e => { MessageBox.Show(e.Message); });
            //            //Text += res.PartNumber + "Start" + Environment.NewLine;
            //            //Observable.FromEvent<EventHandler<UploadPartProgressArgs>, PartUploadProgressChangedArgs>(
            //            //    h => (s, e) => h(new PartUploadProgressChangedArgs(res, e)),
            //            //    h => res.UploadPartProgressEvent += h,
            //            //    h => res.UploadPartProgressEvent -= h)
            //            //    //.ObserveOnDispatcher()
            //            //    //.Finally(() => { Text += res.PartNumber + "End" + Environment.NewLine; })
            //            //    .Subscribe(
            //            //     rs => { Text += rs.PartNumber + ":" + rs.PercentDone + "%" + Environment.NewLine; },
            //            //      e => { MessageBox.Show(e.Message); });
            //        });
                    //, e => { MessageBox.Show(e.Message); });

            //query.ObserveOnDispatcher()
            //    .Subscribe(res => 
            //    {
            //        res.ObserveOnDispatcher()
            //            .Subscribe(rs =>
            //                {
            //                    Text += rs.PartNumber + ":" + rs.PercentDone + "%" + Environment.NewLine;
            //                },
            //                e => { StatusText = e.Message; });
            //    },
            //                e => { StatusText = e.Message; });
        }
        #endregion

        #region BeginAzureUploadCommand
        ViewModelCommand _BeginAzureUploadCommand;

        public ViewModelCommand BeginAzureUploadCommand
        {
            get
            {
                if (_BeginAzureUploadCommand == null)
                    _BeginAzureUploadCommand = new ViewModelCommand(BeginAzureUpload, CanBeginAzureUpload);
                return _BeginAzureUploadCommand;
            }
        }

        private bool CanBeginAzureUpload()
        {
            return FileName != null;
        }

        private void BeginAzureUpload()
        {

            Text = "";
            var AccountName = "";
            var AccessKey = "";

            var accountAndKey = new StorageCredentialsAccountAndKey(AccountName, AccessKey);
            var account = new CloudStorageAccount(accountAndKey, true); // HTTPS を利用する場合は true

            var client = account.CreateCloudBlobClient();
            
            CloudBlobContainer container = client.GetContainerReference("fileupload");
            container.CreateIfNotExist();
            client.ParallelOperationThreadCount = 10;

            CloudBlob blob = container.GetBlobReference(KeyName);
            StatusText = "start as " + client.ParallelOperationThreadCount.ToString();
            
            Random rand = new Random();
            long blockIdSequenceNumber = (long)rand.Next() << 32;
            blockIdSequenceNumber += rand.Next();
            blob.ToBlockBlob.ParallelUpload(new FileStream(FileName, FileMode.Open), blockIdSequenceNumber, new BlobRequestOptions() { Timeout = TimeSpan.FromHours(1)});
            StatusText = "End";
        }
        #endregion

        public ReadOnlyNotificationDispatcherCollection<S3BucketViewModel> Buckets
        {
            get;
            private set;
        }
    }
}
