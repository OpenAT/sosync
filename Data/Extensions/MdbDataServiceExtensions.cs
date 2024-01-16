using dadi_data;
using dadi_data.Interfaces;
using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSosync.Data.Models;

namespace WebSosync.Data.Extensions
{
    public static class MdbDataServiceExtensions
    {
        private static void PrepareMssqlzGruppeDetailIDs<TStudio>(DataService<TStudio> db, int[] onlineIDs, string tempTableName)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var whereClause = "";

            if (onlineIDs == null || onlineIDs.Length == 0)
            {
                onlineIDs = new int[] { };
                whereClause = "1 = 2"; // Ensure query returns no rows
            }
            else
            {
                whereClause = $"sosync_fso_id IN({string.Join(", ", onlineIDs)})";
            }

            var count = db.ExecuteQuery<int>(
                $"SELECT zGruppeDetailID INTO {tempTableName} FROM zGruppeDetail WHERE {whereClause}; " +
                $"SELECT COUNT(*) FROM {tempTableName};")
                .SingleOrDefault();

            if (count != onlineIDs.Length)
                throw new Exception($"zGruppeDetail mismatch for online ID list ({onlineIDs.Length} fson.zgruppedetail requested, {count} zGruppeDetail returned)");
        }


        public static void MergeSaleOrderGroups<TStudio>(this DataService<TStudio> db, int studioID, int[] onlineIDs)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var tempTableName = $"[#sosync_sol_zgd_merge_{Guid.NewGuid().ToString()}]";

            PrepareMssqlzGruppeDetailIDs(db, onlineIDs, tempTableName);

