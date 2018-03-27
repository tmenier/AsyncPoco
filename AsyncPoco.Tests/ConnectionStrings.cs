using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncPoco.Tests
{
	/// <summary>
	/// Because some platforms don't support ConfigurationManager and thus can't (easily) read the app.config file.
	/// </summary>
	public static class ConnectionStrings
	{
		public static string Get(string name) {
			switch (name) {
				case "mysql": return @"server=localhost;database=asyncpoco;user id=asyncpoco;password=asyncpoco;Allow User Variables=true";
				case "sqlserver": return @"Server=(LocalDB)\MSSQLLocalDB; Integrated Security=True";
				case "sqlserverce": return @"Data Source=|DataDirectory|\petapoco.sdf";
				case "postgresql": return @"Server=127.0.0.1;User id=postgres;password=password01;Database=postgres;";
				case "sqlite": return @"Data Source=.\asyncpoco.sqlite;DateTimeKind=Utc";
				default: return null;
			}
		}
	}
}
