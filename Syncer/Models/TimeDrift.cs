using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class TimeDrift
    {
        #region Constructors
        public TimeDrift(int ntp, int fso, int fs)
        {
            NTP = ntp;
            FSOnline = fso;
            FS = fs;
        }
        #endregion

        #region Properties
        public int NTP { get; private set; }
        public int FSOnline { get; private set; }
        public int FS { get; private set; }
        #endregion
    }
}
