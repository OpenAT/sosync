using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class Partner
    {
        public int ID { get; set; }
        public int? ForeignID { get; set; }
        public string Nachname { get; set; }
        public string Vorname { get; set; }
        public string Strasse { get; set; }
        public string Hausnr { get; set; }
        public string Plz { get; set; }
        public string Ort { get; set; }
        public DateTime? Geburtsdatum { get; set; }
    }
}
