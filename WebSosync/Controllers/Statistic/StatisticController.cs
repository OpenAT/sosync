using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebSosync.Data;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebSosync.Controllers.Statistic
{
    [Route("statistic")]
    public class StatisticController : Controller
    {
        private DataService _db;

        public StatisticController(DataService db)
        {
            _db = db;
        }

        [HttpGet("jobs")]
        public async Task<IActionResult> Jobs()
        {
            var rows = (await _db.Connection
                .QueryAsync("select job_state, job_source_model, count(*) count from sosync_job where parent_job_id is null group by job_state, job_source_model order by job_state, job_source_model"))
                .ToArray();

            return new OkObjectResult(rows);
        }
    }
}
