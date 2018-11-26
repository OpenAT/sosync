using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class DonationReport
    {
        public int ID { get; set; }
        public int ForeignID { get; set; }
        public string State { get; set; }
        public string SosyncWriteDate { get; set; }
    }
}
