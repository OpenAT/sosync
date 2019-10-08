using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    /// <summary>
    /// Wraps the <see cref="OdooClient"/> class in a service to be
    /// consumed by dependency injection.
    /// </summary>
    public class OdooService : IDisposable
    {
        #region Members
        [ThreadStatic]
        private static OdooClient _client;
        private ILogger _log;
        private SosyncOptions _options;
        #endregion

        #region Properties
        public OdooClient Client
        {
            get
            {
                if (_client == null)
                    _client = CreateClient();

                return _client;
            }
            set
            {
                _client = value;
            }
        }
        #endregion

        #region Constructors
        public OdooService(SosyncOptions options, ILogger<OdooService> logger)
        {
            _log = logger;
            _options = options;
        }

        private OdooClient CreateClient()
        {
            var client = new OdooClient($"https://{_options.Online_Host}/xmlrpc/2/", _options.Instance);
            client.Authenticate(_options.Online_Sosync_User, _options.Online_Sosync_PW);
            return client;
        }

        public int? GetCountryIDForIsoCode(string isoCode)
        {
            if (!string.IsNullOrEmpty(isoCode))
            {
                var foundCountryID = (int?)_client.SearchModelByField<resCountry, string>(
                    "res.country",
                    x => x.Code,
                    isoCode)
                    .FirstOrDefault();

                return foundCountryID != 0 ? foundCountryID : null;
            }

            return null;
        }

        public void Dispose()
        {
            if (_client != null)
                _client.Dispose();
        }
        #endregion

        #region Methods
        #endregion
    }
}
