using System;
using NUnit.Framework;

namespace AsyncPoco.Tests
{
    public class SqlBuilderTests
    {
        [Test]
        public void simple_append()
        {
            var sql = new Sql();
            sql.Append("LINE 1");
            sql.Append("LINE 2");
            sql.Append("LINE 3");

            Assert.AreEqual("LINE 1\nLINE 2\nLINE 3", sql.SQL);
            Assert.AreEqual(0, sql.Arguments.Length);
        }

        [Test]
        public void single_arg()
        {
            var sql = new Sql();
            sql.Append("arg @0", "a1");

            Assert.AreEqual("arg @0", sql.SQL);
            Assert.AreEqual(1, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
        }

        [Test]
        public void multiple_args()
        {
            var sql = new Sql();
            sql.Append("arg @0 @1", "a1", "a2");

            Assert.AreEqual("arg @0 @1", sql.SQL);
            Assert.AreEqual(2, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
            Assert.AreEqual("a2", sql.Arguments[1]);
        }

        [Test]
        public void unused_args()
        {
            var sql = new Sql();
            sql.Append("arg @0 @2", "a1", "a2", "a3");

            Assert.AreEqual("arg @0 @1", sql.SQL);
            Assert.AreEqual(2, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
            Assert.AreEqual("a3", sql.Arguments[1]);
        }

        [Test]
        public void unordered_args()
        {
            var sql = new Sql();
            sql.Append("arg @2 @1", "a1", "a2", "a3");

            Assert.AreEqual("arg @0 @1", sql.SQL);
            Assert.AreEqual(2, sql.Arguments.Length);
            Assert.AreEqual("a3", sql.Arguments[0]);
            Assert.AreEqual("a2", sql.Arguments[1]);
        }

        [Test]
        public void repeated_args()
        {
            var sql = new Sql();
            sql.Append("arg @0 @1 @0 @1", "a1", "a2");

            Assert.AreEqual("arg @0 @1 @2 @3", sql.SQL);
            Assert.AreEqual(4, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
            Assert.AreEqual("a2", sql.Arguments[1]);
            Assert.AreEqual("a1", sql.Arguments[2]);
            Assert.AreEqual("a2", sql.Arguments[3]);
        }

        [Test]
        public void mysql_user_vars()
        {
            var sql = new Sql();
            sql.Append("arg @@user1 @2 @1 @@@system1", "a1", "a2", "a3");

            Assert.AreEqual("arg @@user1 @0 @1 @@@system1", sql.SQL);
            Assert.AreEqual(2, sql.Arguments.Length);
            Assert.AreEqual("a3", sql.Arguments[0]);
            Assert.AreEqual("a2", sql.Arguments[1]);
        }

        [Test]
        public void named_args()
        {
            var sql = new Sql();
            sql.Append("arg @name @password", new { name = "n", password = "p" });

            Assert.AreEqual("arg @0 @1", sql.SQL);
            Assert.AreEqual(2, sql.Arguments.Length);
            Assert.AreEqual("n", sql.Arguments[0]);
            Assert.AreEqual("p", sql.Arguments[1]);
        }

        [Test]
        public void mixed_named_and_numbered_args()
        {
            var sql = new Sql();
            sql.Append("arg @0 @name @1 @password @2", "a1", "a2", "a3", new { name = "n", password = "p" });

            Assert.AreEqual("arg @0 @1 @2 @3 @4", sql.SQL);
            Assert.AreEqual(5, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
            Assert.AreEqual("n", sql.Arguments[1]);
            Assert.AreEqual("a2", sql.Arguments[2]);
            Assert.AreEqual("p", sql.Arguments[3]);
            Assert.AreEqual("a3", sql.Arguments[4]);
        }

        [Test]
        public void append_with_args()
        {
            var sql = new Sql();
            sql.Append("l1 @0", "a0");
            sql.Append("l2 @0", "a1");
            sql.Append("l3 @0", "a2");

            Assert.AreEqual("l1 @0\nl2 @1\nl3 @2", sql.SQL);
            Assert.AreEqual(3, sql.Arguments.Length);
            Assert.AreEqual("a0", sql.Arguments[0]);
            Assert.AreEqual("a1", sql.Arguments[1]);
            Assert.AreEqual("a2", sql.Arguments[2]);
        }

        [Test]
        public void append_with_args2()
        {
            var sql = new Sql();
            sql.Append("l1");
            sql.Append("l2 @0 @1", "a1", "a2");
            sql.Append("l3 @0", "a3");

            Assert.AreEqual("l1\nl2 @0 @1\nl3 @2", sql.SQL);
            Assert.AreEqual(3, sql.Arguments.Length);
            Assert.AreEqual("a1", sql.Arguments[0]);
            Assert.AreEqual("a2", sql.Arguments[1]);
            Assert.AreEqual("a3", sql.Arguments[2]);
        }

        [Test]
        public void invalid_arg_index()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var sql = new Sql();
                sql.Append("arg @0 @1", "a0");
                Assert.AreEqual("arg @0 @1", sql.SQL);
            });
        }

        [Test]
        public void invalid_arg_name()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var sql = new Sql();
                sql.Append("arg @name1 @name2", new { x = 1, y = 2 });
                Assert.AreEqual("arg @0 @1", sql.SQL);
            });
        }

