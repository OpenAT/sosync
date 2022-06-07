using System;

namespace Syncer.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SyncTargetStudioAttribute: Attribute
    { }
}
