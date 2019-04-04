using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Constants
{
    public class SosyncJobSourceType
    {
        public string Value { get; private set; }

        public static SosyncJobSourceType Default { get; private set; }
        public static SosyncJobSourceType MergeInto { get; private set; }
        public static SosyncJobSourceType Delete { get; private set; }
        public static SosyncJobSourceType Temp { get; private set; }

        static SosyncJobSourceType()
        {
            Default = new SosyncJobSourceType("");
            MergeInto = new SosyncJobSourceType("merge_into");
            Delete = new SosyncJobSourceType("delete");
            Temp = new SosyncJobSourceType("temp");
        }

    private SosyncJobSourceType(string value)
        {
            Value = value;
        }
    }
}
