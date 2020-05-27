﻿using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    /// <summary>
    /// Holds all the information of a MSSQL dbo.Person model, including all
    /// sub models etc..
    /// </summary>
    public class dboPersonStack
    {
        public dboPerson person { get; set; }
        public dboPersonAdresse address { get; set; }
        public dboPersonAdresseAM addressAM { get; set; }
        public dboPersonAdresseBlock addressBlock { get; set; }
        public dboPersonTelefon phone { get; set; }
        public dboPersonTelefon mobile { get; set; }
        public dboPersonTelefon fax { get; set; }
        public DateTime? sosync_write_date { get; set; }
        public DateTime? write_date { get; set; }
        public DateTime? create_date { get; set; }
    }
}
