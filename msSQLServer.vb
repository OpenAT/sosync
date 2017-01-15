Public Class msSQLServer

    Private _instance As String
    Private _pw As String

    Public Sub New(instance As String, pw As String)
        Me._instance = instance
        Me._pw = pw
    End Sub


    Public Function sync_record_exists(table_name As String, odoo_id As Integer, odoo_id2 As Integer, direction As Boolean, operation As String) As Boolean

        Dim ret As Boolean = False

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))

            Dim question As String = String.Format(
            "select 
                 case when count(*) > 0 then cast(1 as bit) else cast(0 as bit) end result 
             from odoo.sync_table t 
             where t.Tabelle = '{0}'
               and t.odoo_id = {1}
               and t.odoo_id2 = {2}
               and t.Direction = {3}
               and t.Operation = '{4}'",
            table_name,
            odoo_id,
            odoo_id2,
            If(direction, "1", "0"),
            operation)

            ret = If(db.ExecuteQuery(Of Boolean?)(question).FirstOrDefault(), False)

        End Using

        Return ret

    End Function

    Public Function get_Schema() As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim res As New Dictionary(Of String, Dictionary(Of String, List(Of String)))


        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))

            Dim schema = (From el In db.ft_sosyncSchema_get() Select el Order By el.TableName, el.FieldName).ToList()

            For Each item In schema
                If Not res.ContainsKey(item.TableName) Then
                    res.Add(item.TableName, New Dictionary(Of String, List(Of String)))

                    res(item.TableName).Add("online_model_name", New List(Of String))
                    res(item.TableName)("online_model_name").Add(item.online_model_name)

                    res(item.TableName).Add("online_model_rel_ids", New List(Of String))
                    res(item.TableName).Add("online_model_rel_fields", New List(Of String))

                    res(item.TableName).Add("id_fields", New List(Of String))
                    res(item.TableName).Add("fields", New List(Of String))


                End If

                If item.TableName.EndsWith("_rel") Then
                    res(item.TableName)("online_model_rel_ids").Add(item.FieldName)
                    res(item.TableName)("online_model_rel_fields").Add(item.online_model_rel_field_name)
                End If

                If item.FieldName = "id" OrElse item.TableName.EndsWith("_rel") Then
                    res(item.TableName)("id_fields").Add(item.FieldName)
                End If

                If item.FieldName <> "id" Then
                    res(item.TableName)("fields").Add(item.FieldName)
                End If

            Next

        End Using

        Return res

    End Function


    Public Sub save_new_odoo_id(record As sync_table_record, new_odoo_id As Integer)


        Dim command As String = ""

        Try

            Using s As New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)


                Dim db As New mdbDataContext(get_connection_string(Me._instance))

                command =
                    String.Format("update odoo.{0} set id = {1}, TriggerFlag = 0 where {0}ID = {2}", record.Tabelle, new_odoo_id, record.ID)

                db.ExecuteCommand(command)

                command = String.Format("update odoo.sync_table set odoo_id = {0} where Tabelle = '{1}' and ID = {2}", new_odoo_id, record.Tabelle, record.ID)

                db.ExecuteCommand(command)

            End Using


        Catch ex As Exception

            log.write_line(String.Format("error saving new odoo_id to mssql server({2}). error details:{1}{0}{1}{1}command:{1}{3}", ex.ToString(), Environment.NewLine, get_connection_string(Me._instance), command), log.Level.Error)

        End Try

    End Sub

    Public Function try_connect() As Boolean

        Dim conn As New SqlClient.SqlConnection(get_connection_string(Me._instance))

        Try
            Using s As New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)
                conn.Open()
                conn.Close()
            End Using

            Return True

        Catch ex As Exception

            log.write_line(String.Format("error connecting to mssql server({2}). error details:{1}{0}", ex.ToString(), Environment.NewLine, conn.ConnectionString), log.Level.Error)

            Return False

        End Try

    End Function


    Private Shared Function get_connection_string(instance As String) As String

        Dim db_name = String.Format(db_name_prototype, instance)

        Return String.Format(connection_string_prototype, instance, db_name)

    End Function

    Public Function request_ID(schema As Dictionary(Of String, Dictionary(Of String, List(Of String))), table_name As String, odoo_id As Integer, odoo_id2 As Integer?) As Integer

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))

            Dim pk_fields = schema(table_name)("id_fields")

            Dim where_clause As String = ""

            If pk_fields.Count = 1 Then
                where_clause = String.Format("where {0} = '{1}'", pk_fields(0), odoo_id)
            ElseIf pk_fields.Count = 2 Then
                where_clause = String.Format("where {0} = '{1}' and {2} = '{3}'", pk_fields(0), odoo_id, pk_fields(1), odoo_id2.Value)
            End If

            Dim command As String = String.Format("select top 1 isnull({0}ID, 0) from odoo.{0} {1}", table_name, where_clause)

            Return db.ExecuteQuery(Of Integer)(command).FirstOrDefault()

        End Using

    End Function

    Public Function insert_sync_record(record As Dictionary(Of String, Object)) As Boolean

        Dim command_sql As String = "insert into odoo.sync_table (Direction, Tabelle, Operation, ID, odoo_id, odoo_id2, Anlagedatum) values (@direction, @table_name, @operation, @id, @odoo_id, @odoo_id2, @creation) set @sync_tableID = scope_identity()"
        Try

            Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

                Dim db As New mdbDataContext(get_connection_string(Me._instance))

                Dim command As New SqlClient.SqlCommand(command_sql, db.Connection())

                For Each param In record
                    command.Parameters.AddWithValue(String.Format("@{0}", param.Key), param.Value)
                Next

                Dim new_id = command.Parameters.Add("@sync_tableID", SqlDbType.Int)

                new_id.Direction = ParameterDirection.Output

                db.Connection.Open()
                command.ExecuteNonQuery()
                db.Connection.Close()

                record.Add("sync_tableID", new_id.Value)

            End Using

        Catch ex As Exception

            log.write_line(String.Format("error inserting sync_table_record. error details:{1}{0}", ex.ToString(), Environment.NewLine), log.Level.Error)

            Return False

        End Try

        Return True

    End Function

    Public Function get_sync_work() As List(Of sync_table_record)

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))
            Dim command As String = "select * from odoo.sync_table where SyncStart is null order by Anlagedatum"

            Return db.ExecuteQuery(Of sync_table_record)(command).ToList()

        End Using

    End Function

    Public Sub save_sync_table_record(record As sync_table_record)

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))

            Dim command As String =
