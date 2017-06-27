using System.Runtime.Serialization;

namespace WebSosync.Models
{
    [DataContract(Name = "job_result")]
    public class JobResultDto
    {
        [DataMember(Name = "job_id")]
        public int JobID { get; set; }

        [DataMember(Name = "error_code")]
        public int ErrorCode { get; set; }

        [DataMember(Name = "error_text")]
        public string ErrorText { get; set; }

        [DataMember(Name = "error_detail")]
        public string ErrorDetail { get; set; }
    }
}
