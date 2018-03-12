using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class PhoneCorrection
    {
        public string Landkennzeichen { get; set; }
        public string Landkennzeichen_mit_Plus { get; set; }
        public string Landkennzeichen_mit_Nullen { get; set; }
        public string LandID { get; set; }
        public string Vorwahl { get; set; }
        public string Vorwahl_mit_Null { get; set; }
        public string Rufnummer { get; set; }
        public string Rufnummer_Gruppiert { get; set; }
        public string MicrosoftStandard { get; set; }
        public string Ergebnis { get; set; }
        public string MobilFestnetz { get; set; }
    }
}
