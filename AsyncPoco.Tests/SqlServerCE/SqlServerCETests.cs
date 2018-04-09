#if NET45
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlServerCe;

namespace AsyncPoco.Tests.SqlServerCE
{
	public class SqlServerCETests : DatabaseTests<SqlCeConnection>
	{
		protected override string ConnStrName { get; } = "sqlserverce";
		protected override string ConnStr { get; } = @"Data Source=|DataDirectory|\petapoco.sdf";
	}
}
#endif