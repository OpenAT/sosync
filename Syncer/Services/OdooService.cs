using DaDi.Odoo;
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
