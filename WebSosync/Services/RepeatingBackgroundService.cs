using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebSosync.Services;

public abstract class RepeatingBackgroundService
    : BackgroundService
{
    private readonly PeriodicTimer _timer;
    private readonly ILogger _logger;

    public RepeatingBackgroundService(TimeSpan period, ILogger logger)
    {
        _timer = new(period);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await HandleWorkAssync(stoppingToken);

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await HandleWorkAssync(stoppingToken);
        }
    }

    private async Task HandleWorkAssync(CancellationToken stoppingToken)
    {
        try
        {
            await WorkAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repeating background work failed.");
        }
    }

    protected abstract Task WorkAsync(CancellationToken stoppingToken);
}
