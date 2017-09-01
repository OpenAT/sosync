using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartnerBpk
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }

        public string BPKErrorRequestZIP { get; set; }

        [DataMember(Name = "create_date")]
        public DateTime Create_Date { get; set; }

        public DateTime? BPKErrorRequestDate { get; set; }
        public string BPKErrorRequestData { get; set; }
        public string BPKErrorText { get; set; }
        public DateTime? BPKRequestBirthdate { get; set; }
        public string BPKErrorRequestFirstname { get; set; }
        public DateTime? BPKErrorRequestBirthdate { get; set; }
        public string BPKPrivate { get; set; }
        public string BPKErrorCode { get; set; }
        public string BPKRequestZIP { get; set; }
        public string BPKErrorRequestLastname { get; set; }
        public string BPKErrorResponseData { get; set; }
        public string BPKPublic { get; set; }
        public string BPKRequestFirstname { get; set; }
        public string BPKRequestLastname { get; set; }
        public string BPKRequestData { get; set; }
        public DateTime? BPKRequestDate { get; set; }
        public string BPKResponseData { get; set; }

        public object[] BPKRequestPartnerID { get; set; }
        public object[] BPKRequestCompanyID { get; set; }

        [DataMember(Name = "sosync_fs_id")]
        public int Sosync_FS_ID { get; set; }

        [DataMember(Name = "write_date")]
        public DateTime? Write_Date { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }
    }
}
