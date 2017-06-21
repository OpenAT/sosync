using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Helpers
{
    public static class ConsoleHelper
    {
        public static void WriteColor(ConsoleColor col, string message)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = col;

            Console.Write(message);

            Console.ForegroundColor = prevColor;
        }

        public static void WriteColorLine(ConsoleColor col, string message)
        {
            WriteColor(col, message + Environment.NewLine);
        }
    }
}
