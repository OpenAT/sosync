using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ModelPriorityAttribute : Attribute
    {
        public ModelPriorityAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; set; }
    }
}
