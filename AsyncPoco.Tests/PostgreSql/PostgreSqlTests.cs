#if !NETCOREAPP1_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace AsyncPoco.Tests.PostgreSql
{
	public class PostgreSqlTests : DatabaseTests<NpgsqlConnection>
	{
		protected override string ConnStrName { get; } = "postgresql";
		protected override string ConnStr { get; } = @"Server=127.0.0.1;User id=postgres;password=password01;Database=postgres;";
		protected override string DbProviderName { get; } = "Npgsql";
	}
}
#endif
