using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Livet;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace CloudStrageTool.Models
{
    class MainModel : NotificationObject
    {
        public MainModel()
        {
            Buckets = new ObservableCollection<S3BucketModel>();
        }

        public void LoadAsync()
        {
            Buckets.Clear();
            Observable.Start(
                () =>
                {
                    foreach (var item in new S3Model().GetS3Buckets())
                    {
                        Buckets.Add(item);
                    }
                }, Scheduler.ThreadPool);
        }

        public ObservableCollection<S3BucketModel> Buckets
        {
            get;
            private set;
        }
    }
}
