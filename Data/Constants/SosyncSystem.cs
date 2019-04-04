using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public class SosyncSystem
    {
        public string Value { get; private set; }

        public static SosyncSystem FSOnline { get; private set; }
        public static SosyncSystem FundraisingStudio { get; private set; }

        static SosyncSystem()
        {
            FSOnline = new SosyncSystem("fso");
            FundraisingStudio = new SosyncSystem("fs");
        }

        private SosyncSystem(string value)
        {
            Value = value;
        }
    }
}
