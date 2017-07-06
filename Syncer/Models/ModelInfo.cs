using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Models
{
    public class ModelInfo
    {
        #region Properties
        /// <summary>
        /// The referenced ID for the model in the foreign system.
        /// </summary>
        public int? ForeignID { get; set; }

        /// <summary>
        /// The last write date for the model.
        /// </summary>
        public DateTime? WriteDate { get; set; }
        #endregion

        #region Constructor
        public ModelInfo(int? foreignID, DateTime? writeDate)
        {
            ForeignID = foreignID;
            WriteDate = writeDate;
        }
        #endregion
    }
}
