using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resCountry
    {
        [DataMember(Name = "id")]
        public int? id { get; set; }
        [DataMember(Name = "code")]
        public string Code { get; set; }
    }
}
