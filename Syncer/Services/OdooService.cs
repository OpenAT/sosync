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
    public class OdooService
    {
        #region Members
        private OdooClient _client;
        private ILogger _log;
        #endregion

        #region Properties
        public OdooClient Client
        {
            get { return _client; }
        }
        #endregion

        #region Constructors
        public OdooService(SosyncOptions options, ILogger<OdooService> logger)
        {
            _log = logger;

            _client = new OdooClient($"http://{options.Online_Host}/xmlrpc/2/", options.Instance);
            _client.Authenticate(options.Online_Sosync_User, options.Online_Sosync_PW);
        }
        #endregion

        #region Methods
        #endregion
    }
}
