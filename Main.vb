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

        'andere blöcke
sync_block:
        sync_work()

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

    Private Sub sync_work()
        System.Threading.Thread.Sleep(1000 * 10)
    End Sub

End Module

Module ConfigFileParser

End Module

Public Class ExecuteArgs


    Public Property windows_user As String
    Public Property windows_user_pw As String
    Public Property odoo_user As String
    Public Property odoo_user_pw As String
    Public Property postgres_user As String
    Public Property postgres_user_pw As String

    Public Property postgres_connection_string As String

    Public Property mssql_connection_string As String

    Public Property odoo_api_url As String

    Public Sub New()

    End Sub



End Class


Public Class hooks
    Public Sub New(state As sosyncState)

    End Sub
    Public Sub update()
        'update hook call
    End Sub
End Class