using Microsoft.AspNetCore.Mvc;
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
        // FromQuery for automatic url parsing
        // DisplayFormat to prevent conversion of empty strings to null by MVC
        // Rest: validation attributes

        [DataMember(Name = "job_source_system")]
        [FromQuery(Name = "job_source_system")]
        [JsonProperty(PropertyName = "job_source_system")]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [Required(AllowEmptyStrings = true, ErrorMessage = "job_source_system is required.")]
        public string JobSourceSystem { get; set; }

        [DataMember(Name = "job_source_model")]
        [FromQuery(Name = "job_source_model")]
        [JsonProperty(PropertyName = "job_source_model")]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [Required(AllowEmptyStrings = true, ErrorMessage = "job_source_model is required.")]
        public string JobSourceModel { get; set; }

        [DataMember(Name = "job_source_record_id")]
        [FromQuery(Name = "job_source_record_id")]
        [JsonProperty(PropertyName = "job_source_record_id")]
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "job_source_record_id must be greter than 0.")]
        public int? JobSourceRecordID { get; set; }

        /// <summary>
        /// The actual job creation date. Use UTC time.
        /// </summary>
        [DataMember(Name = "job_date")]
        [FromQuery(Name = "job_date")]
        [JsonProperty(PropertyName = "job_date")]
        [Required]
        public DateTime? JobDate { get; set; }
    }
}
