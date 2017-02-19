﻿Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates

Module Main

    Sub Main2()

        Dim state As New sosyncState()
        Dim config = (New IniParser.FileIniDataParser()).ReadFile(String.Format("{0}\sosync.ini", state.executing_directory))

        Logging.init(state.instance)

        Dim studio_pw As String = config.Sections("studio")("studio_pw")
        Dim online_admin_pw As String = config.Sections("online")("online_admin_pw")
        Dim online_pgsql_pw As String = config.Sections("online")("online_pgsql_pw")
        Dim online_sosync_pw As String = config.Sections("online")("online_sosync_pw")
        Dim pgSQLHost As New pgSQLServer(state.instance, online_pgsql_pw)
        Dim msSQLHost As New msSQLServer(state.instance, studio_pw)

        If Not pgSQLHost.try_connect() OrElse Not msSQLHost.try_connect() Then
            log.write_line("syncer exit - couldn't connect to all db-servers", log.Level.Error)

        End If


        Dim schema = msSQLHost.get_Schema()
        Dim field_types = msSQLHost.get_Schema_fieldTypes()

        Dim odoo_user_id As Integer = pgSQLHost.get_uid()

        Dim api = New odooXMLRPCWrapper(state.instance, odoo_user_id, online_sosync_pw, msSQLHost)

        'schema("res_partner")("fields").Remove("birthdate_web")

        Dim r As New sync_table_record()
        r.sync_tableID = 97174
        r.Direction = True
        r.Tabelle = "res_partner"
        r.Operation = "u"
        r.ID = 844
        r.odoo_id = 2373
        r.odoo_id2 = Nothing
        'api.update_object(r, schema(r.Tabelle)("online_model_name")(0), r.odoo_id, msSQLHost.get_data(r, schema))
        'msSQLHost.work_insert(r, api, schema, field_types, pgSQLHost)
        msSQLHost.work_update(r, api, schema, field_types)
    End Sub

    Sub Main()


        Dim state As New sosyncState()
        Dim hooks As New hooks()

        Logging.init(state.instance)

        If state.pause Then
            Exit Sub
        End If

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
        Dim online_sosync_pw As String = config.Sections("online")("online_sosync_pw")

        Dim pgSQLHost As New pgSQLServer(state.instance, online_pgsql_pw)
        Dim msSQLHost As New msSQLServer(state.instance, studio_pw)

        If Not pgSQLHost.try_connect() OrElse Not msSQLHost.try_connect() Then
            log.write_line("syncer exit - couldn't connect to all db-servers", log.Level.Error)
            GoTo end_block
        End If

        Dim odoo_user_id As Integer = pgSQLHost.get_uid()

        Dim schema = msSQLHost.get_Schema()
        Dim field_types = msSQLHost.get_Schema_fieldTypes()

        If Not schema_check(pgSQLHost, msSQLHost, odoo_user_id, state.instance, schema, state.force_trigger_renew) Then
            log.write_line("syncer exit - schema check not successful", log.Level.Error)
            GoTo end_block
        End If

        state.force_trigger_renew = False

        If Not fetch_work(pgSQLHost, msSQLHost, schema) Then
            log.write_line("syncer exit - fetch work not successful", log.Level.Error)
            GoTo end_block
        End If

        Dim api = New odooXMLRPCWrapper(state.instance, odoo_user_id, online_sosync_pw, msSQLHost)

        sync_work(pgSQLHost, msSQLHost, api, schema, field_types, state)


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


    Private Sub sync_work(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, api As odooXMLRPCWrapper, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))), field_types As Dictionary(Of String, Dictionary(Of String, String)), state As sosyncState)

        Dim work = msSQLHost.get_sync_work()

        For Each record In work

            msSQLHost.get_new_ids(record)

            Dim result As Boolean = False
            Dim done As Boolean = False

            record.SyncStart = Now


            If Not schema.ContainsKey(record.Tabelle) Then

                record.SyncEnde = Now
                record.SyncResult = True
                record.SyncMessage = String.Format("{0} is not synced anymore (not in schema)", record.Tabelle)
                msSQLHost.save_sync_table_record(record)

                result = True
                done = True

            End If

            If Not done Then

                Try



                    Select Case record.Direction
                        Case True 'online to studio
                            Select Case record.Operation
                                Case "i"
                                    result = msSQLHost.work_insert(record, api, schema, field_types, pgSQLHost)
                                Case "u"
                                    result = msSQLHost.work_update(record, api, schema, field_types)
                                Case "d"
                                    result = msSQLHost.work_delete(record, api, schema)

                            End Select
                        Case False 'studio to online
                            If record.Tabelle.EndsWith("_rel") Then

                                Dim ix_id As Integer = schema(record.Tabelle)("online_model_rel_fields").IndexOf("id")

                                Dim ix_link As Integer = If(ix_id = 0, 1, 0)

                                Select Case record.Operation
                                    Case "i"

                                        result = api.insert_object_rel(record, schema(record.Tabelle)("online_model_name")(0), schema(record.Tabelle)("online_model_rel_fields")(ix_link), If(ix_id = 0, record.odoo_id.Value, record.odoo_id2.Value), If(ix_link = 1, record.odoo_id2.Value, record.odoo_id))

                                    Case "u"
                                'no update implemented (not needed)
                                    Case "d"

                                        result = api.delete_object_rel(record, schema(record.Tabelle)("online_model_name")(0), schema(record.Tabelle)("online_model_rel_fields")(ix_link), If(ix_id = 0, record.odoo_id.Value, record.odoo_id2.Value), If(ix_link = 1, record.odoo_id2.Value, record.odoo_id))

                                End Select
                            Else
                                Select Case record.Operation
                                    Case "i"


                                        result = api.insert_object(record, schema(record.Tabelle)("online_model_name")(0), msSQLHost.get_data(record, schema), msSQLHost)

                                    'If new_id.HasValue Then
                                    '    msSQLHost.save_new_odoo_id(record, new_id.Value)
                                    'End If


                                    Case "u"
                                        result = api.update_object(record, schema(record.Tabelle)("online_model_name")(0), record.odoo_id, msSQLHost.get_data(record, schema))

                                    Case "d"
                                        result = api.delete_object(record, schema(record.Tabelle)("online_model_name")(0), record.odoo_id)

                                End Select

                            End If
                    End Select

                Catch ex As Exception

                    Logging.log("sync-work", ex.ToString())
                    result = False

                End Try

            End If

            If Not result Then

                Exit For

            End If

        Next

    End Sub

    Private Function fetch_work(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, schema As Dictionary(Of String, Dictionary(Of String, List(Of String)))) As Boolean

        ' Try

        Dim l = pgSQLHost.get_unfetched_sync_records()



        For Each record In l
            Try
                Dim record_copy = New Dictionary(Of String, Object)(record)

                Dim id_2 As Integer? = Nothing
                If Not record_copy("odoo_id2") Is DBNull.Value Then
                    id_2 = record_copy("odoo_id2")
                End If

                record_copy.Add("direction", True)

                record_copy.Remove("id")

                record_copy.Add("id", msSQLHost.request_ID(schema, record("table_name"), record("odoo_id"), id_2))

                msSQLHost.insert_sync_record(record_copy)

                pgSQLHost.mark_sync_record_as_fetched(record) 'take original "record", not copy (where id is overwritten!)

                If record("table_name").ToString().EndsWith("_rel") Then

                    Dim start As Date = Now

                    If msSQLHost.sync_record_exists(record("table_name"), record("odoo_id"), record("odoo_id2"), False, record("operation")) Then

                        Dim rec As New sync_table_record()

                        rec.sync_tableID = record_copy("sync_tableID")

                        rec.SyncStart = start
                        rec.SyncEnde = Now
                        rec.SyncMessage = "rel table insert double direction case"
                        rec.SyncResult = True

                        msSQLHost.save_sync_table_record(rec)

                    End If

                End If

            Catch ex As Exception
                log.write_line(String.Format("error fetching records. error details:{0}{1}", Environment.NewLine, ex.ToString()), log.Level.Error)

                Return False


            End Try

        Next

        Return True

        'Catch ex As Exception

        'End Try

    End Function
    Private Function schema_check(pgSQLHost As pgSQLServer, msSQLHost As msSQLServer, odoo_user_id As Integer, instance As String, schema As Dictionary(Of String, Dictionary(Of String, List(Of String))), force_trigger_renew As Boolean) As Boolean

        Dim templates As New pg_template_service(".\files\scripts", odoo_user_id, instance)

        Dim sync_table_template = templates.templates("01_sync_table")("sync_table.pgt")
        Dim schema_view_template = templates.templates("02_schema_view")("get_watched_schema.pgt")
        Dim sosync_current_odoo_user_id_template = templates.templates("02_sosync_current_odoo_user_id")("sosync_current_odoo_user_id.pgt")
        Dim drop_triggers_template = templates.templates("99_drops")("drop_triggers.pgt")
        'Dim init_sync_update_template = templates.templates("51_init_sync")("update.pgt")
        Dim init_sync_insert_template = templates.templates("51_init_sync")("insert.pgt")
        Dim init_sync_resync_template = templates.templates("51_init_sync")("resync.pgt")

        If Not pgSQLHost.execute(sync_table_template.content) Then Return False
        If Not pgSQLHost.execute(schema_view_template.content) Then Return False
        If Not pgSQLHost.execute(sosync_current_odoo_user_id_template.content) Then Return False

        Dim origin_schema = schema
        Dim watched_schema = pgSQLHost.get_watched_schema()
        Dim current_odoo_id As Integer? = pgSQLHost.get_current_odoo_user_id()

        Dim initial_syncs As New Dictionary(Of String, String)

        Dim renew_triggers As New List(Of String)
        Dim remove_triggers As New List(Of String)

        Dim renew_all_triggers As Boolean = force_trigger_renew

        If current_odoo_id.HasValue AndAlso current_odoo_id <> odoo_user_id Then
            renew_all_triggers = True
        End If

        For Each table In origin_schema

            If renew_all_triggers Then

                If table.Key.ToLower().EndsWith("_rel") Then 'only insert
                    initial_syncs.Add(table.Key, "i")
                Else
                    initial_syncs.Add(table.Key, "r") 'resync!
                End If

                renew_triggers.Add(table.Key)

            ElseIf Not watched_schema.ContainsKey(table.Key) Then

                If table.Key.ToLower().EndsWith("_rel") Then 'only insert
                    initial_syncs.Add(table.Key, "i")
                Else
                    initial_syncs.Add(table.Key, "r") 'resync!
                End If

                renew_triggers.Add(table.Key)

            Else

                For Each field In table.Value("fields")

                    If Not watched_schema(table.Key)("fields").Contains(field) Then


                        If table.Key.ToLower().EndsWith("_rel") Then 'only insert
                            initial_syncs.Add(table.Key, "i")
                        Else
                            initial_syncs.Add(table.Key, "r") 'resync!
                        End If

                        renew_triggers.Add(table.Key)
                        Exit For

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
                        Exit For

                    End If

                Next

            End If

        Next

        For Each table In renew_triggers

            If table.EndsWith("_rel") Then

                For Each template In templates.templates("04_per_rel_table")

                    Dim cmd = templates.render(template.Value, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

                    If Not pgSQLHost.execute(cmd) Then Return False

                Next

            ElseIf table = "ir_translation" Then

                For Each template In templates.templates("05_per_wo_write_uid_table")

                    Dim cmd = templates.render(template.Value, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

                    If Not pgSQLHost.execute(cmd) Then Return False

                Next




            Else

                For Each template In templates.templates("03_per_table")

                    Dim cmd = templates.render(template.Value, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

                    If Not pgSQLHost.execute(cmd) Then Return False

                Next

            End If

        Next

        For Each table In remove_triggers

            Dim cmd = templates.render(drop_triggers_template, table, watched_schema(table)("fields"), watched_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next

        For Each resync In (From el In initial_syncs Where el.Value = "r")

            Dim table = resync.Key

            Dim cmd = templates.render(init_sync_resync_template, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next

        'For Each update In (From el In initial_syncs Where el.Value = "u")

        '    Dim table = update.Key

        '    Dim cmd = templates.render(init_sync_update_template, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

        '    If Not pgSQLHost.execute(cmd) Then Return False

        'Next

        For Each insert In (From el In initial_syncs Where el.Value = "i")

            Dim table = insert.Key

            Dim cmd = templates.render(init_sync_insert_template, table, origin_schema(table)("fields"), origin_schema(table)("id_fields"))

            If Not pgSQLHost.execute(cmd) Then Return False

        Next



        Return True

    End Function

End Module

