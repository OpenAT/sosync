﻿using DaDi.Odoo;
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
        public string Instance;
        public int Port;

        public string host_sosync { get; set; }
        public string online_sosync_pw { get; set; }
        public string online_pgsql_pw { get; set; }
        public string studio_sosync_pw { get; set; }
        public string sosync_pgsql_pw { get; set; }
        public string mssql_branch { get; set; }
        public string sosync_branch { get; set; }

        public InstanceInfo()
        {
        }

        public SqlConnection CreateOpenMssqlConnection()
        {
            var conStr = $"Data Source=mssql.{Instance}.datadialog.net; Initial Catalog=mdb_{Instance}; User ID={Instance}_sosync; Password={studio_sosync_pw}";
            var con = new SqlConnection(conStr);
            con.Open();
            return con;
        }

        public SqlConnection CreateOpenMssqlIntegratedConnection()
        {
            var conStr = $"Data Source=mssql.{Instance}.datadialog.net; Initial Catalog=mdb_{Instance}; Integrated Security=True;";
            var con = new SqlConnection(conStr);
            con.Open();
            return con;
        }

        public OdooClient CreateAuthenticatedOdooClient()
        {
            var address = $"https://{Instance}.datadialog.net/xmlrpc/2/";
            var client = new OdooClient(address, Instance);
            client.Authenticate("sosync", online_sosync_pw);
            return client;
        }

        public NpgsqlConnection CreateOpenNpgsqlConnection()
        {
            var conStr = $"User ID={Instance}; Password={online_pgsql_pw}; Host=pgsql.{Instance}.datadialog.net; Port=5432; Database={Instance}; Pooling=true;";
            var con = new NpgsqlConnection(conStr);
            con.Open();
            return con;
        }

        public NpgsqlConnection CreateOpenSyncerNpgsqlConnection()
        {
            var sosync2File = File.ReadAllText( @"C:\WorkingFolder\saltstack\pillar\hosts\sosync2.sls");
            var pass = sosync2File.Substring(sosync2File.IndexOf("sosync_gui_db_password:") + 24, 20).Trim();

            var conStr = $"User ID=sosync_gui; Password={pass}; Host=sosync2.datadialog.net; Port=5432; Database={Instance}_sosync_gui; Pooling=true;";
            var con = new NpgsqlConnection(conStr);
            con.Open();
            return con;
        }
    }
}
