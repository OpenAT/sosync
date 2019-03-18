using dadi_data.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Models;

namespace WebSosync.Services
{
    public class StatisticService
    {
        private MdbService _mdb;

        public StatisticService(MdbService mdb)
        {
            _mdb = mdb;
        }

        public async Task<QueueStatistic> GetMssqlQueueStatisticAsync()
        {
            using (var db = _mdb.GetDataService<dboTypen>())
            {
                return (await db.ExecuteQueryAsync<QueueStatistic>(@"
                SELECT
	                (SELECT COUNT(*) FROM sosync.JobQueue WITH (READPAST)) TotalJobs
	                ,(SELECT COUNT(*) FROM sosync.JobQueue WITH (READPAST) WHERE Submission IS NOT NULL) SubmittedJobs
                    "))
                    .Single();
            }
        }

        public async Task<Dictionary<string, int>> GetMssqlModelStatisticsAsync(IEnumerable<string> modelNames)
        {
            var query = string.Join("UNION ALL\n", modelNames
                .Select(name => $"SELECT '{name}' Model, COUNT(*) NotSychnronized FROM {name} WITH (NOLOCK) WHERE sosync_fso_id IS NULL\n"));

            var result = new Dictionary<string, int>();

            using (var db = _mdb.GetDataService<dboTypen>())
            {
                var rows = (await db.ExecuteQueryAsync<dynamic>(query))
                    .Select(x => new Tuple<string, int>(x.Model, x.NotSychnronized));

                foreach (var row in rows)
                    result.Add(row.Item1, row.Item2);
            }

            return result;
        }
    }
}
