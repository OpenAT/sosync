using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public class SosyncSystem
    {
        public string Value { get; private set; }

        public static SosyncSystem FSOnline { get { return new SosyncSystem("fso"); } }
        public static SosyncSystem FundraisingStudio { get { return new SosyncSystem("fs"); } }

        private SosyncSystem(string value)
        {
            Value = value;
        }
    }
}
