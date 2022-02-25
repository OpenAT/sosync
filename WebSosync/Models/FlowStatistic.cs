using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class FlowStatistic
    {
        public FlowStatistic()
        {
            UnsynchronizedModelsCount = new Dictionary<string, int>();
        }

        [JsonPropertyName("unsynchronizedModelsCount")]
        public Dictionary<string, int> UnsynchronizedModelsCount { get; set; }
    }
}
