using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Attributes
{
    /// <summary>
    /// Attribute to swap winner to Odoo, if a concurrent update was detected.
    /// Default winner is studio.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ConcurrencyOnlineWinsAttribute : Attribute
    {
    }
}
