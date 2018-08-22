using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Attributes
{
    /// <summary>
    /// Attribute for marking sync flows, that combine multiple models.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StudioMultiModelAttribute : Attribute
    {
    }
}
