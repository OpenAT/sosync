using System;
using System.Collections.Generic;
using System.Text;

namespace MassDataCorrection.Models
{
    public class PgDonationReportCorrection
    {
        public int id;
        public int sosync_fs_id;
        public DateTime? submission_id_datetime;
        public string response_error_orig_refnr;
    }

    public class MsDonationReportCorrection
    {
        public int AktionsID;
        public int sosync_fso_id;
        public DateTime? SubmissionIdDate;
        public string ResponseErrorOrigRefNr;
    }
}
