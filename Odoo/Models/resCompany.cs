using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resCompany
    {
        [DataMember(Name="name")]
        public string Name { get; set; }

        [DataMember(Name = "partner_id")]
        public string[] Partner { get; set; }
    }
}
