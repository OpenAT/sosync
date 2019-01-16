using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Syncer.Models
{
    // [XmlInclude(typeof(dboAktionOnlineToken))]
    public class StudioAktion
    {
        public dboAktion Aktion { get; set; }
        public MdbModelBase AktionDetail { get; set; }
    }
}
