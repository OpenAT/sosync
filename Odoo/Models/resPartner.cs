﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Odoo.Models
{
    public class resPartner
    {
        [DataMember(Name = "firstname")]
        public string FirstName { get; set; }

        [DataMember(Name = "lastname")]
        public string LastName { get; set; }

        [DataMember(Name = "name_zwei")]
        public string Name_Zwei { get; set; }

        [DataMember(Name = "birthdate_web")]
        public DateTime? Birthdate_Web { get; set; }

        [DataMember(Name = "title_web")]
        public string Title_Web { get; set; }

        [DataMember(Name = "BPKForcedFirstname")]
        public string BPKForcedFirstname { get; set; }

        [DataMember(Name = "BPKForcedLastname")]
        public string BPKForcedLastname { get; set; }

        [DataMember(Name = "BPKForcedBirthdate")]
        public string BPKForcedBirthdate { get; set; }

        [DataMember(Name = "sosync_fs_id")]
        public int Sosync_FS_ID { get; set; }

        [DataMember(Name = "write_date")]
        public DateTime? Write_Date { get; set; }

        [DataMember(Name = "sosync_write_date")]
        public DateTime? Sosync_Write_Date { get; set; }

        [DataMember(Name = "street")]
        public string Street { get; set; }

        [DataMember(Name = "street_number_web")]
        public string StreetNumber { get; set; }

        [DataMember(Name = "zip")]
        public string Zip { get; set; }

        [DataMember(Name = "city")]
        public string City { get; set; }

        [DataMember(Name = "phone")]
        public string Phone { get; set; }

        [DataMember(Name = "email")]
        public string Email { get; set; }

        [DataMember(Name = "donation_deduction_optout_web")]
        public bool? DonationDeductionOptOut { get; set; }

        [DataMember(Name = "newsletter_web")]
        public bool EmailNewsletter { get; set; }
    }
}
