using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common.Interfaces
{
    public interface IThreadSettings
    {
        int ConfiguredMaxThreads { get; }
        int ConfiguredPackageSize { get; }
        int CurrentMaxThreads { get; set; }
        int CurrentPackageSize { get; set; }
        int? TargetMaxThreads { get; set; }
        int? TargetPackageSize{ get; set; }
        DateTime? TargetMaxThreadsEnd { get; set; }
        bool IsActive { get; }
    }
}
