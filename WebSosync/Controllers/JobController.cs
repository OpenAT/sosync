using Microsoft.AspNetCore.Mvc;
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
        public JobController(SosyncOptions config)
        {
            _config = config;
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
