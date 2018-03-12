using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartnerBpk
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }

        [DataMember(Name = "create_date")]
        public DateTime Create_Date { get; set; }

        public string bpk_private { get; set; }
        public string bpk_public { get; set; }
        public string bpk_request_data { get; set; }
        public DateTime? bpk_request_date { get; set; }
        public object[] bpk_request_partner_id { get; set; }
        public object[] bpk_request_company_id { get; set; }
        public string bpk_request_log { get; set; }
        public string bpk_request_url { get; set; }
        public string bpk_request_firstname { get; set; }
        public string bpk_request_lastname { get; set; }
        public DateTime? bpk_request_birthdate { get; set; }
        public string bpk_request_zip { get; set; }

        public DateTime? bpk_error_request_date { get; set; }
        public string bpk_error_request_data { get; set; }
        public string bpk_error_request_url { get; set; }
        public string bpk_error_request_lastname { get; set; }
        public string bpk_error_request_firstname { get; set; }
        public DateTime? bpk_error_request_birthdate { get; set; }
        public string bpk_error_request_zip { get; set; }
        public string bpk_error_response_data { get; set; }
        public string bpk_error_code { get; set; }
        public string bpk_error_text { get; set; }

        public string bpk_response_data { get; set; }

        public DateTime? last_bpk_request { get; set; }


        public string state { get; set; }

        [DataMember(Name = "sosync_fs_id")]
        public int? Sosync_FS_ID { get; set; }

        [DataMember(Name = "write_date")]
        public DateTime? Write_Date { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }
    }
}
