using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data.Interfaces;

namespace WebSosync.Data.Models
{
    public class SosyncJob : ITree<SosyncJob>
    {
        #region Constructors
        public SosyncJob()
        {
            Children = new List<SosyncJob>();
        }
        #endregion

        #region Methods

        #endregion

        #region Properties
        /// <summary>
        /// The sync job ID for sosync.
        /// </summary>
        public int SosyncID { get; set; }

        /// <summary>
        /// The sync job ID in the source system.
        /// </summary>
        public int Job_ID { get; set; }

        public DateTime Date { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncState"/> only.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncError"/> only.
        /// </summary>
        public string Error_Code { get; set; }

        public int? Parent_Job_ID { get; set; }
        public DateTime? Child_Start { get; set; }
        public DateTime? Child_End { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        public string Source_System { get; set; }

        public string Source_Model { get; set; }
        public int Source_Record_ID { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        public string Target_System { get; set; }

        public string Target_Model { get; set; }
        public int? Target_Record_ID { get; set; } 

        public string Source_Data { get; set; }
        public string Target_Request { get; set; }
        public DateTime? Target_Request_Start { get; set; }
        public DateTime? Target_Request_End { get; set; }
        public string Target_Request_Answer { get; set; }

        public DateTime? job_fetched { get; set; }
        public int Run_Counter { get; set; }

        public IList<SosyncJob> Children { get; set; }
        #endregion

        #region Interface ITree<SosyncJob> implementation
        // Other two interface members matched already
        public int ID => this.Job_ID;
        public int? ParentID => this.Parent_Job_ID;
        #endregion
    }
}
