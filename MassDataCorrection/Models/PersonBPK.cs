using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class PersonBPK
    {
        public int ID { get; set; }
        public int? ForeignID { get; set; }
        public string Vorname { get; set; }
        public string BpkPrivat { get; set; }
        public DateTime? sosync_write_date { get; set; }
    }
}
