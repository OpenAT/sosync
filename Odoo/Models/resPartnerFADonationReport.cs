using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartnerFADonationReport
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }
        
        public DateTime? write_date { get; set; }
        
        public DateTime? sosync_write_date { get; set; }

        public object[] partner_id { get; set; }
        public object[] bpk_company_id { get; set; }
        public DateTime? anlage_am_um { get; set; }
        public DateTime? ze_datum_von { get; set; }
        public DateTime? ze_datum_bis { get; set; }
        public int? meldungs_jahr { get; set; }
        public decimal? betrag { get; set; }

        public DateTime? sub_datetime { get; set; }
        public string sub_url { get; set; }
        public string sub_typ { get; set; }
        public string sub_data { get; set; }
        public string sub_response { get; set; }
        public decimal? sub_request_time { get; set; }
        public string sub_log { get; set; }
        public object[] sub_bpk_id { get; set; }
        public string sub_bpk_company_name { get; set; }
        public string sub_bpk_company_stammzahl { get; set; }
        public string sub_bpk_private { get; set; }
        public string sub_bpk_public { get; set; }
        public string sub_bpk_firstname { get; set; }
        public string sub_bpk_lastname { get; set; }
        public DateTime? sub_bpk_birthdate { get; set; }
        public string sub_bpk_zip { get; set; }

        public string error_code { get; set; }
        public string error_text { get; set; }


        public string state { get; set; }
        

    }
}
