using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Services
{
    public static class ThreadService
    {
        public static Dictionary<string, int> JobLocks = new Dictionary<string, int>();
    }
}
