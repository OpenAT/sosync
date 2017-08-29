using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resCompany
    {
        [DataMember(Name="name")]
        public string Name { get; set; }

        [DataMember(Name = "partner_id")]
        public string[] Partner { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }

        [DataMember(Name = "write_date")]
        public DateTime? Write_Date { get; set; }

        [DataMember(Name = "sosync_fs_id")]
        public int? Sosync_FS_ID { get; set; }
    }
}
