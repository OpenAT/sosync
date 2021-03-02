using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Models;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BatchController
        : ControllerBase
    {
        private ILogger<BatchController> _log;
        private MdbService _mdb;
        private OdooDataService _odb;

        public BatchController(ILogger<BatchController> logger, MdbService mdb, OdooDataService odb)
        {
            _log = logger;
            _mdb = mdb;
            _odb = odb;
        }

        [HttpPost("create")]
        public async Task<IActionResult> PostBatch(BatchRequest batch)
        {
            _log.LogInformation($"Received {batch.Identities.Count} IDs in batch request.");
            var data = await _mdb.GetAktionOnlineTokenAsync(batch.Identities.ToArray());
            return Ok();
        }
    }
}