"UPDATE [odoo].[sync_table]
   SET [SyncStart] = @SyncStart
      ,[SyncEnde] = @SyncEnde
      ,[SyncResult] = @SyncResult
      ,[SyncMessage] = @SyncMessage
 WHERE sync_tableID = @sync_tableID"

            Dim cmd As New SqlClient.SqlCommand(command, db.Connection)

            cmd.Parameters.AddWithValue("@SyncStart", record.SyncStart)
            cmd.Parameters.AddWithValue("@SyncEnde", record.SyncEnde)
            cmd.Parameters.AddWithValue("@SyncResult", record.SyncResult)
            cmd.Parameters.AddWithValue("@SyncMessage", If(record.SyncMessage, DBNull.Value))
            cmd.Parameters.AddWithValue("@sync_tableID", record.sync_tableID)

            db.Connection.Open()

            cmd.ExecuteNonQuery()

            db.Connection.Close()

        End Using

    End Sub


    Public Sub work_insert(insert As sync_table_record, api As odooXMLRPCWrapper, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))))

        insert.SyncStart = Now

        Try
            Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

                Dim db As New mdbDataContext(get_connection_string(Me._instance))
                'using etc.
                Dim data = api.get_data(insert, schema)

                If data.Count = 0 Then
                    insert.SyncEnde = Now
                    insert.SyncResult = True
                    insert.SyncMessage = "no data available at insert - the record may be already deleted in odoo"
                    Me.save_sync_table_record(insert)
                    Return
                End If

                If insert.Tabelle.ToLower().EndsWith("_rel") Then
                    Dim rel_record_exists_script As String = String.Format(
"
                    if exists(select
                                *
                                from odoo.{0}
                                where {1} = {2}
                                  and {3} = {4})
                        begin
                            select cast(1 as bit)
                        end
                    else
                        begin
                            select cast(0 as bit)
                        end",
                            insert.Tabelle,
                            data.Keys(0),
                            String.Format("@{0}", data.Keys(0)),
                            data.Keys(1),
                            String.Format("@{0}", data.Keys(1)))


                    Dim rel_record_exists_cmd As New SqlClient.SqlCommand(rel_record_exists_script, db.Connection)

                    For Each item In data
                        Dim param = New SqlClient.SqlParameter(String.Format("@{0}", item.Key), item.Value)
                        rel_record_exists_cmd.Parameters.Add(param)
                    Next

                    db.Connection.Open()
                    Dim rel_rec_exists As Boolean = rel_record_exists_cmd.ExecuteScalar()
                    db.Connection.Close()

                    If rel_rec_exists Then
                        insert.SyncEnde = Now
                        insert.SyncResult = True
                        insert.SyncMessage = "rel_record already in db"
                        Me.save_sync_table_record(insert)
                        Return
                    End If

                End If

                Dim command As String = String.Format(
    "           insert into odoo.{0}({1}, TriggerFlag) 
                values ({2}, 0) 
                declare @newID int = scope_identity() 
                if exists(select * 
                          from sys.objects o 
                          inner join sys.schemas s 
                              on o.schema_id = s.schema_id 
                          where s.name = 'odoo' 
                          and o.name = 'stp_{0}_inserted' 
                          and o.type = 'P')
                    begin
                        exec odoo.stp_{0}_inserted @newID
                    end",
                          insert.Tabelle,
                          String.Join(", ", data.Keys.ToArray()),
                         String.Join(", ", (From el In data.Keys Select String.Format("@{0}", el)).ToArray())
    )

                Dim cmd As New SqlClient.SqlCommand(command, db.Connection)


                For Each item In data
                    Dim param = New SqlClient.SqlParameter(String.Format("@{0}", item.Key), item.Value)
                    cmd.Parameters.Add(param)
                Next

                db.Connection.Open()
                cmd.ExecuteNonQuery()
                db.Connection.Close()

            End Using

        Catch ex As Exception

            insert.SyncEnde = Now
            insert.SyncResult = False
            insert.SyncMessage = ex.ToString()

            Me.save_sync_table_record(insert)

            Return

        End Try

        insert.SyncEnde = Now
        insert.SyncResult = True
        Me.save_sync_table_record(insert)

    End Sub
    Public Sub work_update(update As sync_table_record, api As odooXMLRPCWrapper, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))))

        update.SyncStart = Now

        If update.Tabelle.EndsWith("_rel") Then
            update.SyncEnde = Now
            update.SyncResult = False
            update.SyncMessage = "_rel-records cannot be updated!"
            Me.save_sync_table_record(update)
            Return
        End If

        Try
            Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

                Dim db As New mdbDataContext(get_connection_string(Me._instance))
                'using etc.
                Dim data = api.get_data(update, schema)

                If data.Count = 0 Then
                    update.SyncEnde = Now
                    update.SyncResult = True
                    update.SyncMessage = "no data available at update - the record may be already deleted in odoo"
                    Me.save_sync_table_record(update)
                    Return
                End If


                Dim command As String = String.Format(
    "           update odoo.{0}
                set %columns%, TriggerFlag = 0
                where {1} = {2}
                if exists(select * 
                          from sys.objects o 
                          inner join sys.schemas s 
                              on o.schema_id = s.schema_id 
                          where s.name = 'odoo' 
                          and o.name = 'stp_{0}_updated' 
                          and o.type = 'P')
                    begin
                        declare @{0}ID int = (select top 1 {0}ID from odoo.{0} where {1} = {2})
                        exec odoo.stp_{0}_updated @{0}ID
                    end",
                        update.Tabelle,
                        schema(update.Tabelle)("id_fields")(0),
                        update.odoo_id)

                Dim columns As New List(Of String)

                For Each field In schema(update.Tabelle)("fields")
                    columns.Add(String.Format("{1} = @{1}{0}", Environment.NewLine, field))
                Next

                command = command.Replace("%columns%", String.Join(", ", columns.ToArray()))

                Dim cmd As New SqlClient.SqlCommand(command, db.Connection)


                For Each item In data
                    Dim param = New SqlClient.SqlParameter(String.Format("@{0}", item.Key), item.Value)
                    cmd.Parameters.Add(param)
                Next
                db.Connection.Open()
                cmd.ExecuteNonQuery()
                db.Connection.Close()

            End Using

        Catch ex As Exception

            update.SyncEnde = Now
            update.SyncResult = False
            update.SyncMessage = ex.ToString()

            Me.save_sync_table_record(update)

            Return

        End Try

        update.SyncEnde = Now
        update.SyncResult = True
        Me.save_sync_table_record(update)

    End Sub

    Public Sub work_delete(delete As sync_table_record, api As odooXMLRPCWrapper, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))))

        delete.SyncStart = Now

        Try
            Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

                Dim db As New mdbDataContext(get_connection_string(Me._instance))

                Dim command As String = String.Format(
    "    
                if exists(select * 
                          from sys.objects o 
                          inner join sys.schemas s 
                              on o.schema_id = s.schema_id 
                          where s.name = 'odoo' 
                          and o.name = 'stp_{0}_deleting' 
                          and o.type = 'P')
                    begin
                        declare @{0}ID int = (select top 1 {0}ID from odoo.{0} where {1} = {2} {3})
                        exec odoo.stp_{0}_deleting @{0}ID
                    end
                update odoo.{0}
                set TriggerFlagDelete = 0
                where {1} = {2} {3}
                delete from odoo.{0}
                where {1} = {2} {3}
    
    ",
                        delete.Tabelle,
                        schema(delete.Tabelle)("id_fields")(0),
                        delete.odoo_id,
                        If(schema(delete.Tabelle)("id_fields").Count > 1, String.Format("and {0} = {1}", schema(delete.Tabelle)("id_fields")(1), delete.odoo_id2.Value), ""))

                Dim cmd As New SqlClient.SqlCommand(command, db.Connection)

                db.Connection.Open()
                cmd.ExecuteNonQuery()
                db.Connection.Close()

            End Using

        Catch ex As Exception

            delete.SyncEnde = Now
            delete.SyncResult = False
            delete.SyncMessage = ex.ToString()

            Me.save_sync_table_record(delete)

            Return

        End Try

        delete.SyncEnde = Now
        delete.SyncResult = True
        Me.save_sync_table_record(delete)

    End Sub

    Public Function get_data(item As sync_table_record, schema As Dictionary(Of String, Dictionary(Of String, List(Of String)))) As Dictionary(Of String, Object)

        Dim data As New Dictionary(Of String, Object)

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext(get_connection_string(Me._instance))

            Dim command As String = String.Format("select {0} from odoo.{1} ", String.Join(", ", schema(item.Tabelle)("fields").ToArray()), item.Tabelle)

            Dim where_clause As String = String.Format("where {1}ID = {0}", item.ID, item.Tabelle)
            Dim cmd As New SqlClient.SqlCommand(command & where_clause, db.Connection)

            db.Connection.Open()
            Dim r = cmd.ExecuteReader()


            If r.Read() Then

                For i As Integer = 0 To r.FieldCount - 1
                    data.Add(r.GetName(i), r(i))
                Next

            End If

            r.Close()

            db.Connection.Close()


        End Using

        Return data

    End Function


    Private Const connection_string_prototype As String = "Data Source=mssql.{0}.datadialog.net;Initial Catalog=MDB_{0};Integrated Security=True"
    '    Private Const connection_string_prototype As String = "Data Source=mssql.{0}.datadialog.net;Initial Catalog={1};Integrated Security=True"

    Private Const db_name_prototype As String = "MDB_{0}"

    Private Const fallback_connection_string_cname_dns_source_prototype As String = "mdb.mssql.{0}.datadialog.net"

    Private Const dadi_dns_server_name As String = "dadidc01.datadialog.net"

    Private Const dadi_upn_prototype As String = "{0}@datadialog.net"

    Private Const char_dot As String = "."
End Class

Public Class sync_table_record
    Public Property sync_tableID As Integer
    Public Property Direction As Boolean
    Public Property Tabelle As String
    Public Property Operation As String
    Public Property ID As Integer
    Public Property odoo_id As Integer?
    Public Property odoo_id2 As Integer?
    Public Property Anlagedatum As DateTime?
    Public Property SyncStart As DateTime?
    Public Property SyncEnde As DateTime?
    Public Property SyncResult As Boolean?
    Public Property SyncMessage As String
End Class

