using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WebSosync.Converters;

namespace WebSosync.Models
{
    public class JobDto
    {
        [Required]
        [JsonConverter(typeof(CustomDateTimeConverter))]
        public DateTime job_date { get; set; }
        
        [Required]
        [RegularExpression("^(fs|fso)$")]
        public string job_source_system { get; set; }
        
        [Required]
        public string job_source_model { get; set; }
        
        [Required]
        [Range(1, int.MaxValue)]
        public int job_source_record_id { get; set; }
        
        [RegularExpression("^(delete|merge_into)$")]
        public string job_source_type { get; set; }

        [Range(1, int.MaxValue)]
        public int? job_source_merge_into_record_id { get; set; }
        
        [Range(1, int.MaxValue)]
        public int? job_source_target_merge_into_record_id { get; set; }

        [Range(1, int.MaxValue)]
        public int? job_source_target_record_id { get; set; }

        [Required]
        [JsonConverter(typeof(CustomDateTimeConverter))]
        public DateTime job_source_sosync_write_date { get; set; }
        
        [JsonConverter(typeof(DictionaryConverter))]
        public Dictionary<string, string> job_source_fields { get; set; }
        
        [Range(0, int.MaxValue)]
        public int? job_priority { get; set; }

        public JobDto[] children { get; set; }
    }
}
