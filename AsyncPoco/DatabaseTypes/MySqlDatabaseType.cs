// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using AsyncPoco.Internal;


namespace AsyncPoco.DatabaseTypes
{
	class MySqlDatabaseType : DatabaseType
	{
		public override string GetParameterPrefix(string ConnectionString)
		{
			if (ConnectionString != null && ConnectionString.IndexOf("Allow User Variables=true") >= 0)
				return "?";
			else
				return "@";
		}

		public override string EscapeSqlIdentifier(string str)
		{
			return string.Format("`{0}`", str);
		}

		public override string  GetExistsSql()
		{
 			return "SELECT EXISTS (SELECT 1 FROM {0} WHERE {1})";
		}
	}
}
