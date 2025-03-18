using Microsoft.Extensions.DependencyInjection;
using System;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;

namespace WebSosync.Data;

public class FsoDataServiceFactory
{
    private readonly IServiceProvider _services;
    private readonly SosyncOptions _config;

    public FsoDataServiceFactory(IServiceProvider services, SosyncOptions options)
    {
        _services = services;
        _config = options;
    }

    public FsoDataService Create()
    {
        var conStr = ConnectionHelper.GetPostgresConnectionString(
                _config.Online_DB_Host,
                _config.Online_DB_Port,
                _config.Online_DB_Name,
                _config.Online_DB_User,
                _config.Online_DB_User_PW);

        return ActivatorUtilities.CreateInstance<FsoDataService>(_services, conStr);
    }
}
