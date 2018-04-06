using NUnit.Framework;
using System;
using System.Reflection;

namespace AsyncPoco.Tests
{
	public class Poco2
	{
		public string prop1 { get; set; }
		public string prop2 { get; set; }
		public string prop3 { get; set; }
		public string prop4 { get; set; }
	    public string prop5 { get; set; }
	    public string prop6 { get; set; }
    }

	public class MyColumnMapper : AsyncPoco.IMapper
	{
		public TableInfo GetTableInfo(Type t)
		{
			var ti = TableInfo.FromPoco(t);

			if (t == typeof(Poco2))
			{
				ti.TableName = "petapoco";
				ti.PrimaryKey = "id";
			}

			return ti;
		}
		public ColumnInfo GetColumnInfo(System.Reflection.PropertyInfo pi)
		{
			var ci = ColumnInfo.FromProperty(pi);
			if (ci == null)
				return null;

			if (pi.DeclaringType == typeof(Poco2))
			{
				switch (pi.Name)
				{
					case "prop1":
						// Leave this property as is
						break;

					case "prop2":
						// Rename this column
						ci.ColumnName = "remapped2";
						break;

					case "prop3":
						// Mark this as a result column
						ci.ResultColumn = true;
						break;

					case "prop4":
						// Ignore this property
						return null;

                    case "prop5":
                        ci.InsertTemplate = "custom";
                        break;

                    case "prop6":
                        ci.UpdateTemplate = "custom";
                        break;
				}
			}

			// Do default property mapping
			return ci;
		}


		public Func<object, object> GetFromDbConverter(System.Reflection.PropertyInfo pi, Type SourceType)
		{
			return null;
		}

		public Func<object, object> GetToDbConverter(PropertyInfo SourceProperty)
		{
			return null;
		}
	}

	[TestFixture]
	public class ColumnMapper
	{


		[Test]
		public void NoColumnMapper()
		{

			AsyncPoco.Mappers.Register(Assembly.GetExecutingAssembly(), new MyColumnMapper());
			var pd = AsyncPoco.Internal.PocoData.ForType(typeof(Poco2));

			Assert.AreEqual(pd.Columns.Count, 3);
			Assert.AreEqual(pd.Columns["prop1"].PropertyInfo.Name, "prop1");
			Assert.AreEqual(pd.Columns["remapped2"].ColumnName, "remapped2");
			Assert.AreEqual(pd.Columns["prop3"].ColumnName, "prop3");
			Assert.AreEqual(pd.Columns["prop5"].InsertTemplate, "custom");
			Assert.AreEqual(pd.Columns["prop6"].UpdateTemplate, "custom");
            Assert.AreEqual(string.Join(", ", pd.QueryColumns), "prop1, remapped2");
			Assert.AreEqual(pd.TableInfo.PrimaryKey, "id");
			Assert.AreEqual(pd.TableInfo.TableName, "petapoco");

			AsyncPoco.Mappers.Revoke(Assembly.GetExecutingAssembly());
		}
	}
}
