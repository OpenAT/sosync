Public Class sosyncState

    Private Const runagain_filename As String = "runagain"
    Private Const update_filename As String = "update"
    Private Const runlock_filename As String = "runlock"
    Private Const pause_filename As String = "pause"
    Private Const force_trigger_renew_filename As String = "force_trigger_renew"

    Public Property executing_directory As String
    Public Property instance As String

    Public Property update As Boolean
        Get
            Return get_state(update_filename)
        End Get
        Set(value As Boolean)
            save_state(value, update_filename)
        End Set
    End Property
    Public Property runagain As Boolean
        Get
            Return get_state(runagain_filename)
        End Get
        Set(value As Boolean)
            save_state(value, runagain_filename)
        End Set
    End Property
    Public Property runlock As Boolean
        Get
            Return get_state(runlock_filename)
        End Get
        Set(value As Boolean)
            Me.save_state(value, runlock_filename)
        End Set
    End Property
    Public Property pause As Boolean
        Get
            Return get_state(pause_filename)
        End Get
        Set(value As Boolean)
            Me.save_state(value, pause_filename)
        End Set
    End Property
    Public Property force_trigger_renew As Boolean
        Get
            Return get_state(force_trigger_renew_filename)
        End Get
        Set(value As Boolean)
            Me.save_state(value, force_trigger_renew_filename)
        End Set
    End Property

    Public ReadOnly Property process_id As Integer
        Get
            Return System.Diagnostics.Process.GetCurrentProcess().Id
        End Get
    End Property


    Public ReadOnly Property is_main_process As Boolean
        Get

            Dim current_main_process_id As Integer? = get_main_process_id(Me.executing_directory)

            If current_main_process_id.HasValue Then

                Dim my_process_id As Integer = Process.GetCurrentProcess().Id

                If my_process_id = current_main_process_id Then
                    Return True
                End If

                Dim main_process = (From el In System.Diagnostics.Process.GetProcesses() Where el.Id = current_main_process_id Select el).FirstOrDefault()

                If main_process Is Nothing OrElse main_process.HasExited Then
                    'altes state file löschen
                    file_remove_if_exists(get_state_full_file_name(current_main_process_id.ToString()))
                    'hier ist auch klar, dass der alte prozess nicht richtig beendet wurde.
                    'eventuell log hier
                    Return True
                Else
                    Return False
                End If

            End If

            Return True

        End Get
    End Property

    Public Sub unlink_main_process()
        file_remove_if_exists(get_state_full_file_name(Me.process_id.ToString()))
    End Sub

    Public Sub New()

        Me.executing_directory = System.IO.Directory.GetCurrentDirectory()
        log.write_line("working_dir = " & Me.executing_directory, log.Level.Info)
        'original:
        Me.instance = (New System.IO.DirectoryInfo(Me.executing_directory)).Name
        log.write_line("instance = " & Me.instance, log.Level.Info)
        'für debug:
        'Me.instance = "aahs"

        If Me.is_main_process Then
            file_create_or_replace(get_state_full_file_name(Me.process_id.ToString()))
        End If

    End Sub

    Private Sub save_state(state As Boolean, state_file_name As String)
        Dim file_name As String = get_state_full_file_name(state_file_name)
        If state Then
            file_create_or_replace(file_name)
        Else
            file_remove_if_exists(file_name)
        End If
    End Sub

    Private Function get_state(state_file_name As String) As Boolean
        Return file_exists(get_state_full_file_name(state_file_name))
    End Function

    Private Function get_state_full_file_name(state_file_name As String) As String
        Return String.Format("{0}\{1}", Me.executing_directory, state_file_name)
    End Function



End Class