            db.ExecuteNonQuery(
                Properties.Resources.MSSQL_Merge_SaleOrderGroups.Replace("#temp_table_name", tempTableName),
                new { sale_order_lineID = studioID });
        }

        public static void MergeProductTemplateGroups<TStudio>(this DataService<TStudio> db, int studioID, int[] onlineIDs)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var tempTableName = $"[#sosync_pt_zgd_merge_{Guid.NewGuid().ToString()}]";

            PrepareMssqlzGruppeDetailIDs(db, onlineIDs, tempTableName);

            db.ExecuteNonQuery(
                Properties.Resources.MSSQL_Merge_ProductTemplateGroups.Replace("#temp_table_name", tempTableName),
                new { product_templateID = studioID });
        }

        public static void MergePersonGetResponseTags<TStudio>(this DataService<TStudio> db, int personID, int[] studioGrTagIds)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var query = Properties.Resources.MSSQL_Merge_PersonGrTags;

            if (studioGrTagIds.Length > 0)
            {
                query = query
                    .Replace("%TAGLIST%", string.Join(",", studioGrTagIds));
            }
            else
            {
                query = query
                    .Replace("%TAGLIST%", "NULL"); // SomeID IN (NULL) always results in zero rows
            }

            db.ExecuteNonQuery(
                query,
                new { PersonID = personID });
        }

        public static void MergeProductAttributeValuesProductProductRel<TStudio>(this DataService<TStudio> db, int productProductID, int[] productAttributeValueIDs)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var query = Properties.Resources.MSSQL_Merge_ProductAttributeValueProductProductRel;

            if (productAttributeValueIDs.Length > 0)
            {
                query = query
                    .Replace("%VALUE-ID-LIST%", string.Join(",", productAttributeValueIDs));
            }
            else
            {
                query = query
                    .Replace("%VALUE-ID-LIST%", "NULL"); // SomeID IN (NULL) always results in zero rows
            }

            db.ExecuteNonQuery(
                query,
                new { product_productID = productProductID });
        }

        public static async Task<MdbTokenDto[]> GetUnsynchronizedOnlineTokensAsync<TStudio>(this DataService<TStudio> db, int topCount)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var data = await db.ExecuteQueryAsync<MdbTokenDto>(
                @"EXEC ui.stp_sosync2_GetTokensForBatchSync2Fso @batchSize = @topCount",
                new { topCount });

            return data.ToArray();
        }

        public static async Task MarkTokensSynchronizedAsync<TStudio>(this DataService<TStudio> db, MdbTokenDto[] tokens)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            const string tempTokenTableName = "[#token]";
            const string tempPersonResyncTableName = "[#personResync]";

            await db.ExecuteNonQueryAsync(@$"
                DROP TABLE IF EXISTS {tempTokenTableName};
                CREATE TABLE {tempTokenTableName} (
                    AktionsID int not null primary key
                    ,sosync_fso_id int null
                    ,sosync_write_date datetime2
                    ,last_sync_version datetime2
                );");

            using var bulk = new SqlBulkCopy(db.Connection);
            bulk.DestinationTableName = tempTokenTableName;

            var table = new DataTable(tempTokenTableName);
            string dummy = table.TableName;
            table.Columns.Add("AktionsID", typeof(int));
            table.Columns.Add("sosync_fso_id", typeof(int));
            table.Columns.Add("sosync_write_date", typeof(DateTime));
            table.Columns.Add("last_sync_version", typeof(DateTime));

            foreach (var token in tokens)
                table.Rows.Add(token.AktionsID, token.sosync_fso_id, token.sosync_write_date, token.last_sync_version);

            await bulk.WriteToServerAsync(table);

            await db.ExecuteNonQueryAsync(@$"
                set transaction isolation level read uncommitted;

                UPDATE
	                t
                SET
	                t.sosync_fso_id = s.sosync_fso_id
	                ,t.sosync_write_date = s.sosync_write_date
	                ,t.last_sync_version = s.last_sync_version
                FROM
	                {tempTokenTableName} s
	                INNER JOIN dbo.AktionOnlineToken t
		                ON t.AktionsID = s.AktionsID
                WHERE
                    s.sosync_fso_id IS NOT NULL
                    AND (
	                    not exists(select t.sosync_fso_id intersect select s.sosync_fso_id)
	                    or not exists(select t.sosync_write_date intersect select s.sosync_write_date)
	                    or not exists(select t.last_sync_version intersect select s.last_sync_version)
                    );

                DROP TABLE IF EXISTS {tempPersonResyncTableName};

                CREATE TABLE {tempPersonResyncTableName} (
                    PersonID int not null primary key
                )

                INSERT INTO {tempPersonResyncTableName} (PersonID)
                SELECT DISTINCT p.PersonID
                FROM
	                {tempTokenTableName} s
	                INNER JOIN dbo.AktionOnlineToken t
		                ON t.AktionsID = s.AktionsID
                    INNER JOIN dbo.Aktion a
                        ON t.AktionsID = a.AktionsID
                    INNER JOIN dbo.Person p
                        ON a.PersonID = p.PersonID
                WHERE
                    AktionstypID = 2005881 -- AktionOnlineToken
                    AND s.sosync_fso_id IS NULL;

                UPDATE
                    p
                SET
                    p.sosync_fso_id = NULL
                    ,p.sosync_write_date = ISNULL(p.sosync_write_date, SYSUTCDATETIME())
                    ,p.last_sync_version = ISNULL(p.sosync_write_date, SYSUTCDATETIME())
                FROM
                    {tempPersonResyncTableName} tpr
                    INNER JOIN dbo.Person p
                        ON tpr.PersonID = p.PersonID;

                insert into sosync.JobQueue (
                    JobDate
			        ,JobSourceSystem
			        ,JobSourceModel
			        ,JobSourceRecordID
			        ,JobState
			        ,JobSourceSosyncWriteDate
			        ,JobSourceFields
			        ,JobSourceType
			        ,JobPriority
			        ,spid
			        ,source_ID
			        ,ParentJobQueueID
                    )
                select
                    p.sosync_write_date -- JobDate
			        ,'fs' -- JobSourceSystem
			        ,'dbo.Person' -- JobSourceModel
			        ,p.PersonID -- JobSourceRecordID
			        ,'new' -- JobState
			        ,p.sosync_write_date -- JobSourceSosyncWriteDate
			        ,'{"{\"info\": \"Job by soync2 token batch: person resync\"}"}' -- JobSourceFields
                    , null -- JobSourceType
			        ,null -- JobPriority
			        ,@@spid -- spid
			        ,p.PersonID -- source_ID
			        ,null -- ParentJobQueueID
                FROM
                    {tempPersonResyncTableName} tpr
                    INNER JOIN dbo.Person p
                        ON tpr.PersonID = p.PersonID;
                ");
        }
    }
}
