using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Interfaces
{
    public interface IAuditable
    {
        DateTime? CreateDate { get; set; }
        DateTime? WriteDate { get; set; }
    }
}
