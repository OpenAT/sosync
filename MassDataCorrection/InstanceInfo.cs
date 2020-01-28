using DaDi.Odoo;
using DaDi.Saltstack;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace MassDataCorrection
{
    public class InstanceInfo
    {
        public InstancePillar Pillar { get; private set; }

        public string Instance => Pillar.Name;

        public int Port => Pillar.Port;

        public InstanceInfo(string pillarFileName)
        {
            Pillar = new InstancePillar(pillarFileName);
        }

        public SqlConnection CreateOpenMssqlConnection()
        {
            var conStr = Pillar.GetMssqlSyncerConnectionString();
            var con = new SqlConnection(conStr);
            con.Open();
            return con;
        }

        public SqlConnection CreateOpenMssqlIntegratedConnection()
        {
            var conStr = Pillar.GetMssqlIntegratedConnectionString();
            var con = new SqlConnection(conStr);
            con.Open();
            return con;
        }

        public OdooClient CreateAuthenticatedOdooClient()
        {
            var address = Pillar.GetOdooRpcUrl();
            var client = new OdooClient(address, Pillar.Name);
            client.Authenticate("sosync", Pillar.OnlineSosyncPw);
            return client;
        }

        public NpgsqlConnection CreateOpenNpgsqlConnection()
        {
            var conStr = Pillar.GetNpgsqlConnectionString();
            var con = new NpgsqlConnection(conStr);
            con.Open();
            return con;
        }

        public NpgsqlConnection CreateOpenSyncerNpgsqlConnection()
        {
            var conStr = Pillar.GetSyncerNpgsqlConnectionString();
            var con = new NpgsqlConnection(conStr);
            con.Open();
            return con;
        }
    }
}
