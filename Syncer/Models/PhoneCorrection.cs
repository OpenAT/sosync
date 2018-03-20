using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class PhoneCorrection
    {
        public string Telefonlandkennzeichen { get; set; }
        public string Telefonvorwahl { get; set; }
        public string Telefonrufnummer { get; set; }
        public int TelefontypID { get; set; }
        public string TelefonLandname { get; set; }
        public string ReturnValue { get; set; }
    }
}
