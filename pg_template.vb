Public Class pg_template

    Public ReadOnly Property content As String

    Public Sub New(template_content As String)
        Me._content = template_content
        Me._variables = pg_template_variable.extract_variables(Me._content)
    End Sub

    Public ReadOnly Property variables As List(Of pg_template_variable)

    Public ReadOnly Property variables2 As Dictionary(Of String, pg_template_variable)
        Get
            Dim ret As New Dictionary(Of String, pg_template_variable)
            For Each item In Me.variables
                If Not ret.ContainsKey(item.name) Then
                    ret.Add(item.name, item)
                End If
            Next
            Return ret
        End Get
    End Property

    Public Function substitute(substitutions As Dictionary(Of String, String)) As String
        Dim str As New System.Text.StringBuilder(Me._content)
        For Each item In substitutions
            str.Replace(item.Key, item.Value)
        Next
        Return str.ToString()
    End Function

End Class
