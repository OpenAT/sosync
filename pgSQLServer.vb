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

End Class
