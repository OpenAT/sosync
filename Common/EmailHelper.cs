using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common
{
    public static class EmailHelper
    {
        private static string NullIfEmpty(string s)
        {
            if (s == "")
                return null;

            return s;
        }

        public static string MergeEmail(string account, string domain)
        {
            var fullEmail = (account ?? "") + "@" + (domain ?? "")
                .Trim();

            if (fullEmail == "@")
                return null;

            if (fullEmail.StartsWith("@"))
                return fullEmail.Substring(1);

            if (fullEmail.EndsWith("@"))
                return fullEmail.Substring(0, fullEmail.Length - 1);

            return NullIfEmpty(fullEmail);
        }

        public static void SplitEmail(string fullEmail, out string account, out string domain)
        {
            account = null;
            domain = null;

            if (string.IsNullOrEmpty(fullEmail))
                return;

            var parts = fullEmail.Split('@');

            if (parts.Length == 1)
            {
                if (parts[0].StartsWith("@"))
                    domain = parts[0].Substring(1);
                else if (parts[0].EndsWith("@"))
                    account = parts[0].Substring(0, parts[0].Length - 1);
                else
                    account = NullIfEmpty(parts[0]);
            }
            else if (parts.Length == 2)
            {
                account = NullIfEmpty(parts[0]);
                domain = NullIfEmpty(parts[1]);
            }
            else
            {
                account = string.Join("@", parts, 0, parts.Length - 1); // all parts except last
                domain = parts[parts.Length - 1];
            }
        }
    }
}
