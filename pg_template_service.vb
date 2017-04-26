Imports System.IO
Public Class pg_template_service

    Private _dir As String

    Private _odoo_user_id As Integer

    Private _instance_name As String

    Public Sub New(templates_dir As String, odoo_user_id As Integer, instance_name As String)
        Me._dir = templates_dir
        Me._odoo_user_id = odoo_user_id
        Me._instance_name = instance_name
        init_templates()
    End Sub

    Public ReadOnly Property templates As Dictionary(Of String, Dictionary(Of String, pg_template))

    Private Sub init_templates()

        Me._templates = New Dictionary(Of String, Dictionary(Of String, pg_template))

        For Each section_dir In System.IO.Directory.GetDirectories(Me._dir)

            Dim section_dir_info = New System.IO.DirectoryInfo(section_dir)

            Dim section_dic As New Dictionary(Of String, pg_template)

            Me._templates.Add(section_dir_info.Name, section_dic)

            For Each template_file In System.IO.Directory.GetFiles(section_dir, "*.pgt")

                Dim template_file_info As New System.IO.FileInfo(template_file)

                section_dic.Add(template_file_info.Name, New pg_template(System.IO.File.ReadAllText(template_file)))

            Next

        Next

    End Sub

    Public Function render(template As pg_template, table As String, fields As List(Of String), id_fields As List(Of String)) As String

        Dim substitutions As New Dictionary(Of String, String)


        For Each variable In template.variables2


            Select Case variable.Key
                Case "id2_sync_table_field"
                    If id_fields.Count = 2 Then
                        substitutions.Add(variable.Value.representation, variable.Value.substitute("odoo_id2"))
                    Else
                        substitutions.Add(variable.Value.representation, variable.Value.substitute(""))
                    End If
                Case "id_field_name"
                    substitutions.Add(variable.Value.representation, variable.Value.substitute(id_fields.Item(0)))
                Case "id2_field_name"
                    If id_fields.Count = 2 Then
                        substitutions.Add(variable.Value.representation, variable.Value.substitute(id_fields.Item(1)))
                    Else
                        substitutions.Add(variable.Value.representation, variable.Value.substitute(""))
                    End If
                Case "table_name"
                    substitutions.Add(variable.Value.representation, table)
                Case "odooUserID"
                    substitutions.Add(variable.Value.representation, Me._odoo_user_id.ToString())
                Case "fields_distinction"
                    substitutions.Add(variable.Value.representation, update_trigger_field_distinction(fields))
                Case "notification_channel"
                    substitutions.Add(variable.Value.representation, Me._instance_name)
            End Select

        Next

        Return template.substitute(substitutions)

    End Function

    Private Shared Function update_trigger_field_distinction(fields As List(Of String)) As String

        Dim str As New System.Text.StringBuilder()

        Dim counter As Integer = 0

        For Each field In fields
            counter += 1

            Dim line As String = String.Format("old.""{0}"" IS DISTINCT FROM new.""{0}""", field)
            If counter > 1 Then
                line = String.Format("or {0}", line)
            End If

            str.AppendLine(line)

        Next

        Return str.ToString()

    End Function

End Class
