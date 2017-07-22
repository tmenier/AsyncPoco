using NUnit.Framework;
using System.Collections.Generic;

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
			Bananas
		}

		enum Fruits2
		{
			Oranges
		}

		[Test]
		public void EnumMapper()
		{
			Assert.AreEqual(Fruits.Apples, Internal.EnumMapper.EnumFromString(typeof(Fruits), "Apples"));
			Assert.AreEqual(Fruits.Pears, Internal.EnumMapper.EnumFromString(typeof(Fruits), "pears"));
			Assert.AreEqual(Fruits.Bananas, Internal.EnumMapper.EnumFromString(typeof(Fruits), "BANANAS"));
			Assert.AreEqual(Fruits2.Oranges, Internal.EnumMapper.EnumFromString(typeof(Fruits2), "Oranges"));

			// nullable enums
			Assert.AreEqual(Fruits.Apples, Internal.EnumMapper.EnumFromString(typeof(Fruits?), "Apples"));
			Assert.AreEqual(null, Internal.EnumMapper.EnumFromString(typeof(Fruits?), null));

			Assert.Throws<KeyNotFoundException>(() => Internal.EnumMapper.EnumFromString(typeof(Fruits2), "Apples"));
		}
	}
}
