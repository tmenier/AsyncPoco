// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using AsyncPoco.Internal;


namespace AsyncPoco.DatabaseTypes
{
	class SQLiteDatabaseType : DatabaseType
	{
		public override object MapParameterValue(object value)
		{
			if (value.GetType() == typeof(uint))
				return (long)((uint)value);

			return base.MapParameterValue(value);
		}

		public override async Task<object> ExecuteInsertAsync(Database db, DbCommand cmd, string PrimaryKeyName)
		{
			if (PrimaryKeyName != null)
			{
				cmd.CommandText += ";\nSELECT last_insert_rowid();";
				return await db.ExecuteScalarHelperAsync(cmd);
			}
			else
			{
				await db.ExecuteNonQueryHelperAsync(cmd);
				return -1;
			}
		}

		public override string GetExistsSql()
		{
			return "SELECT EXISTS (SELECT 1 FROM {0} WHERE {1})";
		}

	}
}
