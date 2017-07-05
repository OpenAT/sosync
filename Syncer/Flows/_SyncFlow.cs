﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using System;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for any sync flow.
    /// </summary>
    public abstract class SyncFlow
    {
        #region Members
        private IServiceProvider _svc;
        private OdooService _odoo;
        private ILogger<SyncFlow> _log;
        #endregion

        #region Properties
        protected ILogger<SyncFlow> Log
        {
            get { return _log; }
        }

        protected IServiceProvider Service
        {
            get { return _svc; }
        }

        protected OdooService Odoo
        {
            get { return _odoo; }
        }
        #endregion

        #region Constructors
        public SyncFlow(IServiceProvider svc)
        {
            _svc = svc;
            _log = _svc.GetService<ILogger<SyncFlow>>();
            _odoo = _svc.GetService<OdooService>();
        }
        #endregion

        #region Methods

        #endregion
    }
}
