using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class ModelInfo
    {
        #region Properties
        /// <summary>
        /// The ID for the model.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The referenced ID for the model in the foreign system.
        /// </summary>
        public int? ForeignID { get; set; }

        /// <summary>
        /// The last sosync write date for the model.
        /// </summary>
        public DateTime? SosyncWriteDate { get; set; }

        /// <summary>
        /// The last write date for the model.
        /// </summary>
        public DateTime? WriteDate { get; set; }

        /// <summary>
        /// The last version stamp sosync synchronized.
        /// </summary>
        public DateTime? SosyncSyncedVersion { get; set; }
        #endregion

        #region Constructor
        public ModelInfo(int id, int? foreignID, DateTime? sosyncWriteDate, DateTime? writeDate, DateTime? sosyncSyncedVersion)
        {
            ID = id;
            ForeignID = foreignID;
            SosyncWriteDate = sosyncWriteDate;
            WriteDate = writeDate;
            SosyncSyncedVersion = sosyncSyncedVersion;
        }
        #endregion
    }
}
