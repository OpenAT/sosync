using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class IdentityModel
    {
        public int? StudioID { get; set; }
        public int? OnlineID { get; set; }

        public DateTime? SosyncWriteDate { get; set; }

        public IdentityModel()
        {
            StudioID = null;
            OnlineID = null;
            SosyncWriteDate = null;
        }

        public IdentityModel(int? studioID, int? onlineID, DateTime? sosyncWriteDate)
        {
            StudioID = studioID;
            OnlineID = onlineID;
            SosyncWriteDate = sosyncWriteDate;
        }
    }
}
