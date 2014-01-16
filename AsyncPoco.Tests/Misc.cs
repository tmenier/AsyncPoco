using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaTest;

namespace AsyncPoco.Tests
{
	[TestFixture]
	class Misc
	{
		Database db = new Database("sqlserver");

		[Test]
		public void EscapeColumnName()
		{
			Assert.AreEqual(db._dbType.EscapeSqlIdentifier("column.name"), "[column.name]");
			Assert.AreEqual(db._dbType.EscapeSqlIdentifier("column name"), "[column name]");
		}

		[Test]
		public void EscapeTableName()
		{
			Assert.AreEqual(db._dbType.EscapeTableName("column.name"), "column.name");
			Assert.AreEqual(db._dbType.EscapeTableName("column name"), "[column name]");
		}

		enum Fruits
		{
			Apples,
			Pears,
			Bananas,
		}

		enum Fruits2
		{
			Oranges,
			Berries,
		}

		[Test]
		public void EnumMapper()
		{
			Assert.AreEqual(Fruits.Apples, AsyncPoco.Internal.EnumMapper.EnumFromString(typeof(Fruits), "Apples"));
			Assert.AreEqual(Fruits.Pears, AsyncPoco.Internal.EnumMapper.EnumFromString(typeof(Fruits), "pears"));
			Assert.AreEqual(Fruits.Bananas, AsyncPoco.Internal.EnumMapper.EnumFromString(typeof(Fruits), "BANANAS"));

			Assert.AreEqual(Fruits2.Oranges, AsyncPoco.Internal.EnumMapper.EnumFromString(typeof(Fruits2), "Oranges"));

			Assert.Throws(typeof(Exception), () => AsyncPoco.Internal.EnumMapper.EnumFromString(typeof(Fruits2), "Apples"));
		}
	}
}
