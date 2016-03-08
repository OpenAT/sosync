variables available:
#{id2_sync_table_field} /*if the current table has 2 primary key columns(odoo) then 'odoo_id2' else ''*/
#{id_field_name} /*name of the first id column (pk) of the table*/
#{id2_field_name} /*name of the second id column (pk); empty ('') if there is none*/
#{table_name} /*name of the current table*/
#{odooUserID} /*user id of sosync User on odoo*/
#{fields_distinction} /*string for compare only watched fileds in update trigger (new.xy is distinct from old.xy or new.zz is distinct from old.zz .....)*/
#{notification_channel} /*the name of the notification chanel used to notify the sync controller about changes in direction otf(=odoo to frst)*/


formatting options:
#{[variable_name]:xxx} /*xxx = prefix for variable (mainly used for commas or table aliases)*/