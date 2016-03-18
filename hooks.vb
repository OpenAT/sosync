Public Class hooks
    Public Shared Sub sosyncer_run(instance As String)

        Dim webhook_url As String = String.Format("http://salt.datadialog.net:8000/hook/sosync/sync?instance={0}", instance)

        call_webhook(webhook_url)

    End Sub

    Public Shared Sub sosyncer_update(instance As String)

        Dim webhook_url As String = String.Format("http://salt.datadialog.net:8000/hook/sosync/update?instance={0}", instance)
        call_webhook(webhook_url)

    End Sub

    Private Shared Sub call_webhook(webhook_url As String)

        Dim process As New System.Diagnostics.Process()
        process.StartInfo.FileName = String.Format("{0}\{1}", System.IO.Directory.GetCurrentDirectory(), "webhook.exe")
        process.StartInfo.Arguments = webhook_url

        process.Start()

    End Sub

End Class