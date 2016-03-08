Public Class pg_template_variable

    Public Property name As String
    Public Property prefix As String
    Public Property representation As String
    Public Property substitution As String

    Private Sub substitute(variable_value As String)
        Me.substitution = Me.prefix & variable_value
    End Sub

    'Public Shared Sub substitute_variables(variables As List(Of variable), table As String, fields As List(Of SyncDefinition), odooUserID As Integer, notification_channel As String)
    '    Dim id_fields = get_id_columns(fields)
    '    For Each item In variables
    '        Select Case item.name
    '            Case "id2_sync_table_field"
    '                If id_fields.Count = 2 Then
    '                    item.substitute("odoo_id2")
    '                End If
    '            Case "id_field_name"
    '                item.substitute(id_fields.Item(0).Spalte)
    '            Case "id2_field_name"
    '                If id_fields.Count = 2 Then
    '                    item.substitute(id_fields.Item(1).Spalte)
    '                End If
    '            Case "table_name"
    '                item.substitute(table)
    '            Case "odooUserID"
    '                item.substitute(odooUserID.ToString())
    '            Case "fields_distinction"
    '                item.substitute(update_trigger_field_distinction(fields))
    '            Case "notification_channel"
    '                item.substitute(notification_channel)
    '        End Select
    '    Next
    'End Sub

    Public Shared Function extract_variables(template_content As String) As List(Of pg_template_variable)
        Dim l As New List(Of pg_template_variable)

        Dim regex As New System.Text.RegularExpressions.Regex("#{(.*?)}")

        Dim m = regex.Matches(template_content)

        For Each match As System.Text.RegularExpressions.Match In m

            Dim v As New pg_template_variable()
            l.Add(v)

            Dim abstraction_1 As String = match.Value.Substring(2, match.Value.Length - 3)

            Dim parts As List(Of String) = abstraction_1.Split(":").ToList()

            v.name = parts(0)
            If parts.Count > 1 Then
                v.prefix = parts(1)
            End If

            v.representation = match.Value

        Next

        Return l

    End Function
End Class
