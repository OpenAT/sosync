using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class BatchRequest
    {
        [Required]
        public string Model { get; set; }
        
        [Required]
        public List<int> Identities { get; set; }
    }
}
