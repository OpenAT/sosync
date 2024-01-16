using dadi_data.Models;
using Npgsql;
using PostgreSQLCopyHelper;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;
using Dapper;
using System.Linq;

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

    public async Task StoreTokensAsync(MdbTokenDto[] tokens)
    {
        var tokenDict = tokens.ToDictionary(
            t => t.AktionsID,
            t => t);

        await _con.OpenAsync();

        await _con.ExecuteAsync(@"
            CREATE TEMPORARY TABLE IF NOT EXISTS temp_token (
                name varchar,
                partner_id int,
                expiration_date date,
                fs_origin varchar,
                last_datetime_of_use timestamp,
                first_datetime_of_use timestamp,
                number_of_checks int,
                sosync_write_date varchar,
                sosync_synced_version varchar,
                sosync_fs_id int,
                frst_write_date timestamp,
                frst_create_date timestamp,

                person_id int,
                aktions_id int
            );
            TRUNCATE TABLE temp_token;
            ");

        var bulk = new PostgreSQLCopyHelper<MdbTokenDto>("temp_token")
            .MapText("name", t => t.Name)
            .MapInteger("person_id", t => t.PersonID) // instead of partner_id, will be looked up in db via join
            .MapDate("expiration_date", t => t.Ablaufdatum)
            .MapText("fs_origin", t => t.FsOrigin)
            .MapText("sosync_write_date", t => GetTimeString(t.sosync_write_date ?? t.write_date))
            .MapText("sosync_synced_version", t => GetTimeString(t.last_sync_version))
            .MapInteger("sosync_fs_id", t => t.AktionsID)
            .MapTimeStamp("frst_write_date", t => Treat2000DateAsNull(t.write_date))
            .MapTimeStamp("frst_create_date", t => Treat2000DateAsNull(t.create_date))
            .MapInteger("aktions_id", t => t.AktionsID);

        await bulk.SaveAllAsync(_con, tokens);

        var insertedArray = (await _con.QueryAsync<TokenInsertedDto>(@$"

            UPDATE
                temp_token
            SET
                partner_id = p.id
            FROM
                res_partner p
            WHERE
                temp_token.person_id = p.sosync_fs_id;

            INSERT INTO res_partner_fstoken (
                name
                ,partner_id
                ,expiration_date
                ,fs_origin
                ,sosync_write_date
                ,sosync_synced_version
                ,sosync_fs_id
                ,frst_write_date
                ,frst_create_date
            )
            SELECT
                name
                ,partner_id
                ,expiration_date
                ,fs_origin
                ,sosync_write_date
                ,sosync_synced_version
                ,sosync_fs_id
                ,frst_write_date
                ,frst_create_date
            FROM
                temp_token
            WHERE
                temp_token.partner_id IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM res_partner_fstoken rpf
                    WHERE rpf.name = temp_token.name
                );

            SELECT
                pt.id {nameof(TokenInsertedDto.FsoID)}
                ,tt.aktions_id {nameof(TokenInsertedDto.AktionsID)}
            FROM
                temp_token tt
                LEFT JOIN res_partner_fstoken pt
                    on tt.sosync_fs_id = pt.sosync_fs_id;
            "))
            .ToArray();

        foreach (var inserted in insertedArray)
        {
            tokenDict[inserted.AktionsID].sosync_fso_id = inserted.FsoID;
        }

        await _con.CloseAsync();
    }

    private static readonly DateTime JanuaryFirst2000 = new DateTime(2000, 1, 1);
    private DateTime? Treat2000DateAsNull(DateTime? mssqlDate)
    {
        if (mssqlDate == JanuaryFirst2000)
            return null;

        return mssqlDate;
    }

    private string GetTimeString(DateTime? time)
    {
        if (time.HasValue)
        {
            var result = time.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            return result;
        }

        return null;
    }
}
