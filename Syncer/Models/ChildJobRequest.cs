using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class ChildJobRequest
    {
        #region Properties
        public string JobSourceSystem { get; set; }
        public string JobSourceModel { get; set; }
        public int JobSourceRecordID { get; set; }
        #endregion

        #region Constructors
        public ChildJobRequest(string system, string model, int recordID)
        {
            JobSourceSystem = system;
            JobSourceModel = model;
            JobSourceRecordID = recordID;
        }
        #endregion
    }
}
