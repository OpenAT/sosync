using dadi_data;
using dadi_data.Models;
using Syncer.Exceptions;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (studioModelName.ToLower().StartsWith("dbo.aktion"))
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

        public async Task<dynamic> GetAktionOnlineTokenAsync(int[] ids)
        {
            var query = @"
                SELECT TOP 300000
	                t.AktionsID
	                -- Mapping as in sosync 2 -----
	                ,t.Name
	                ,p.sosync_fso_id partner_id
	                ,t.Ablaufdatum expiration_date
	                ,t.FsOrigin fs_origin
	                ,t.LetzteBenutzungAmUm last_datetime_of_use
	                ,t.ErsteBenutzungAmUm first_datetime_of_use
	                ,t.AnzahlÜberprüfungen number_of_checks
	                -------------------------------
	                ,t.sosync_fso_id
	                ,t.sosync_write_date
	                ,t.last_sync_version
                FROM
	                dbo.Aktion a
	                INNER JOIN dbo.AktionOnlineToken t
		                ON a.AktionsID = t.AktionsID
		                AND a.AktionstypID = 2005881
	                INNER JOIN dbo.Person p
		                ON a.PersonID = p.PersonID
                "; // +
                // $" WHERE t.AktionsID IN ({ string.Join(",", ids)});";

            using (var db = GetDataService<dboTypen>())
            {
                return await db.ExecuteQueryAsync<dynamic>(query);
            }
        }
        #endregion
    }
}
