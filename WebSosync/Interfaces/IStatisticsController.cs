using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Interfaces
{
    public interface IStatisticsController
    {
        Task<IActionResult> Queue();
        Task<IActionResult> Flows();
    }
}
