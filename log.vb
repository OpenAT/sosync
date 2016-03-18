Public Class log

    Public Enum Level
        Info = 0
        Warning = 1
        [Error] = 2
    End Enum

    Private Const log_file_name As String = "log"
    Private Shared _executing_dir As String
    Private Shared _executing_dir_initialized As Boolean = False
    Public Shared Sub write_line(Line As String, level As Level)

        If Not _executing_dir_initialized Then
            init_executing_dir()
        End If

        Dim file_name As String = String.Format("{0}\{1}", log._executing_dir, log.log_file_name)

        System.IO.File.AppendAllText(file_name, String.Format("[@@log@{3}@{2:yyyy-MM-ddTHH:mm:ss.fffffff}]{0}{1}", Line, Environment.NewLine, Now, level))

    End Sub

    Private Shared Sub init_executing_dir()

        log._executing_dir = System.IO.Directory.GetCurrentDirectory()
        log._executing_dir_initialized = True

    End Sub

End Class
