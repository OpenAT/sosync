Module Main

    Sub Main()

        Dim state As New sosyncState()
        Dim hooks As New hooks(state)

        If state.runlock Then
            If state.update Then
                state.unlink_main_process()
                Exit Sub
            ElseIf Not state.is_main_process Then
                state.runagain = True
                Exit Sub
            End If
        End If

        state.runlock = True

        If state.update Then
            state.unlink_main_process()
            hooks.update()
            Exit Sub
        End If


sync_block:
        sync_work(state)

        If state.update Then
            state.runagain = False
            state.unlink_main_process()
            hooks.update()
            Exit Sub
        End If

        If state.runagain Then
            state.runagain = False
            GoTo sync_block
        End If

        state.runlock = False
        state.unlink_main_process()
        Exit Sub


    End Sub

    Private Sub sync_work(state As sosyncState)

        Dim config = (New IniParser.FileIniDataParser()).ReadFile(String.Format("{0}\sosync.ini", state.executing_directory))

        Dim studio_mssql_pw As String = config.Sections("studio")("studio_mssql_pw")
        Dim online_admin_pw As String = config.Sections("online")("online_admin_pw")
        Dim online_pgsql_pw As String = config.Sections("online")("online_pgsql_pw")

        schema_check(state, online_pgsql_pw)




        'getestet, funktioniert!:
        'Using s As New dadi_impersonation.ImpersonationScope("hgh@datadialog.net", "lefkos")

        '    Dim cmd As New System.Data.SqlClient.SqlCommand("select system_user", New SqlClient.SqlConnection("Data Source=dadisql;Initial Catalog=AG_AIWWF;Integrated Security=True"))

        '    cmd.Connection.Open()
        '    Dim res = cmd.ExecuteScalar()
        '    cmd.Connection.Close()

        'End Using

    End Sub

    Private Sub schema_check(state As sosyncState, online_pgsql_pw As String)

        Dim pgHost As New pgSQLServer(state.instance, online_pgsql_pw)



    End Sub

End Module


Public Class hooks
    Public Sub New(state As sosyncState)

    End Sub
    Public Sub update()
        'update hook call
    End Sub
End Class