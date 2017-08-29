using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    /// <summary>
    /// Holds all the information of a MSSQL dbo.Person model, including all
    /// sub models etc..
    /// </summary>
    public class PersonCombined
    {
        public dboPerson Person { get; set; }
        public dboPersonAdresse PersonAdresse { get; set; }
        public dboPersonEmail PersonEmail { get; set; }
        public dboPersonTelefon PersonTelefon {get; set; }

        public DateTime? WriteDateCombined { get; set; }
        public DateTime? SosyncWriteDateCombined { get; set; }
    }
}
