#if !NETCOREAPP1_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace AsyncPoco.Tests.SQLite
{
	public class SQLiteTests : DatabaseTests<SQLiteConnection>
	{
		protected override string ConnStrName { get; } = "sqlite";
		protected override string ConnStr { get; } = @"Data Source=.\asyncpoco.sqlite;DateTimeKind=Utc";
		protected override string DbProviderName { get; } = "System.Data.SQLite";
	}
}
#endif
