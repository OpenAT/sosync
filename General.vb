Module General
    Private Const sosync_executeable_name As String = "sosync.exe"
    Public Sub file_create_or_replace(filename As String)
        If Not System.IO.File.Exists(filename) Then
            Dim str = System.IO.File.Create(filename)
            str.Close()
        End If
    End Sub

    Public Sub file_remove_if_exists(filename As String)
        If System.IO.File.Exists(filename) Then
            System.IO.File.Delete(filename)
        End If
    End Sub

    Public Function file_exists(filename As String) As Boolean
        Return System.IO.File.Exists(filename)
    End Function

    Public Function get_main_process_id(executing_directory As String) As Integer?

        Dim numeric_files = (From el In (New System.IO.DirectoryInfo(executing_directory)).GetFiles() Where IsNumeric(el.Name) Select el).ToList()

        If numeric_files.Count = 0 Then
            Return Nothing
        End If

        If numeric_files.Count > 1 Then
            'zustand darf nicht auftreten!!!
        End If

        If numeric_files.Count = 1 Then
            Return Integer.Parse(numeric_files.FirstOrDefault().Name)
        End If

    End Function

End Module
