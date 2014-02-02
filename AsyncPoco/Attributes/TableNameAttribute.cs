// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.
 
using System;

namespace AsyncPoco
{
	/// <summary>
	/// Sets the DB table name to be used for a Poco class.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class TableNameAttribute : Attribute
	{
		public TableNameAttribute(string tableName)
		{
			Value = tableName;
		}

		public string Value
		{
			get;
			private set;
		}
	}

}
