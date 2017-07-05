using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Attributes
{
    public class OnlineModelAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
