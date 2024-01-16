using Npgsql;
using System;
using System.Data.Common;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;

namespace WebSosync.Data;

public class FsoDataService
    : IDisposable
{
    private readonly NpgsqlConnection _con;

    public DbConnection Connection
    {
        get { return _con; }
    }

    public FsoDataService(string conStr)
    {
        _con = new NpgsqlConnection(conStr);
    }

    public void Dispose()
    {
        _con.Dispose();
    }
}
