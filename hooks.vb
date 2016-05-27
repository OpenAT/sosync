Public Class hooks
    Public Shared Sub sosyncer_run(instance As String)

        Dim webhook_url As String = String.Format("https://salt.datadialog.net:8000/hook/sosync/sync?instance={0}", instance)
        dadi_webhook.SendWebhook(webhook_url)


    End Sub

    Public Shared Sub sosyncer_update(instance As String)

        Dim webhook_url As String = String.Format("https://salt.datadialog.net:8000/hook/sosync/update?instance={0}", instance)
        dadi_webhook.SendWebhook(webhook_url)

    End Sub

End Class
