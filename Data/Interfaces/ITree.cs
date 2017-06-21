using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Interfaces
{
    public interface ITree<T>
    {
        int ID { get; }
        int? ParentID { get; }
        IList<T> Children { get; }
    }
}
