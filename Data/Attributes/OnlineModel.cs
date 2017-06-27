using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSosync.Data
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class OnlineModel : Attribute
    {
        public string Name { get; set; }

        public OnlineModel(string name)
        {
            Name = name;
        }
    }
}