        [Test]
        public void append_instances()
        {
            var sql = new Sql("l0 @0", "a0");
            var sql1 = new Sql("l1 @0", "a1");
            var sql2 = new Sql("l2 @0", "a2");

            Assert.AreSame(sql, sql.Append(sql1));
            Assert.AreSame(sql, sql.Append(sql2));

            Assert.AreEqual("l0 @0\nl1 @1\nl2 @2", sql.SQL);
            Assert.AreEqual(3, sql.Arguments.Length);
            Assert.AreEqual("a0", sql.Arguments[0]);
            Assert.AreEqual("a1", sql.Arguments[1]);
            Assert.AreEqual("a2", sql.Arguments[2]);
        }

        [Test]
        public void ConsecutiveWhere()
        {
            var sql = new Sql()
                .Append("SELECT * FROM blah");

            sql.Append("WHERE x");
            sql.Append("WHERE y");

            Assert.AreEqual("SELECT * FROM blah\nWHERE x\nAND y", sql.SQL);
        }

        [Test]
        public void ConsecutiveOrderBy()
        {
            var sql = new Sql()
                .Append("SELECT * FROM blah");

            sql.Append("ORDER BY x");
            sql.Append("ORDER BY y");

            Assert.AreEqual("SELECT * FROM blah\nORDER BY x\n, y", sql.SQL);
        }

        [Test]
        public void param_expansion_1()
        {
            // Simple collection parameter expansion
            var sql = Sql.Builder.Append("@0 IN (@1) @2", 20, new int[] { 1, 2, 3 }, 30);
            Assert.AreEqual("@0 IN (@1,@2,@3) @4", sql.SQL);
            Assert.AreEqual(5, sql.Arguments.Length);
            Assert.AreEqual(20, sql.Arguments[0]);
            Assert.AreEqual(1, sql.Arguments[1]);
            Assert.AreEqual(2, sql.Arguments[2]);
            Assert.AreEqual(3, sql.Arguments[3]);
            Assert.AreEqual(30, sql.Arguments[4]);
        }

        [Test]
        public void param_expansion_2()
        {
            // Out of order expansion
            var sql = Sql.Builder.Append("IN (@3) (@1)", null, new int[] { 1, 2, 3 }, null, new int[] { 4, 5, 6 });
            Assert.AreEqual("IN (@0,@1,@2) (@3,@4,@5)", sql.SQL);
            Assert.AreEqual(6, sql.Arguments.Length);
            Assert.AreEqual(4, sql.Arguments[0]);
            Assert.AreEqual(5, sql.Arguments[1]);
            Assert.AreEqual(6, sql.Arguments[2]);
            Assert.AreEqual(1, sql.Arguments[3]);
            Assert.AreEqual(2, sql.Arguments[4]);
            Assert.AreEqual(3, sql.Arguments[5]);
        }

        [Test]
        public void param_expansion_named()
        {
            // Expand a named parameter
            var sql = Sql.Builder.Append("IN (@numbers)", new { numbers = (new int[] { 1, 2, 3 }) });
            Assert.AreEqual("IN (@0,@1,@2)", sql.SQL);
            Assert.AreEqual(3, sql.Arguments.Length);
            Assert.AreEqual(1, sql.Arguments[0]);
            Assert.AreEqual(2, sql.Arguments[1]);
            Assert.AreEqual(3, sql.Arguments[2]);
        }

        [Test]
        public void select_two_columns()
        {
            var sql = Sql.Builder
                .Select("FirstName", "LastName");

            // TODO: May want to consider tests with spaces in column names (for each DB type).
            Assert.AreEqual("SELECT FirstName, LastName", sql.SQL);
        }

        [Test]
        public void left_join()
        {
            var sql = Sql.Builder
                .Select("*")
                .From("articles")
                .LeftJoin("comments").On("articles.article_id=comments.article_id");
            Assert.AreEqual("SELECT *\nFROM articles\nLEFT JOIN comments\nON articles.article_id=comments.article_id", sql.SQL);
        }
    }

}