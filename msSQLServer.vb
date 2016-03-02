Public Class msSQLServer

    Private _instance As String
    Private _pw As String

    Public Sub New(instance As String, pw As String)
        Me._instance = instance
        Me._pw = pw
    End Sub

End Class
