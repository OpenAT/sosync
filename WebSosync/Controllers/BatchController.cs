using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        private MdbService _mdb;
        private OdooDataService _odb;

        public BatchController(MdbService mdb, OdooDataService odb)
        {
            _mdb = mdb;
            _odb = odb;
        }

        [HttpPost("create")]
        public async Task<IActionResult> PostBatch(BatchRequest batch)
        {
            await Task.Delay(1); // Dummy

            return Ok();
        }
    }
}
