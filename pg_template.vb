Public Class pg_template

    Private _content As String

    Public Sub New(template_content As String)
        Me._content = template_content
        Me._variables = pg_template_variable.extract_variables(Me._content)
    End Sub

    Public ReadOnly Property variables As List(Of pg_template_variable)

    Public ReadOnly Property variables2 As Dictionary(Of String, pg_template_variable)
        Get
            Return Me._variables.ToDictionary(Of String)(Function(x As pg_template_variable)
                                                             Return x.name
                                                         End Function)
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
