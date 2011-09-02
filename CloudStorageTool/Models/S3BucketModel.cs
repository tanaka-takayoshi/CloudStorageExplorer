using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Livet;

namespace CloudStrageTool.Models
{
    public class S3BucketModel : NotificationObject
    {
        public string BucketName { get; set; }
        public string CreationDate { get; set; }
    }
}
