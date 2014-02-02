// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AsyncPoco.Internal;

namespace AsyncPoco.DatabaseTypes
{
	class SqlServerCEDatabaseType : DatabaseType
	{
		public override string BuildPageQuery(long skip, long take, PagingHelper.SQLParts parts, ref object[] args)
		{
			var	sqlPage = string.Format("{0}\nOFFSET @{1} ROWS FETCH NEXT @{2} ROWS ONLY", parts.sql, args.Length, args.Length + 1);
			args = args.Concat(new object[] { skip, take }).ToArray();
			return sqlPage;
		}

		public override async Task<object> ExecuteInsertAsync(Database db, DbCommand cmd, string PrimaryKeyName)
		{
			await db.ExecuteNonQueryHelperAsync(cmd);
			return await db.ExecuteScalarAsync<object>("SELECT @@@IDENTITY AS NewID;");
		}

	}
}
