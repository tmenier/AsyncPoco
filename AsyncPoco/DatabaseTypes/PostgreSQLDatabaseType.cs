// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using AsyncPoco.Internal;

namespace AsyncPoco.DatabaseTypes
{
	class PostgreSQLDatabaseType : DatabaseType
	{
		public override object MapParameterValue(object value)
		{
			// Don't map bools to ints in PostgreSQL
			if (value.GetType() == typeof(bool))
				return value;

			return base.MapParameterValue(value);
		}

		public override string EscapeSqlIdentifier(string str)
		{
			return string.Format("\"{0}\"", str);
		}

		public override async Task<object> ExecuteInsertAsync(Database db, DbCommand cmd, string PrimaryKeyName)
		{
			if (PrimaryKeyName != null && !PrimaryKeyName.Contains(","))
			{
				cmd.CommandText += string.Format("returning {0} as NewID", EscapeSqlIdentifier(PrimaryKeyName));
				return await db.ExecuteScalarHelperAsync(cmd);
			}
			else
			{
				await db.ExecuteNonQueryHelperAsync(cmd);
				return -1;
			}
		}
	}
}
