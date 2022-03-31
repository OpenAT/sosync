using dadi_data;
using dadi_data.Models;
using Syncer.Exceptions;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class MdbService
        : MssqlServiceBase
    {
        #region Properties
        public string Instance { get { return Config.Instance; } }
        #endregion

        #region Constructors
        public MdbService(SosyncOptions config)
            : base(config)
        {
        }
        #endregion

        #region Methods
        public string GetIsoCodeForLandID(int? landID)
        {
            string countryCode = null;

            using (var dbSvc = GetDataService<dboTypen>())
            {
                countryCode = dbSvc.ExecuteQuery<string>(
                    "select sosync.LandID_to_IsoCountryCode2(@LandID)",
                    new { LandID = landID })
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(countryCode))
                return null;

            return countryCode;
        }

        public int? GetLandIDForIsoCode(string isoCode)
        {
            int? landID = null;

            using (var dbSvc = GetDataService<dboTypen>())
            {
                landID = dbSvc.ExecuteQuery<int?>(
                    "select sosync.IsoCountryCode2_to_LandID(@Code)",
                    new { Code = isoCode })
                    .FirstOrDefault();
            }

            if (landID == 0)
                return null;

            return landID;
        }

        public string GetStudioModelIdentity(string studioModelName)
        {
            if (studioModelName.ToLower().StartsWith("dbo.aktion")
                && (false == studioModelName.ToLower().EndsWith("detail")))
                return "AktionsID";

            return $"{studioModelName.Split('.')[1]}ID";
        }

        public string GetStudioModelReadView(string studioModelName)
        {
            return $"orm.[{studioModelName.Replace(".", "")}.read.view]";
        }

        public int? GetLandIDFromIsoCode(string isoCode)
        {
            using (var dbSvc = GetDataService<dboTypen>())
            {
                var foundLandID = dbSvc.ExecuteQuery<int?>(
                    "select sosync.IsoCountryCode2_to_LandID(@Code)",
                    new { Code = isoCode })
                    .FirstOrDefault();

                if (foundLandID.HasValue && foundLandID.Value != 0)
                    return foundLandID.Value;
            }

            return null;
        }
        #endregion
    }
}
