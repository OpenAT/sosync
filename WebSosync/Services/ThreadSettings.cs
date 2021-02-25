using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Common.Interfaces;
using WebSosync.Data.Models;

namespace WebSosync.Services
{
    public class ThreadSettings
        : IThreadSettings
    {
        private int _configuredMaxThreads;
        private int _configuredPackageSize;
        private int _currentMaxThreads;
        private int _currentPackageSize;
        private int? _targetMaxThreads;
        private int? _targetPackageSize;
        private DateTime? _targetMaxThreadsEnd;

        public int ConfiguredMaxThreads => _configuredMaxThreads;
        public int CurrentMaxThreads
        {
            get => _currentMaxThreads;
            set => _currentMaxThreads = value;
        }
        public int? TargetMaxThreads
        {
            get => _targetMaxThreads;
            set => _targetMaxThreads = value;
        }
        public DateTime? TargetMaxThreadsEnd
        {
            get => _targetMaxThreadsEnd;
            set => _targetMaxThreadsEnd = value;
        }

        public int ConfiguredPackageSize => _configuredPackageSize;

        public int CurrentPackageSize
        {
            get => _currentPackageSize;
            set => _currentPackageSize = value;
        }
        public int? TargetPackageSize 
        {
            get => _targetPackageSize;
            set => _targetPackageSize = value;
        }

        public ThreadSettings(SosyncOptions options)
        {
            _configuredMaxThreads = options.Max_Threads ?? 2;
            _configuredPackageSize = options.Job_Package_Size ?? 20;
        }

        public bool IsActive
        {
            get
            {
                var active = _targetMaxThreadsEnd.HasValue
                    && _targetMaxThreadsEnd >= DateTime.Now;

                if (active == false)
                {
                    _targetMaxThreads = null;
                    _targetPackageSize = null;
                    _targetMaxThreadsEnd = null;
                }

                return active;
            }
        }
    }
}
