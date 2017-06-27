﻿using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;

namespace WebSosync.Models
{
    /// <summary>
    /// Represents a sync job data transfer object (DTO) to be used
    /// by webservice controllers.
    /// </summary>
    [DataContract(Name = "sync_job")]
    public class SyncJobDto
    {
        // DataMember for serialization
        // FromQuery for url parsing
        // Rest: validation attributes

        [DataMember(Name = "source_system")]
        [FromQuery(Name = "source_system")]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [Required(AllowEmptyStrings = true, ErrorMessage = "source_system is required.")]
        public string SourceSystem { get; set; }

        [DataMember(Name = "source_model")]
        [FromQuery(Name = "source_model")]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [Required(AllowEmptyStrings = true, ErrorMessage = "source_model is required.")]
        public string SourceModel { get; set; }

        [DataMember(Name = "source_record_id")]
        [FromQuery(Name = "source_record_id")]
        [Range(1, Int32.MaxValue)]
        public int? SourceRecordID { get; set; }

        /// <summary>
        /// The actual job creation date. Use UTC time.
        /// </summary>
        [DataMember(Name = "job_date")]
        [FromQuery(Name = "job_date")]
        [Required]
        public DateTime? JobDate { get; set; }
    }
}
