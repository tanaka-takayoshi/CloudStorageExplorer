using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Livet;
using CloudStrageTool.Models;

namespace CloudStrageTool.ViewModels
{
    public class S3BucketViewModel : ViewModel
    {
        private S3BucketModel _model;

        public S3BucketViewModel(S3BucketModel m, MainWindowViewModel parent)
        {
            _model = m;
            MainWindowViewModel = parent;

            //入力値やエラー情報の初期化
            //InitializeInput();
            
            //ModelのPropertyChangedイベントはBindNotifyChangedを使用してハンドルします。
            //こうする事で弱イベントによるModelイベントの購読が行われます。
            //イベントハンドラのライフサイクルはこのViewModelと同じなのでリークしませんし、
            //イベントハンドラがViewModelより先に消えません。
            ViewModelHelper.BindNotifyChanged(
                _model,
                this,
                (sender, e) =>
                {
                    RaisePropertyChanged(e.PropertyName);
                });
        }

        public MainWindowViewModel MainWindowViewModel
        {
            get;
            private set;
        }

        #region Modelプロパティのラッパー

        public string BucketName
        {
            get
            { return _model.BucketName; }
            set
            { _model.BucketName = value; }
        }

        public string CreationDate
        {
            get
            { return _model.CreationDate; }
            set
            { _model.CreationDate = value; }
        }

        #endregion
    }
}
