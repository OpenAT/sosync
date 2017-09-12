﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WebSosync.Data.Properties {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("WebSosync.Data.Properties.Resources", typeof(Resources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to with updated_rows as (
        ///	update sync_table
        ///	set job_state = &apos;done&apos;, job_log = @job_log, job_closed_by_job_id = @job_closed_by_job_id, job_last_change = @job_last_change
        ///	where
        ///		job_source_sosync_write_date &lt; @job_source_sosync_write_date
        ///		and job_source_system = @job_source_system
        ///		and job_source_model = @job_source_model
        ///		and job_source_record_id = @job_source_record_id
        ///		and job_state = &apos;new&apos;
        ///	returning job_id
        ///)
        ///select count(*) affected_rows from updated_rows;.
        /// </summary>
        internal static string ClosePreviousJobs_Update_SCRIPT {
            get {
                return ResourceManager.GetString("ClosePreviousJobs_Update_SCRIPT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to with recursive children as (
        ///	-- roots
        ///	select *
        ///	from sync_table
        ///	where parent_job_id is null and job_state in (&apos;new&apos;, &apos;inprogress&apos;)
        ///
        ///	union all
        ///	
        ///	-- children
        ///	select child.*
        ///	from sync_table as child
        ///	inner join sync_table parent on child.parent_job_id = parent.job_id
        ///)
        ///select * from children;.
        /// </summary>
        internal static string GetAllOpenSyncJobs_SELECT {
            get {
                return ResourceManager.GetString("GetAllOpenSyncJobs_SELECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to with recursive children as (
        ///	-- roots
        ///	select * from (
        ///		select *
        ///		from sync_table
        ///		where parent_job_id is null and job_state in (&apos;new&apos;, &apos;inprogress&apos;)
        ///		limit 1
        ///	) as first_parent
        ///
        ///	union all
        ///	
        ///	-- children
        ///	select child.*
        ///	from sync_table as child
        ///	inner join sync_table parent on child.parent_job_id = parent.job_id
        ///)
        ///select * from children order by job_date desc;.
        /// </summary>
        internal static string GetFirstOpenSynJobAndChildren_SELECT {
            get {
                return ResourceManager.GetString("GetFirstOpenSynJobAndChildren_SELECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to select
        ///	*
        ///from
        ///	sync_table
        ///where
        ///	job_to_fso_can_sync = true
        ///    and (job_to_fso_sync_version is null or job_to_fso_sync_version &lt;&gt; job_last_change)
        ///order by
        ///	job_last_change desc
        ///limit 1;.
        /// </summary>
        internal static string GetFirstSyncJobToSync_SELECT {
            get {
                return ResourceManager.GetString("GetFirstSyncJobToSync_SELECT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DO $$ 
        ///    BEGIN
        ///        BEGIN
        ///            ALTER TABLE {0} ADD COLUMN {1} {2};
        ///        EXCEPTION
        ///            WHEN duplicate_column THEN RAISE NOTICE &apos;column {1} already exists in {0}.&apos;;
        ///        END;
        ///    END;
        ///$$.
        /// </summary>
        internal static string SetupAddColumn_SCRIPT {
            get {
                return ResourceManager.GetString("SetupAddColumn_SCRIPT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CREATE TABLE IF NOT EXISTS sync_table
        ///(
        /// -- SyncJob
        ///  job_id serial constraint synctable_primary_key primary key,
        ///  job_date timestamp without time zone not null,
        ///  
        ///  -- Sosync only
        ///  job_fs_id integer,
        ///  job_fso_id integer,
        ///  job_last_change timestamp without time zone,
        ///  
        ///  -- SyncJob source
        ///  job_source_system text,
        ///  job_source_model text,
        ///  job_source_record_id integer not null,
        ///  
        ///  -- SyncJob info
        ///  job_fetched timestamp without time zone,
        ///  job_start timestamp without time zone,
        /// [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string SetupDatabase_SCRIPT {
            get {
                return ResourceManager.GetString("SetupDatabase_SCRIPT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DO $$ 
        ///    BEGIN
        ///        BEGIN
        ///            ALTER TABLE {0} DROP COLUMN {1};
        ///        EXCEPTION
        ///            WHEN undefined_column THEN RAISE NOTICE &apos;column {1} does not exist in {0}.&apos;;
        ///        END;
        ///    END;
        ///$$.
        /// </summary>
        internal static string SetupDropColumn_SCRIPT {
            get {
                return ResourceManager.GetString("SetupDropColumn_SCRIPT", resourceCulture);
            }
        }
    }
}
