using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Syncer.Flows
{
    public class dboxBPKAccount : dadi_data.Models.dboxBPKAccount
    {
        [DataMember(Name="sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }
    }
}
