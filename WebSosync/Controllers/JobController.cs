using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Data.Models;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class JobController
    {
        #region Members
        private SosyncOptions _config;
        #endregion
        
        #region Constructors
        public JobController(IOptions<SosyncOptions> config)
        {
            _config = config.Value;
        }
        #endregion

        #region Methods
        [HttpPut()]
        public IActionResult Put()
        {
#warning TODO: Implement me
            return new BadRequestObjectResult("Not implemented yet!");
        }
        #endregion
    }
}
