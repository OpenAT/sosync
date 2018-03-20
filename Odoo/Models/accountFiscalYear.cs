using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class accountFiscalYear
    {
        [DataMember(Name = "id")]
        public int ID { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "company_id")]
        public object[] CompanyID { get; set; }

        [DataMember(Name = "date_start")]
        public DateTime? DateStart { get; set; }

        [DataMember(Name = "date_stop")]
        public DateTime? DateStop { get; set; }

        [DataMember(Name = "ze_datum_von")]
        public DateTime? ZeDatumVon { get; set; }

        [DataMember(Name = "ze_datum_bis")]
        public DateTime? ZeDatumBis { get; set; }

        [DataMember(Name = "meldezeitraum_start")]
        public DateTime? MeldezeitraumStart { get; set; }

        [DataMember(Name = "meldezeitraum_end")]
        public DateTime? MeldezeitraumEnd { get; set; }

        [DataMember(Name = "drg_interval_number")]
        public int DrgIntervalNumber { get; set; }

        [DataMember(Name = "drg_interval_type")]
        public string DrgIntervalType { get; set; }

        [DataMember(Name = "drg_next_run")]
        public DateTime? DrgNextRun { get; set; }

        [DataMember(Name = "drg_last")]
        public DateTime? DrgLast { get; set; }

        [DataMember(Name = "sosync_fs_id")]
        public int? Sosync_FS_ID { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }

        [DataMember(Name = "write_date")]
        public DateTime? Write_Date { get; set; }

        [DataMember(Name = "create_date")]
        public DateTime Create_Date { get; set; }
    }
}
