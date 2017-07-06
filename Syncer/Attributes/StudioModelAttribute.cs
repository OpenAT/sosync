﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StudioModelAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
