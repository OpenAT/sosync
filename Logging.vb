Module Logging
    Private _instance As String
    Public Sub init(instance As String)
        _instance = instance
    End Sub
    Public Sub log(component As String, message As String)
        log(component, message, False)
    End Sub
    Public Sub log(component As String, message As String, only_warning As Boolean)


        Dim empf As String = "seh@datadialog.net"
        Dim instance As String = _instance
        Dim service As String = "sosyncer"
        Dim name As String = component

        dadi.logging.LogEntry.Create(instance,
                                     service,
                                     If(only_warning, dadi.logging.EntryLevel.Warnung, dadi.logging.EntryLevel.Fehler),
                                     name,
                                     message,
                                     If(only_warning, "", empf))
    End Sub

End Module
