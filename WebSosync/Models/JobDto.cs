using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebSosync.Models
{
    public class JobDto
    {
        [Required]
        public DateTime job_date { get; set; }
        [Required]
        public string job_source_system { get; set; }
        [Required]
        public string job_source_model { get; set; }
        [Required]
        public int job_source_record_id { get; set; }
        public string job_source_type { get; set; }

        public int? job_source_merge_into_record_id { get; set; }
        public int? job_source_target_merge_into_record_id { get; set; }

        public int? job_source_target_record_id { get; set; }

        [Required]
        public DateTime job_source_sosync_write_date { get; set; }
        public Dictionary<string, string> job_source_fields { get; set; }
        public int? job_priority { get; set; }

        public JobDto[] children { get; set; }
    }
}
