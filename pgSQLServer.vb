Imports Npgsql
Public Class pgSQLServer

    Private _instance As String
    Private _password As String

    Public Sub New(instance As String, password As String)

        Me._instance = instance
        Me._password = password

        initialize_connection()

    End Sub

    Private _connection As NpgsqlConnection

    Private Sub initialize_connection()

        Me._connection = New NpgsqlConnection(New NpgsqlConnectionStringBuilder(pgSQLServer.get_connection_string(Me._instance, Me._password)))

    End Sub

    Public Function try_connect() As Boolean

        Try
            Me._connection.Open()
            Me._connection.Close()
            Return True
        Catch ex As Exception

            log.write_line(String.Format("error connecting to mssql server({2}). error details:{1}{0}", ex.ToString(), Environment.NewLine, Me._connection.ConnectionString), log.Level.Error)

            Return False
        End Try

    End Function

    Public Function execute(command As String) As Boolean

        Dim cmd As New NpgsqlCommand(command, Me._connection)

        Try

            Me._connection.Open()
            cmd.ExecuteNonQuery()
            Me._connection.Close()

            Return True

        Catch ex As Exception

            log.write_line(String.Format("error executing command against pgSQLServer.{1}statement:{0}{1}exception:{2}", command, Environment.NewLine, ex.ToString()), log.Level.Error)
            Return False

        End Try

    End Function

    Public Function get_watched_schema() As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim res As New Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim cmd As New NpgsqlCommand("select TableName, FieldName from sosync_get_watched_schema", Me._connection)


        Me._connection.Open()
        Dim r = cmd.ExecuteReader()
        While r.Read()

            Dim table As String = r(0).ToString()
            Dim field As String = r(1).ToString()

            If Not res.ContainsKey(table) Then
                res.Add(table, New Dictionary(Of String, List(Of String)))

                res(table).Add("id_fields", New List(Of String))
                res(table).Add("fields", New List(Of String))

            End If

            If field = "id" OrElse table.EndsWith("_rel") Then
                res(table)("id_fields").Add(field)
            End If

            If field <> "id" Then
                res(table)("fields").Add(field)
            End If

        End While
        Me._connection.Close()

        Return res

    End Function

    Private Shared Function get_connection_string(instance As String, pw As String) As String

        Dim host As String = String.Format("pgsql.{0}.datadialog.net", instance)
        Dim port As String = 5432
        Dim user As String = instance
        Dim password As String = pw
        Dim database As String = instance

        Return String.Format("Server={0};Port={1};User ID={2};Password={3};Database={4};",
                             host,
                             port,
                             user,
                             password,
                             database)


    End Function

    Public Function get_unfetched_sync_records() As List(Of Dictionary(Of String, Object))

        Dim result As New List(Of Dictionary(Of String, Object))

        Dim command As New Npgsql.NpgsqlCommand("select id, table_name, odoo_id, odoo_id2, operation, creation from sosync_sync_table where fetched is null or fetched = 0 order by creation;", Me._connection)

        Me._connection.Open()

        Dim rdr = command.ExecuteReader()

        While rdr.Read()

            Dim rec_dic As New Dictionary(Of String, Object)

            For i As Integer = 0 To rdr.FieldCount - 1
                rec_dic.Add(rdr.GetName(i), rdr(i))
            Next

            result.Add(rec_dic)

        End While

        Me._connection.Close()

        Return result

    End Function

    Public Sub mark_sync_record_as_fetched(record As Dictionary(Of String, Object))

        Dim command As String = String.Format("update sosync_sync_table set fetched = 1 where id = {0}", record("id"))

        Dim cmd As New NpgsqlCommand(command, Me._connection)

        cmd.Connection.Open()

        cmd.ExecuteNonQuery()

        cmd.Connection.Close()

    End Sub

    Public Function get_uid() As Integer

        'todo set to sosync_user_name, meanwhile admin
        Dim command As String = String.Format("select id from res_users where login = '{0}' limit 1", "admin")

        Dim cmd As New NpgsqlCommand(command, Me._connection)

        cmd.Connection.Open()

        Dim result As Integer? = cmd.ExecuteScalar()

        cmd.Connection.Close()

        If result.HasValue Then
            Return result.Value
        Else
            Return 0
        End If

    End Function



    Public Function get_data(item As sync_table_record, schema As Dictionary(Of String, Dictionary(Of String, List(Of String)))) As Dictionary(Of String, Object)

        Dim command As String = String.Format("select {0} from {1} ", String.Join(", ", schema(item.Tabelle)("fields").ToArray()), item.Tabelle)

        Dim where_clause As String = String.Format("where {0} = {1}", schema(item.Tabelle)("id_fields")(0), item.odoo_id)

        If item.odoo_id2.HasValue Then
            where_clause &= String.Format(" and {0} = {1}", schema(item.Tabelle)("id_fields")(1), item.odoo_id2)
        End If

        Dim cmd As New Npgsql.NpgsqlCommand(String.Format("{0} {1}", command, where_clause), Me._connection)

        Dim data As New Dictionary(Of String, Object)

        Me._connection.Open()
        Dim r = cmd.ExecuteReader()

        If r.Read() Then

            For i As Integer = 0 To r.FieldCount - 1
                data.Add(r.GetName(i), r(i))
            Next

        End If

        r.Close()

        Me._connection.Close()

        Return data

    End Function

End Class
