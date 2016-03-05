Public Class msSQLServer

    Private _instance As String
    Private _pw As String

    Public Sub New(instance As String, pw As String)
        Me._instance = instance
        Me._pw = pw
    End Sub



    Public Function get_Schema() As sosyncSchema

        Dim res As New sosyncSchema()
        res.Models = New List(Of sosyncSchemaModel)

        Using New dadi_impersonation.ImpersonationScope(String.Format(dadi_upn_prototype, Me._instance), Me._pw)

            Dim db As New mdbDataContext()

        End Using

    End Function




    Private Shared Function get_connection_string(instance As String) As String

        Dim db_name = String.Format(db_name_prototype, instance)

        Return String.Format(connection_string_prototype, instance, db_name)

    End Function

    Private Shared Function get_fallback_connection_string(instance As String) As String

        Try

            Dim dnsIP = System.Net.Dns.GetHostEntry(dadi_dns_server_name)

            Dim dnsClient As New DNS.Client.DnsClient(dnsIP.AddressList(0))

            Dim response = dnsClient.Resolve(String.Format(fallback_connection_string_cname_dns_source_prototype, instance), DNS.Protocol.RecordType.CNAME)

            Dim db_name As String = CType(response.AnswerRecords(0), DNS.Protocol.ResourceRecords.CanonicalNameResourceRecord).CanonicalDomainName.ToString().Split(char_dot)(0)

            Return String.Format(connection_string_prototype, instance, db_name)

        Catch ex As Exception

            Return msSQLServer.get_connection_string(instance)

        End Try



    End Function

    Private Const connection_string_prototype As String = "Data Source=mssql.{0}.datadialog.net;Initial Catalog={1};Integrated Security=True"
    Private Const db_name_prototype As String = "MDB_{0}"

    Private Const fallback_connection_string_cname_dns_source_prototype As String = "mdb.mssql.{0}.datadialog.net"

    Private Const dadi_dns_server_name As String = "dadidc01.datadialog.net"

    Private Const dadi_upn_prototype As String = "{0}@datadialog.net"

    Private Const char_dot As String = "."
End Class
