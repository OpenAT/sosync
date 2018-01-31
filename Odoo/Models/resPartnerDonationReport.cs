using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartnerDonationReport
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }
        
        public DateTime? write_date { get; set; }  
        public DateTime? sosync_write_date { get; set; }
        public int? sosync_fs_id { get; set; }

        public string state { get; set; }
        public string info { get; set; }
        public string submission_env { get; set; }
        public object[] partner_id { get; set; }
        public object[] bpk_company_id { get; set; }
        public DateTime? anlage_am_um { get; set; }
        public DateTime? ze_datum_von { get; set; }
        public DateTime? ze_datum_bis { get; set; }
        public int? meldungs_jahr { get; set; }
        public decimal? betrag { get; set; }
        public string cancellation_for_bpk_private { get; set; }
        public string submission_type { get; set; }
        public string submission_refnr { get; set; }
        public string submission_firstname { get; set; }
        public string submission_lastname { get; set; }
        public DateTime? submission_birthdate_web { get; set; }
        public string submission_zip { get; set; }
        public string submission_bpk_request_id { get; set; }
        public string submission_bpk_public { get; set; }
        public string submission_bpk_private { get; set; }
        public string response_content { get; set; }
        public string response_error_code { get; set; }
        public string response_error_detail { get; set; }
        public string error_type { get; set; }
        public string error_code { get; set; }
        public string error_detail { get; set; }
        public object[] report_erstmeldung_id { get; set; }
        public object[] report_follow_up_ids { get; set; }
        public object[] skipped_by_id { get; set; }
        public bool? skipped { get; set; }
    }
}
