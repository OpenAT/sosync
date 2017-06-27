using System.Runtime.Serialization;
using WebSosync.Enumerations;

namespace WebSosync.Models
{
    [DataContract(Name = "job_result")]
    public class JobResultDto
    {
        [DataMember(Name = "job_id")]
        public int JobID { get; set; }

        [DataMember(Name = "error_code")]
        public JobErrorCode ErrorCode { get; set; }

        [DataMember(Name = "error_text")]
        public string ErrorText { get; set; }

        [DataMember(Name = "error_detail")]
        public object ErrorDetail { get; set; }
    }
}
