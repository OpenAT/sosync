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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
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
        ///   Looks up a localized string similar to with recursive children as (
        ///	-- roots
        ///	select *
        ///	from sync_table
        ///	where parent_job_id is null and state in (&apos;new&apos;, &apos;inprogress&apos;)
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
        ///  job_id serial,
        ///  job_date timestamp without time zone NOT NULL,
        ///  fetched timestamp without time zone,
        ///  start timestamp without time zone,
        ///  &quot;end&quot; timestamp without time zone,
        ///  state text NOT NULL,
        ///  error_code text,
        ///  parent_job_id integer,
        ///  child_start timestamp without time zone,
        ///  child_end timestamp without time zone,
        ///  source_system text NOT NULL,
        ///  source_model text NOT NULL,
        ///  source_record_id integer NOT NULL,
        ///  target_system text,
        ///  targ [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string SetupDatabase_SCRIPT {
            get {
                return ResourceManager.GetString("SetupDatabase_SCRIPT", resourceCulture);
            }
        }
    }
}
