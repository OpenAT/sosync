using System.Runtime.Serialization;
using WebSosync.Enumerations;

namespace WebSosync.Models
{
    [DataContract(Name = "job_result")]
    public class JobResultDto
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }

        [DataMember(Name = "error_code")]
        public int ErrorCode { get; set; }

        [DataMember(Name = "error_text")]
        public string ErrorText { get; set; }

        [DataMember(Name = "error_detail")]
        public object ErrorDetail { get; set; }
    }
}
