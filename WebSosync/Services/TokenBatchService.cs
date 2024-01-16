using dadi_data;
using dadi_data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Models;

namespace WebSosync.Services;

public class TokenBatchService
    : RepeatingBackgroundService
{
    private readonly ILogger<TokenBatchService> _logger;
    private readonly MdbService _mdbService;
    private readonly FsoDataServiceFactory _fsoDataFactory;
    private readonly SosyncOptions _options;

    public TokenBatchService(ILogger<TokenBatchService> logger, MdbService mdbService, FsoDataServiceFactory fsoDataFactory, SosyncOptions options)
#warning TODO: Use 1 Minute!
        : base(TimeSpan.FromSeconds(5), logger)
    {
        _logger = logger;
        _mdbService = mdbService;
        _fsoDataFactory = fsoDataFactory;
        _options = options;
    }

    protected override async Task WorkAsync(CancellationToken stoppingToken)
    {
        using var mdb = _mdbService.GetDataService<dboAktionOnlineToken>();
        using var fso = _fsoDataFactory.Create();

        /*
         Child job hierarchy: OnlineToken > Person > zVerzeichnis

         Before mapping data
            MSSQL: last_sync_version = sosync_write_date (if write_date is different)
            MSSQL: sosync_fso_id = OdooID (if fso_id is null)

            if (studioModel.last_sync_version != studioModel.sosync_write_date)
            {
                studioModel.last_sync_version = studioModel.sosync_write_date;
                db.Update(studioModel);
            }

         Mapping
          
            // Token specific fields
            online.Add("name", studio.Name);
            online.Add("partner_id", partner_id);
            online.Add("expiration_date", studio.Ablaufdatum);
            online.Add("fs_origin", studio.FsOrigin);
            online.Add("last_datetime_of_use", studio.LetzteBenutzungAmUm);
            online.Add("first_datetime_of_use", studio.ErsteBenutzungAmUm);
            online.Add("number_of_checks", studio.AnzahlÜberprüfungen);

            // General sosync fields (create + update)
            online.Add("sosync_write_date", (studioModel.sosync_write_date ?? studioModel.write_date));
            online.Add("frst_write_date", Treat2000DateAsNull(studioModel.write_date));
            online.Add("sosync_synced_version", studioModel.last_sync_version);
            
            // General sosync fields (create only)
            online.Add("frst_create_date", Treat2000DateAsNull(studioModel.create_date));
            online.Add("sosync_fs_id", getStudioIdentity(studioModel));

         After create:
        
            MSSQL: sosync_fso_id

         */

        var tokens = await mdb.GetUnsynchronizedOnlineTokensAsync(_options.Token_Batch_Size);
        var dummy = await fso.Connection.ExecuteScalarAsync<int>("select count(*) from res_partner;");
    }
}
