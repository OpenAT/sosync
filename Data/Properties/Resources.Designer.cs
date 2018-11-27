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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("WebSosync.Data.Properties.Resources", typeof(Resources).Assembly);
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
        ///	update sosync_job
        ///	set
        ///		job_state = &apos;skipped&apos;
        ///		,job_log = @job_log
        ///		,job_closed_by_job_id = @job_closed_by_job_id
        ///		,write_date = @write_date
        ///		,job_start = now() at time zone &apos;utc&apos;
        ///		,job_end = now() at time zone &apos;utc&apos;
        ///	where
        ///		job_source_sosync_write_date &lt; @job_source_sosync_write_date
        ///		and job_source_system = @job_source_system
        ///		and job_source_model = @job_source_model
        ///		and job_source_record_id = @job_source_record_id
        ///		and job_state = &apos;new&apos;
        ///	returning id
        ///)        /// [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ClosePreviousJobs_Update_SCRIPT {
            get {
                return ResourceManager.GetString("ClosePreviousJobs_Update_SCRIPT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to with recursive children as (
        ///	-- roots
        ///	select * from (
        ///		select *
        ///		from sosync_job
        ///		where parent_job_id is null and job_state = &apos;new&apos;
        ///        order by job_priority desc, job_date desc
        ///        limit %LIMIT%
        ///	) first_parent
        ///
        ///	union all
        ///	
        ///	-- children
        ///	select jobs.*
        ///	from sosync_job jobs
        ///	inner join children c on c.id = jobs.parent_job_id
        ///)
        ///select * from children order by job_date asc;.
        /// </summary>
        internal static string GetFirstOpenSynJobAndChildren_SELECT {
            get {
                return ResourceManager.GetString("GetFirstOpenSynJobAndChildren_SELECT", resourceCulture);
            }
        }
    }
}
