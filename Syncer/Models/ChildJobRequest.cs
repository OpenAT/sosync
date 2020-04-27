using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Models
{
    public class ChildJobRequest
    {
        #region Properties
        public string JobSourceSystem { get; set; }
        public string JobSourceModel { get; set; }
        public int JobSourceRecordID { get; set; }
        public SosyncJobSourceType JobSourceType { get; set; }
        public bool ForceDirection { get; set; }
        #endregion

        #region Constructors
        public ChildJobRequest(SosyncSystem system, string model, int recordID, SosyncJobSourceType jobSourceType, bool forceDirection)
        {
            JobSourceSystem = system.Value;
            JobSourceModel = model;
            JobSourceRecordID = recordID;
            JobSourceType = jobSourceType;
            ForceDirection = forceDirection;
        }
        #endregion
    }
}
