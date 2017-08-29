using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartner
    {
        [DataMember(Name = "firstname")]
        public string FirstName { get; set; }

        [DataMember(Name = "lastname")]
        public string LastName { get; set; }

        [DataMember(Name = "name_zwei")]
        public string Name_Zwei { get; set; }
        
        [DataMember(Name = "sosync_fs_id")]
        public int Sosync_FS_ID { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }
    }
}
