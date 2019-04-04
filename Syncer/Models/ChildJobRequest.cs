using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;

namespace Syncer.Models
{
    public class ChildJobRequest
    {
        #region Properties
        public string JobSourceSystem { get; set; }
        public string JobSourceModel { get; set; }
        public int JobSourceRecordID { get; set; }
        public bool ForceDirection { get; set; }
        #endregion

        #region Constructors
        public ChildJobRequest(SosyncSystem system, string model, int recordID, bool forceDirection)
        {
            JobSourceSystem = system.Value;
            JobSourceModel = model;
            JobSourceRecordID = recordID;
            ForceDirection = forceDirection;
        }
        #endregion
    }
}
