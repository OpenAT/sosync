Imports CookComputing.XmlRpc

Public Class odooXMLRPCWrapper

    Private proxy As odooRPC
    Private db As String
    Private uid As Integer
    Private password As String
    Private msSQLHost As msSQLServer

    Public Sub New(instance As String, uid As Integer, password As String, msSQLHost As msSQLServer)

        proxy = XmlRpcProxyGen.Create(Of odooRPC)()

        CType(proxy, IXmlRpcProxy).Url = String.Format("http://{0}.datadialog.net/xmlrpc/2/object", instance)

        Me.db = instance
        Me.uid = uid
        Me.password = password
        Me.msSQLHost = msSQLHost

    End Sub
    Public Shared Function create_json_serialized_data(data As Dictionary(Of String, Object)) As Dictionary(Of String, String)

        Dim result As New Dictionary(Of String, String)

        For Each item In data
            result.Add(item.Key, serialize_json(item.Value))
        Next

        Return result

    End Function

    Private Shared Function serialize_json(value As Object) As String

        If value Is Nothing OrElse value Is DBNull.Value Then
            Return ""
        End If

        Dim s As New Newtonsoft.Json.JsonSerializer()
        Dim sb As New System.Text.StringBuilder()
        Dim wr As New System.IO.StringWriter(sb)
        Dim w As New Newtonsoft.Json.JsonTextWriter(wr)
        s.Serialize(w, value)

        Dim retVal = sb.ToString()

        w.Close()

        If retVal.EndsWith("""") AndAlso retVal.StartsWith("""") Then
            retVal = retVal.Substring(1, retVal.Length - 2)
        End If

        Return retVal

    End Function

    Public Sub insert_object_rel(record As sync_table_record, model_name As String, field_name As String, odoo_id As Integer, odoo_id2 As Integer?)

        '#general syntax for many2many field
        '$many2many_field = array(
        '    New xmlrpcval(
        '        Array(
        '            New xmlrpcval(6, "int"),
        '            New xmlrpcval(0, "int"),
        '            New xmlrpcval(Array(New xmlrpcval($order_id, "int")), "array")
        '            ), "array")
        '    );

        'Array('invoice_ids'=> new xmlrpcval($many2many_field, "array"))


        Try

            Dim field_array(1) As Object
            Dim field_struct(3) As Object
            field_struct(0) = 4
            field_struct(1) = odoo_id2
            field_struct(2) = 0
            field_array(0) = field_struct

            Dim data As New XmlRpcStruct()
            data.Add(field_name, field_array)

            Dim args(2)

            args(0) = odoo_id
            args(1) = data

            record.SyncStart = Now

            proxy.execute_kw(db, uid, password, model_name, "write", args)

            record.SyncEnde = Now
            record.SyncResult = True
            msSQLHost.save_sync_table_record(record)

        Catch ex As Exception

            record.SyncEnde = Now
            record.SyncResult = False
            record.SyncMessage = ex.ToString()
            msSQLHost.save_sync_table_record(record)

        End Try



    End Sub

    Public Sub delete_object_rel(record As sync_table_record, model_name As String, field_name As String, odoo_id As Integer, odoo_id2 As Integer?)

        Try

            Dim field_array(1) As Object
            Dim field_struct(3) As Object
            field_struct(0) = 3
            field_struct(1) = odoo_id2
            field_struct(2) = 0
            field_array(0) = field_struct

            Dim data As New XmlRpcStruct()
            data.Add(field_name, field_array)

            Dim args(2)

            args(0) = odoo_id
            args(1) = data

            record.SyncStart = Now

            proxy.execute_kw(db, uid, password, model_name, "write", args)

            record.SyncEnde = Now
            record.SyncResult = True
            msSQLHost.save_sync_table_record(record)

        Catch ex As Exception

            record.SyncEnde = Now
            record.SyncResult = False
            record.SyncMessage = ex.ToString()
            msSQLHost.save_sync_table_record(record)

        End Try


    End Sub

    Public Function insert_object(record As sync_table_record, model_name As String, data As Dictionary(Of String, String)) As Integer?


        record.SyncStart = Now

        Dim ret As Integer? = Nothing

        Try

            record.SyncStart = Now

            Dim retVal = proxy.execute_kw(db, uid, password, model_name, "create", create_args(data))

            If retVal.GetType() Is GetType(Integer) AndAlso CType(retVal, Integer?).HasValue Then
                ret = retVal
            End If

            record.SyncEnde = Now
            record.SyncResult = True
            msSQLHost.save_sync_table_record(record)

        Catch ex As Exception

            record.SyncEnde = Now
            record.SyncResult = False
            record.SyncMessage = ex.ToString()
            msSQLHost.save_sync_table_record(record)

        End Try

        Return ret

    End Function

    Public Sub update_object(record As sync_table_record, model_name As String, id As Integer, data As Dictionary(Of String, String))

        Try

            record.SyncStart = Now

            proxy.execute_kw(db, uid, password, model_name, "write", create_args(data, id))

            record.SyncEnde = Now
            record.SyncResult = True
            msSQLHost.save_sync_table_record(record)

        Catch ex As Exception

            record.SyncEnde = Now
            record.SyncResult = False
            record.SyncMessage = ex.ToString()
            msSQLHost.save_sync_table_record(record)

        End Try

    End Sub

    Public Sub delete_object(record As sync_table_record, model_name As String, id As Integer)

        Try

            record.SyncStart = Now

            proxy.execute_kw(db, uid, password, model_name, "unlink", create_args(Nothing, id))

            record.SyncEnde = Now
            record.SyncResult = True
            msSQLHost.save_sync_table_record(record)

        Catch ex As Exception

            record.SyncEnde = Now
            record.SyncResult = False
            record.SyncMessage = ex.ToString()
            msSQLHost.save_sync_table_record(record)

        End Try

    End Sub

    Private Function create_args(data As Dictionary(Of String, String), ParamArray additionalArgs() As Object) As Object()

        Dim xml_args As New XmlRpcStruct()
        If data IsNot Nothing Then
            For Each item In data
                xml_args.Add(item.Key, item.Value)
            Next

        End If

        Dim arraySize As Integer = 0
        If data Is Nothing Then
            arraySize = additionalArgs.Count - 1
        Else
            arraySize = additionalArgs.Count
        End If

        Dim res(arraySize) As Object

        For i As Integer = 0 To additionalArgs.Count - 1
            res(i) = additionalArgs(i)
        Next

        If data IsNot Nothing Then
            res(additionalArgs.Count) = xml_args

        End If

        Return res

    End Function

    Public Interface odooRPC

        <XmlRpcMethod("execute_kw")>
        Function execute_kw(db As String, uid As Integer, password As String, model_name As String, method_name As String, args As Object()) As Object

    End Interface

End Class
