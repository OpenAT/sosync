using DaDi.Odoo.Models;
using dadi_data;
using dadi_data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using System;
using System.Diagnostics;
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
        : base(TimeSpan.FromMinutes(1), logger)
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

        MdbTokenDto[] tokens;
        do
        {
            tokens = await mdb.GetUnsynchronizedOnlineTokensAsync(_options.Token_Batch_Size);

            var s = Stopwatch.StartNew();
            _logger.LogInformation("Token batch, synchronizing {tokenCount} tokens.", tokens.Length);

            UpdateDefaults(tokens);
            await fso.StoreTokensAsync(tokens);
            await mdb.MarkTokensSynchronizedAsync(tokens);
            await Task.Delay(1000, stoppingToken);

            s.Stop();
            _logger.LogInformation("Token batch done in {milliseconds}ms.", s.ElapsedMilliseconds);

        } while (tokens.Length > 0 && !stoppingToken.IsCancellationRequested);
    }

    private void UpdateDefaults(MdbTokenDto[] tokens)
    {
        foreach (var token in tokens)
        {
            token.write_date = token.create_date;
            token.sosync_write_date = token.create_date;
            token.last_sync_version = token.create_date;
        }
    }
}
