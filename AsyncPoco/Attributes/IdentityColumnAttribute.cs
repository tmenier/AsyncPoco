// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;

namespace AsyncPoco
{

    /// <summary>
    /// Marks a poco property as an identity column.
    /// Not used on inserts
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IdentityColumnAttribute : ColumnAttribute
    {
        public IdentityColumnAttribute()
        {
        }

        public IdentityColumnAttribute(string name)
            : base(name)
        {
        }
    }

}
