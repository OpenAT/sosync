using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Models
{
    /// <summary>
    /// Represents a sync job data transfer object (DTO) to be used
    /// by webservice controllers.
    /// </summary>
    public class SyncJobDto
    {
        public string Source_System { get; set; }
        public string Source_Model { get; set; }
        public string Source_Record_ID { get; set; }

        /// <summary>
        /// The actual job creation date. Use UTC time.
        /// </summary>
        public DateTime Job_Date { get; set; }
    }
}
