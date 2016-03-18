﻿Module Main

    Sub Main()

        Dim state As New sosyncState()
        Dim hooks As New hooks()

        If state.runlock Then
            If state.update Then
                state.unlink_main_process()
                Exit Sub
            ElseIf Not state.is_main_process Then
                state.runagain = True
                Exit Sub
            End If
        End If

        state.runlock = True

        If state.update Then
            state.unlink_main_process()
            hooks.sosyncer_update(state.instance)
            Exit Sub
        End If


sync_block:

        Dim config = (New IniParser.FileIniDataParser()).ReadFile(String.Format("{0}\sosync.ini", state.executing_directory))

        Dim studio_pw As String = config.Sections("studio")("studio_pw")
        Dim online_admin_pw As String = config.Sections("online")("online_admin_pw")
        Dim online_pgsql_pw As String = config.Sections("online")("online_pgsql_pw")

        Dim pgSQLHost As New pgSQLServer(state.instance, online_pgsql_pw)
        Dim msSQLHost As New msSQLServer(state.instance, studio_pw)

        If Not pgSQLHost.try_connect() OrElse Not msSQLHost.try_connect() Then
            log.write_line("syncer exit - couldn't connect to all db-servers", log.Level.Error)
            GoTo end_block
        End If

        Dim odoo_user_id As Integer = pgSQLHost.get_uid()

        Dim schema = msSQLHost.get_Schema()

        If Not schema_check(pgSQLHost, msSQLHost, odoo_user_id, state.instance, schema) Then
            log.write_line("syncer exit - schema check not successful", log.Level.Error)
            GoTo end_block
        End If

        If Not fetch_work(pgSQLHost, msSQLHost, schema) Then
            log.write_line("syncer exit - fetch work not successful", log.Level.Error)
            GoTo end_block
        End If

        Dim api = New odooXMLRPCWrapper(state.instance, odoo_user_id, online_admin_pw, msSQLHost)

        sync_work(pgSQLHost, msSQLHost, api, schema)


end_block:

        If state.update Then
            state.runagain = False
            state.unlink_main_process()
            hooks.sosyncer_update(state.instance)
            Exit Sub
        End If

        If state.runagain Then
            state.runagain = False
            GoTo sync_block
        End If

        state.runlock = False
        state.unlink_main_process()
        Exit Sub


    End Sub

    Private Sub sync_work(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, api As odooXMLRPCWrapper, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))))

        Dim work = msSQLHost.get_sync_work()

        For Each record In work

            Select Case record.Direction
                Case True 'online to studio
                    Select Case record.Operation
                        Case "i"
                            msSQLHost.work_insert(record, pgSQLHost, schema)
                        Case "u"
                            msSQLHost.work_update(record, pgSQLHost, schema)
                        Case "d"
                            msSQLHost.work_delete(record, pgSQLHost, schema)

                    End Select
                Case False 'studio to online
                    If record.Tabelle.EndsWith("_rel") Then
                        Select Case record.Operation
                            Case "i"
                                api.insert_object_rel(record, schema(record.Tabelle)("online_model_name")(0), schema(record.Tabelle)("online_model_rel_field_name")(0), record.odoo_id.Value, record.odoo_id2.Value)
                            Case "u"
                                'no update implemented (not needed)
                            Case "d"
                                api.delete_object_rel(record, schema(record.Tabelle)("online_model_name")(0), schema(record.Tabelle)("online_model_rel_field_name")(0), record.odoo_id.Value, record.odoo_id2.Value)

                        End Select
                    Else
                        Select Case record.Operation
                            Case "i"
                                api.insert_object(record, schema(record.Tabelle)("online_model_name")(0), odooXMLRPCWrapper.create_json_serialized_data(pgSQLHost.get_data(record, schema)))

                            Case "u"
                                api.insert_object(record, schema(record.Tabelle)("online_model_name")(0), odooXMLRPCWrapper.create_json_serialized_data(pgSQLHost.get_data(record, schema)))

                            Case "d"
                                api.insert_object(record, schema(record.Tabelle)("online_model_name")(0), odooXMLRPCWrapper.create_json_serialized_data(pgSQLHost.get_data(record, schema)))

                        End Select

                    End If
            End Select

        Next

    End Sub

    Private Function fetch_work(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, schema As Dictionary(Of String, Dictionary(Of String, List(Of String)))) As Boolean

        Try

            For Each record In pgSQLHost.get_unfetched_sync_records()

                record.Add("@direction", True)
                record.Add("@id", If(msSQLHost.request_ID(schema, record("table_name"), record("odoo_id"), record("odoo_id2")), DBNull.Value))

                msSQLHost.insert_sync_record(record)

            Next

            Return True

        Catch ex As Exception

            log.write_line(String.Format("error fetching records. error details:{0}{1}", Environment.NewLine, ex.ToString()), log.Level.Error)

            Return False

        End Try

    End Function
   Private Function schema_check(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, odoo_user_id As Integer, instance As String, schema As Dictionary(Of String, Dictionary(Of String, List(Of String)))) As Boolean

        Dim templates As New pg_template_service(".\files\scripts", odoo_user_id, instance)

        Dim sync_table_template = templates.templates("01_sync_table")("sync_table.pgt")
        Dim schema_view_template = templates.templates("02_schema_view")("get_watched_schema.pgt")
        Dim drop_triggers_template = templates.templates("99_drops")("drop_triggers.pgt")
        Dim init_sync_update_tamplate = templates.templates("51_init_sync")("update.pgt")
        Dim init_sync_insert_tamplate = templates.templates("51_init_sync")("insert.pgt")

        If Not pgSQLHost.execute(sync_table_template.content) Then Return False
        If Not pgSQLHost.execute(schema_view_template.content) Then Return False

        Dim origin_schema = schema
        Dim watched_schema = pgSQLHost.get_watched_schema()

        Dim initial_syncs As New Dictionary(Of String, String)

        Dim renew_triggers As New List(Of String)
        Dim remove_triggers As New List(Of String)

        For Each table In origin_schema

            If Not watched_schema.ContainsKey(table.Key) Then

                initial_syncs.Add(table.Key, "i")
                renew_triggers.Add(table.Key)

            Else

                For Each field In table.Value("fields")

                    If Not watched_schema(table.Key)("fields").Contains(field) Then

                        initial_syncs.Add(table.Key, "u")
                        renew_triggers.Add(table.Key)

                    End If

                Next

            End If

        Next


        For Each table In watched_schema

            If Not origin_schema.ContainsKey(table.Key) Then

                remove_triggers.Add(table.Key)

            Else

                For Each field In table.Value("fields")

                    If Not origin_schema(table.Key)("fields").Contains(field) Then

                        renew_triggers.Add(table.Key)

                    End If

                Next

            End If

        Next

        For Each table In renew_triggers

            For Each template In templates.templates("03_per_table")

                Dim cmd = templates.render(template.Value, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

                If Not pgSQLHost.execute(cmd) Then Return False

            Next

        Next

        For Each table In remove_triggers

            Dim cmd = templates.render(drop_triggers_template, table, watched_schema(table)("fields"), watched_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next

        For Each update In (From el In initial_syncs Where el.Value = "u")

            Dim table = update.Key

            Dim cmd = templates.render(init_sync_update_tamplate, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next

        For Each insert In (From el In initial_syncs Where el.Value = "i")

            Dim table = insert.Key

            Dim cmd = templates.render(init_sync_insert_tamplate, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next



        Return True

    End Function

End Module

