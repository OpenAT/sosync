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

namespace WebSosync.Services;

public class TokenBatchService
    : RepeatingBackgroundService
{
    private readonly ILogger<TokenBatchService> _logger;
    private readonly MdbService _mdbService;
    private readonly FsoDataServiceFactory _fsoDataFactory;

    public TokenBatchService(ILogger<TokenBatchService> logger, MdbService mdbService, FsoDataServiceFactory fsoDataFactory)
#warning TODO: Use 1 Minute!
        : base(TimeSpan.FromSeconds(5), logger)
    {
        _logger = logger;
        _mdbService = mdbService;
        _fsoDataFactory = fsoDataFactory;
    }

    protected override async Task WorkAsync(CancellationToken stoppingToken)
    {
        using var mdb = _mdbService.GetDataService<dboAktionOnlineToken>();
        using var fso = _fsoDataFactory.Create();

        var tokens = await mdb.GetUnsynchronizedOnlineTokensAsync();
        var dummy = await fso.Connection.ExecuteScalarAsync<int>("select count(*) from res_partner;");
    }
}
