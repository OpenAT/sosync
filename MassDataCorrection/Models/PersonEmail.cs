using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class PersonEmail
    {
        public int ID { get; set; }
        public int? ForeignID { get; set; }
        public DateTime? SosyncWriteDate { get; set; }
        public string Email { get; set; }
    }
}
