using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class PgPartnerBpkCorrection
    {
        public int id;
        public int sosync_fs_id;

        public string bpk_request_zip;
        public string bpk_error_request_zip;
        public string bpk_request_log;
        public DateTime? last_bpk_request;
        public string bpk_request_url;
        public string bpk_error_request_url;
        public string state;
    }

    public class MsPartnerBpkCorrection
    {
        public int PersonBPKID;
        public int sosync_fso_id;

        public string PLZ;
        public string FehlerPLZ;
        public string RequestLog;
        public DateTime? LastRequest;
        public string RequestUrl;
        public string ErrorRequestUrl;
        public string fso_state;
    }
}
