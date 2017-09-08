using Daemaged.NTP;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Models;
using System;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class TimeService
    {
        #region Constructors
        public TimeService(ILogger<TimeService> logger, SosyncOptions config)
        {
            _log = logger;
            _config = config;

            _servers = new string[]
            {
                "at.pool.ntp.org",
                "ptbtime1.ptb.de",
                "bevtime1.metrologie.at",
                "0.europe.pool.ntp.org"
            };
        }
        #endregion

        #region Methods
        /// <summary>
        /// Get the time offset in milliseconds to the specified NTP server.
        /// </summary>
        /// <param name="server">The NTP server to check time against.</param>
        /// <returns>Time offset in milliseconds.</returns>
        private double GetOffset(string server)
        {
            var client = new Ntp(server)
            {
                VersionNumber = 3,
                Timeout = 500 // ms
            };

            return client.GetTime()
                .TimeOffset
                .ToTimeSpan()
                .TotalMilliseconds;
        }

        /// <summary>
        /// Returns the time offset in milliseconds to internet NTP servers, using an
        /// internal server priority list.
        /// </summary>
        /// <returns>The time offset in milliseconds.</returns>
        public double GetOffset()
        {
            var i = 0;
            double? result = null;
            while (!result.HasValue && i < _servers.Length)
            {
                try
                {
                    result = GetOffset(_servers[i]);
                }
                catch (Exception ex)
                {
                    _log.LogInformation($"Checking NTP {_servers[i]} failed: {ex.Message}");
                }
                i++;
            }

            if (!result.HasValue)
                throw new SyncerException($"Failed to reach any of the {_servers.Length} configured NTP servers.");

            return result.Value;
        }

        public TimeDrift GetTimeDrift()
        {
            var ntpOffset = (int)Math.Abs(GetOffset());
            var fsoOffset = (int)Math.Abs(GetOffset($"{_config.Instance}.datadialog.net"));
#warning Read FS Offset once it hosts an NTP server
            var fsOffset = 0;

            return new TimeDrift(ntpOffset, fsoOffset, fsOffset);
        }

        public void ThrowOnTimeDrift()
        {
            var drift = GetTimeDrift();
            _log.LogInformation($"Time drifts: NTP={drift.NTP}ms, FSO={drift.FSOnline}ms, FS={drift.FS}ms, tolerance={_config.Max_Time_Drift_ms}ms");

            if (drift.NTP > _config.Max_Time_Drift_ms)
                throw new TimeDriftException($"Time drift tolerance to NTP server exceeded (max: {_config.Max_Time_Drift_ms}ms, actual: {drift.NTP}ms).");

            if (drift.FSOnline > _config.Max_Time_Drift_ms)
                throw new TimeDriftException($"Time drift tolerance to FS-Online exceeded (max: {_config.Max_Time_Drift_ms}ms, actual: {drift.FSOnline}ms).");

            if (drift.FS > _config.Max_Time_Drift_ms)
                throw new TimeDriftException($"Time drift tolerance to FS exceeded (max: {_config.Max_Time_Drift_ms}ms, actual: {drift.FS}ms).");
        }
        #endregion

        #region Members
        private ILogger<TimeService> _log;
        private SosyncOptions _config;
        private string[] _servers;
        #endregion
    }
}
