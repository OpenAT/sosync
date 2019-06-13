using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class CheckModel
    {
        public string ModelName { get; set; }
        public int ID { get; set; }
        public int? ForeignID { get; set; }
        public DateTime? SosyncWriteDate { get; set; }
        public string Data { get; set; }
    }
}
