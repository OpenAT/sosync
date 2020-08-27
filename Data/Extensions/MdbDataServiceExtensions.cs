using dadi_data;
using dadi_data.Interfaces;
using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private static void PrepareMssqlzPersonIDs<TStudio>(DataService<TStudio> db, int[] onlineIDs, string tempTableName)
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
                whereClause = $"sosync_fso_id IN ({string.Join(", ", onlineIDs)})";
            }

            var count = db.ExecuteQuery<int>(
                $"SELECT PersonID INTO {tempTableName} FROM dbo.Person WHERE {whereClause}; " +
                $"SELECT COUNT(*) FROM {tempTableName};")
                .SingleOrDefault();

            if (count != onlineIDs.Length)
                throw new Exception($"Person mismatch for online ID list ({onlineIDs.Length} res.partner requested, {count} dbo.Person returned)");
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

        public static void MergeGrTagPersons<TStudio>(this DataService<TStudio> db, int studioID, int[] onlineIDs)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var tempTableName = $"[#sosync_tag_person_merge_{Guid.NewGuid()}]";

            PrepareMssqlzPersonIDs(db, onlineIDs, tempTableName);

            db.ExecuteNonQuery(
                Properties.Resources.MSSQL_Merge_GrTagPersons.Replace("#temp_table_name", tempTableName),
                new { gr_tagID = studioID });
        }
    }
}
