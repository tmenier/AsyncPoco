using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncPoco.Tests
{
    [TestFixture("sqlserver")]
    [TestFixture("sqlserverce")]
    [TestFixture("mysql")]
    [TestFixture("postgresql")]
    [TestFixture("sqlite")]    
    public class DatabaseTests
    {
        public DatabaseTests(string connectionStringName)
        {
            _connectionStringName = connectionStringName;
        }

        string _connectionStringName;
        Random r = new Random();
        Database db;

        [OneTimeSetUp]
        public async Task CreateDbAsync()
        {
            db = new Database(_connectionStringName);
            var all = Utils.LoadTextResource($"{typeof(DatabaseTests).Namespace}.{_connectionStringName}_init.sql");
            foreach (var sql in all.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
                await db.ExecuteAsync(sql);
        }

        [OneTimeTearDown]
        public async Task DeleteDbAsync()
        {
            var all = Utils.LoadTextResource($"{typeof(DatabaseTests).Namespace}.{_connectionStringName}_done.sql");
            foreach (var sql in all.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
                await db.ExecuteAsync(sql);
        }

        Task<long> GetRecordCountAsync()
        {
            return db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM petapoco");
        }

        [TearDown]
        public async Task TeardownAsync()
        {
            // Delete everything
            await db.DeleteAsync<deco>("");
            await db.DeleteAsync<petapoco2>("");

            // Should be clean
            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        poco CreatePoco()
        {
            // Need a rounded date as DB can't store milliseconds
            var now = DateTime.UtcNow;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            // Setup a record
            var o = new poco();
            o.title = string.Format("insert {0}", r.Next());
            o.draft = true;
            o.content = string.Format("insert {0}", r.Next());
            o.date_created = now;
            o.date_edited = now;
            o.state = State.Yes;
            o.state2 = null;
            o.col_w_space = 23;
            o.nullreal = 24;

            return o;
        }

        deco CreateDeco()
        {
            // Need a rounded date as DB can't store milliseconds
            var now = DateTime.UtcNow;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            // Setup a record
            var o = new deco();
            o.title = string.Format("insert {0}", r.Next());
            o.draft = true;
            o.content = string.Format("insert {0}", r.Next());
            o.date_created = now;
            o.date_edited = now;
            o.state = State.Maybe;
            o.state2 = State.No;
            o.col_w_space = 23;
            o.nullreal = 24;

            return o;
        }

        void AssertPocos(poco a, poco b)
        {
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.title, b.title);
            Assert.AreEqual(a.draft, b.draft);
            Assert.AreEqual(a.content, b.content);
            Assert.AreEqual(a.date_created, b.date_created);
            Assert.AreEqual(a.date_edited, b.date_edited);
            Assert.AreEqual(a.state, b.state);
            Assert.AreEqual(a.state2, b.state2);
            Assert.AreEqual(a.col_w_space, b.col_w_space);
            Assert.AreEqual(a.nullreal, b.nullreal);
        }

        void AssertPocos(deco a, deco b)
        {
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.title, b.title);
            Assert.AreEqual(a.draft, b.draft);
            Assert.AreEqual(a.content, b.content);
            Assert.AreEqual(a.date_created, b.date_created);
            Assert.AreEqual(a.state, b.state);
            Assert.AreEqual(a.state2, b.state2);
            Assert.AreEqual(a.col_w_space, b.col_w_space);
            Assert.AreEqual(a.nullreal, b.nullreal);
        }

        // Insert some records, return the id of the first
        async Task<long> InsertRecordsAsync(int count)
        {
            long lFirst = 0;
            for (int i = 0; i < count; i++)
            {
                var o = CreatePoco();
                await db.InsertAsync("petapoco", "id", o);

                var lc = db.LastCommand;

                if (i == 0)
                {
                    lFirst = o.id;
                    Assert.AreNotEqual(o.id, 0);
                }
            }

            return lFirst;
        }

        [Test]
        public async Task poco_Crud()
        {
            // Create a random record
            var o = CreatePoco();

            Assert.IsTrue(db.IsNew("id", o));

            // Insert it
            await db.InsertAsync("petapoco", "id", o);
            Assert.AreNotEqual(o.id, 0);

            Assert.IsFalse(db.IsNew("id", o));

            // Retrieve it
            var o2 = await db.SingleAsync<poco>("SELECT * FROM petapoco WHERE id=@0", o.id);

            Assert.IsFalse(db.IsNew("id", o2));

            // Check it
            AssertPocos(o, o2);

            // Update it
            o2.title = "New Title";
            await db.SaveAsync("petapoco", "id", o2);

            // Retrieve it again
            var o3 = await db.SingleAsync<poco>("SELECT * FROM petapoco WHERE id=@0", o.id);

            // Check it
            AssertPocos(o2, o3);

            // Delete it
            await db.DeleteAsync("petapoco", "id", o3);

            // Should be gone!
            var o4 = await db.SingleOrDefaultAsync<poco>("SELECT * FROM petapoco WHERE id=@0", o.id);
            Assert.IsNull(o4);
        }

        [Test]
        public async Task deco_Crud()
        {
            // Create a random record
            var o = CreateDeco();
            Assert.IsTrue(db.IsNew(o));

            // Insert it
            await db.InsertAsync(o);
            Assert.AreNotEqual(o.id, 0);

            Assert.IsFalse(db.IsNew(o));

            // Retrieve it
            var o2 = await db.SingleAsync<deco>("SELECT * FROM petapoco WHERE id=@0", o.id);

            Assert.IsFalse(db.IsNew(o2));

            // Check it
            AssertPocos(o, o2);

            // Update it
            o2.title = "New Title";
            await db.SaveAsync(o2);

            // Retrieve it again
            var o3 = await db.SingleAsync<deco>("SELECT * FROM petapoco WHERE id=@0", o.id);

            // Check it
            AssertPocos(o2, o3);

            // Delete it
            await db.DeleteAsync(o3);

            // Should be gone!
            var o4 = await db.SingleOrDefaultAsync<deco>("SELECT * FROM petapoco WHERE id=@0", o.id);
            Assert.IsNull(o4);
        }

        [Test]
        public async Task Fetch()
        {
            // Create some records
            const int count = 5;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.FetchAsync<poco>("SELECT * from petapoco ORDER BY id");
            Assert.AreEqual(r.Count, count);

            // Check em
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(r[i].id, id + i);
            }

        }

        [Test]
        public async Task Query()
        {
            // Create some records
            const int count = 5;
            long id = await InsertRecordsAsync(count);

            int i = 0;
            await db.QueryAsync<poco>("SELECT * from petapoco ORDER BY id", p =>
            {
                Assert.AreEqual(p.id, id + i);
                i++;
            });

            Assert.AreEqual(i, count);
        }

        [Test]
        public async Task Page()
        {
            // In this test we're checking that the page count is correct when there are
            // not-exactly pagesize*N records (ie: a partial page at the end)

            // Create some records
            const int count = 13;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.PageAsync<poco>(2, 5, "SELECT * from petapoco ORDER BY id");

            // Check em
            int i = 0;
            foreach (var p in r.Items)
            {
                Assert.AreEqual(p.id, id + i + 5);
                i++;
            }

            // Check other stats
            Assert.AreEqual(r.Items.Count, 5);
            Assert.AreEqual(r.CurrentPage, 2);
            Assert.AreEqual(r.ItemsPerPage, 5);
            Assert.AreEqual(r.TotalItems, 13);
            Assert.AreEqual(r.TotalPages, 3);
        }

        [Test]
        public async Task Page_NoOrderBy()
        {
            // Unordered paging not supported by Compact Edition
            if (_connectionStringName == "sqlserverce")
                return;
            // In this test we're checking that the page count is correct when there are
            // not-exactly pagesize*N records (ie: a partial page at the end)

            // Create some records
            const int count = 13;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.PageAsync<poco>(2, 5, "SELECT * from petapoco");

            // Check em
            int i = 0;
            foreach (var p in r.Items)
            {
                Assert.AreEqual(p.id, id + i + 5);
                i++;
            }

            // Check other stats
            Assert.AreEqual(r.Items.Count, 5);
            Assert.AreEqual(r.CurrentPage, 2);
            Assert.AreEqual(r.ItemsPerPage, 5);
            Assert.AreEqual(r.TotalItems, 13);
            Assert.AreEqual(r.TotalPages, 3);
        }

        [Test]
        public async Task Page_Distinct()
        {
            // Unordered paging not supported by Compact Edition
            if (_connectionStringName == "sqlserverce")
                return;
            // In this test we're checking that the page count is correct when there are
            // not-exactly pagesize*N records (ie: a partial page at the end)

            // Create some records
            const int count = 13;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.PageAsync<poco>(2, 5, "SELECT DISTINCT id from petapoco ORDER BY id");

            // Check em
            int i = 0;
            foreach (var p in r.Items)
            {
                Assert.AreEqual(p.id, id + i + 5);
                i++;
            }

            // Check other stats
            Assert.AreEqual(r.Items.Count, 5);
            Assert.AreEqual(r.CurrentPage, 2);
            Assert.AreEqual(r.ItemsPerPage, 5);
            Assert.AreEqual(r.TotalItems, 13);
            Assert.AreEqual(r.TotalPages, 3);
        }

        [Test]
        public async Task FetchPage()
        {
            // Create some records
            const int count = 13;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.FetchAsync<poco>(2, 5, "SELECT * from petapoco ORDER BY id");

            // Check em
            int i = 0;
            foreach (var p in r)
            {
                Assert.AreEqual(p.id, id + i + 5);
                i++;
            }

            // Check other stats
            Assert.AreEqual(r.Count, 5);
        }

        [Test]
        public async Task Page_boundary()
        {
            // In this test we're checking that the page count is correct when there are
            // exactly pagesize*N records.

            // Create some records
            const int count = 15;
            long id = await InsertRecordsAsync(count);

            // Fetch em
            var r = await db.PageAsync<poco>(3, 5, "SELECT * from petapoco ORDER BY id");

            // Check other stats
            Assert.AreEqual(r.Items.Count, 5);
            Assert.AreEqual(r.CurrentPage, 3);
            Assert.AreEqual(r.ItemsPerPage, 5);
            Assert.AreEqual(r.TotalItems, 15);
            Assert.AreEqual(r.TotalPages, 3);
        }

        [Test]
        public async Task deco_Delete()
        {
            // Create some records
            const int count = 15;
            long id = await InsertRecordsAsync(count);

            // Delete some
            await db.DeleteAsync<deco>("WHERE id>=@0", id + 5);

            // Check they match
            Assert.AreEqual(await GetRecordCountAsync(), 5);
        }

        [Test]
        public async Task deco_Update()
        {
            // Create some records
            const int count = 15;
            long id = await InsertRecordsAsync(count);

            // Update some
            await db.UpdateAsync<deco>("SET title=@0 WHERE id>=@1", "zap", id + 5);

            // Check some updated
            await db.QueryAsync<deco>("ORDER BY Id", d =>
            {
                if (d.id >= id + 5)
                {
                    Assert.AreEqual(d.title, "zap");
                }
                else
                {
                    Assert.AreNotEqual(d.title, "zap");
                }
            });
        }

        [Test]
        public async Task deco_ExplicitAttribute()
        {
            // Create a records
            long id = await InsertRecordsAsync(1);

            // Retrieve it in two different ways
            var a = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", id);
            var b = await db.SingleOrDefaultAsync<deco_explicit>("WHERE id=@0", id);
            var c = await db.SingleOrDefaultAsync<deco_explicit>("SELECT * FROM petapoco WHERE id=@0", id);

            // b record should have ignored the content
            Assert.IsNotNull(a.content);
            Assert.IsNull(b.content);
            Assert.IsNull(c.content);
        }

        [Test]
        public async Task deco_IgnoreAttribute()
        {
            // Create a records
            long id = await InsertRecordsAsync(1);

            // Retrieve it in two different ways
            var a = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", id);
            var b = await db.SingleOrDefaultAsync<deco_non_explicit>("WHERE id=@0", id);
            var c = await db.SingleOrDefaultAsync<deco_non_explicit>("SELECT * FROM petapoco WHERE id=@0", id);

            // b record should have ignored the content
            Assert.IsNotNull(a.content);
            Assert.IsNull(b.content);
            Assert.IsNull(c.content);
        }

        [Test]
        public async Task Transaction_complete()
        {
            using (var scope = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);
                scope.Complete();
            }

            Assert.AreEqual(await GetRecordCountAsync(), 10);
        }

        [Test]
        public async Task Transaction_cancelled()
        {
            using (var scope = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);
            }

            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        [Test]
        public async Task Transaction_nested_nn()
        {
            using (var scope1 = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);

                using (var scope2 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                }
            }

            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        [Test]
        public async Task Transaction_nested_yn()
        {
            using (var scope1 = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);

                using (var scope2 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                }
                scope1.Complete();
            }

            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        [Test]
        public async Task Transaction_nested_ny()
        {
            using (var scope1 = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);

                using (var scope2 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                    scope2.Complete();
                }
            }

            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        [Test]
        public async Task Transaction_nested_yy()
        {
            using (var scope1 = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);

                using (var scope2 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                    scope2.Complete();
                }

                scope1.Complete();
            }

            Assert.AreEqual(await GetRecordCountAsync(), 20);
        }

        [Test]
        public async Task Transaction_nested_yny()
        {
            using (var scope1 = await db.GetTransactionAsync())
            {
                await InsertRecordsAsync(10);

                using (var scope2 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                    //scope2.Complete();
                }

                using (var scope3 = await db.GetTransactionAsync())
                {
                    await InsertRecordsAsync(10);
                    scope3.Complete();
                }

                scope1.Complete();
            }

            Assert.AreEqual(await GetRecordCountAsync(), 0);
        }

        [Test]
        public async Task DateTimesAreUtc()
        {
            var id = await InsertRecordsAsync(1);
            var a2 = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", id);
            Assert.AreEqual(DateTimeKind.Utc, a2.date_created.Kind);
            Assert.AreEqual(DateTimeKind.Utc, a2.date_edited.Value.Kind);
        }

        [Test]
        public async Task DateTimeNullable()
        {
            // Need a rounded date as DB can't store milliseconds
            var now = DateTime.UtcNow;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            // Setup a record
            var a = new deco();
            a.title = string.Format("insert {0}", r.Next());
            a.draft = true;
            a.content = string.Format("insert {0}", r.Next());
            a.date_created = now;
            a.date_edited = null;

            await db.InsertAsync(a);

            // Retrieve it
            var b = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", a.id);
            Assert.AreEqual(b.id, a.id);
            Assert.AreEqual(b.date_edited.HasValue, false);

            // Update it to NULL
            b.date_edited = now;
            await db.UpdateAsync(b);
            var c = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", a.id);
            Assert.AreEqual(c.id, a.id);
            Assert.AreEqual(c.date_edited.HasValue, true);

            // Update it to not NULL
            c.date_edited = null;
            await db.UpdateAsync(c);
            var d = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", a.id);
            Assert.AreEqual(d.id, a.id);
            Assert.AreEqual(d.date_edited.HasValue, false);
        }

        [Test]
        public async Task NamedArgs()
        {
            long first = await InsertRecordsAsync(10);

            var items = await db.FetchAsync<deco>("WHERE id >= @min_id AND id <= @max_id",
                        new
                        {
                            min_id = first + 3,
                            max_id = first + 6
                        }
                    );
            Assert.AreEqual(items.Count, 4);
        }

        [Test]
        public async Task SingleOrDefault_Empty()
        {
            Assert.IsNull(await db.SingleOrDefaultAsync<deco>("WHERE id=@0", 0));
        }

        [Test]
        public async Task SingleOrDefault_Single()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.SingleOrDefaultAsync<deco>("WHERE id=@0", id));
        }

        [Test]
        public void SingleOrDefault_Multiple()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var id = await InsertRecordsAsync(2);
                await db.SingleOrDefaultAsync<deco>("WHERE id>=@0", id);
            });
        }

        [Test]
        public async Task FirstOrDefault_Empty()
        {
            Assert.IsNull(await db.FirstOrDefaultAsync<deco>("WHERE id=@0", 0));
        }

        [Test]
        public async Task FirstOrDefault_First()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.FirstOrDefaultAsync<deco>("WHERE id=@0", id));
        }

        [Test]
        public async Task FirstOrDefault_Multiple()
        {
            var id = await InsertRecordsAsync(2);
            Assert.IsNotNull(await db.FirstOrDefaultAsync<deco>("WHERE id>=@0", id));
        }

        [Test]
        public void Single_Empty()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await db.SingleAsync<deco>("WHERE id=@0", 0);
            });
        }

        [Test]
        public async Task Single_Single()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.SingleAsync<deco>("WHERE id=@0", id));
        }

        [Test]
        public void Single_Multiple()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var id = await InsertRecordsAsync(2);
                await db.SingleAsync<deco>("WHERE id>=@0", id);
            });
        }

        [Test]
        public void First_Empty()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await db.FirstAsync<deco>("WHERE id=@0", 0);
            });
        }

        [Test]
        public async Task First_First()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.FirstAsync<deco>("WHERE id=@0", id));
        }

        [Test]
        public async Task First_Multiple()
        {
            var id = await InsertRecordsAsync(2);
            Assert.IsNotNull(await db.FirstAsync<deco>("WHERE id>=@0", id));
        }

        [Test]
        public async Task SingleOrDefault_PK_Empty()
        {
            Assert.IsNull(await db.SingleOrDefaultAsync<deco>(0));
        }

        [Test]
        public async Task SingleOrDefault_PK_Single()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.SingleOrDefaultAsync<deco>(id));
        }

        [Test]
        public void Single_PK_Empty()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await db.SingleAsync<deco>(0);
            });
        }

        [Test]
        public async Task Single_PK_Single()
        {
            var id = await InsertRecordsAsync(1);
            Assert.IsNotNull(await db.SingleAsync<deco>(id));
        }

        [Test]
        public async Task AutoSelect_SelectPresent()
        {
            var id = await InsertRecordsAsync(1);
            var a = await db.SingleOrDefaultAsync<deco>("SELECT * FROM petapoco WHERE id=@0", id);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.id, id);
        }

        [Test]
        public async Task AutoSelect_SelectMissingFromMissing()
        {
            var id = await InsertRecordsAsync(1);
            var a = await db.SingleOrDefaultAsync<deco>("WHERE id=@0", id);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.id, id);
        }

        [Test]
        public async Task AutoSelect_SelectMissingFromPresent()
        {
            var id = await InsertRecordsAsync(1);
            var a = await db.SingleOrDefaultAsync<deco>("FROM petapoco WHERE id=@0", id);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.id, id);
        }

        void AssertDynamic(dynamic a, dynamic b)
        {
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.title, b.title);
            Assert.AreEqual(a.draft, b.draft);
            Assert.AreEqual(a.content, b.content);
            Assert.AreEqual(a.date_created, b.date_created);
            Assert.AreEqual(a.state, b.state);
            Assert.AreEqual(a.state2, b.state2);
        }

        dynamic CreateExpando()
        {
            // Need a rounded date as DB can't store milliseconds
            var now = DateTime.UtcNow;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            // Setup a record
            dynamic o = new System.Dynamic.ExpandoObject();
            o.title = string.Format("insert {0}", r.Next());
            o.draft = true;
            o.content = string.Format("insert {0}", r.Next());
            o.date_created = now;
            o.date_edited = now;
            o.state = (int)State.Maybe;
            o.state2 = (int?)null;

            return o;
        }

        [Test]
        public async Task Dynamic_Query()
        {
            // Create a random record
            var o = CreateExpando();

            Assert.IsTrue(db.IsNew("id", o));

            // Insert it
            await db.InsertAsync("petapoco", "id", o);
            Assert.AreNotEqual(o.id, 0);

            Assert.IsFalse(db.IsNew("id", o));

            // Retrieve it
            var o2 = await db.SingleAsync<dynamic>("SELECT * FROM petapoco WHERE id=@0", o.id);

            Assert.IsFalse(db.IsNew("id", o2));

            // Check it
            AssertDynamic(o, o2);

            // Update it
            o2.title = "New Title";
            await db.SaveAsync("petapoco", "id", o2);

            // Retrieve it again
            var o3 = await db.SingleAsync<dynamic>("SELECT * FROM petapoco WHERE id=@0", o.id);

            // Check it
            AssertDynamic(o2, o3);

            // Delete it
            await db.DeleteAsync("petapoco", "id", o3);

            // Should be gone!
            var o4 = await db.SingleOrDefaultAsync<dynamic>("SELECT * FROM petapoco WHERE id=@0", o.id);
            Assert.IsNull(o4);
        }

        [Test]
        public async Task Manual_PrimaryKey()
        {
            var o = new petapoco2();
            o.email = "blah@blah.com";
            o.name = "Mr Blah";
            await db.InsertAsync(o);

            var o2 = await db.SingleOrDefaultAsync<petapoco2>("WHERE email=@0", "blah@blah.com");
            Assert.AreEqual(o2.name, "Mr Blah");
        }

        [Test]
        public async Task SingleValueRequest()
        {
            var id = await InsertRecordsAsync(1);
            var id2 = await db.SingleOrDefaultAsync<long>("SELECT id from petapoco WHERE id=@0", id);
            Assert.AreEqual(id, id2);
        }

        [Test]
        public async Task Exists_Query_Does()
        {
            var id = await InsertRecordsAsync(10);
            Assert.IsTrue(await db.ExistsAsync<deco>("id = @0", id));
            Assert.IsTrue(await db.ExistsAsync<deco>(id));
        }

        [Test]
        public async Task Exists_Query_DoesNot()
        {
            var id = await InsertRecordsAsync(10);
            Assert.IsFalse(await db.ExistsAsync<deco>("id = @0", id + 100));
            Assert.IsFalse(await db.ExistsAsync<deco>(id + 100));
        }

        [Test]
        public async Task UpdateByObjectCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            await db.UpdateAsync("composite_pk", "id1,id2", new composite_pk { id1 = 2, id2 = 1, value = "buzz" });

            var rows = await db.FetchAsync<composite_pk>("SELECT * FROM composite_pk WHERE value = 'buzz'");
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(2, rows[0].id1);
            Assert.AreEqual(1, rows[0].id2);
        }

        [Test]
        public async Task UpdateByKeyCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            await db.UpdateAsync("composite_pk", "id1,id2", new { value = "buzz" }, new { id1 = 2, id2 = 1 });

            var rows = await db.FetchAsync<composite_pk>("SELECT * FROM composite_pk WHERE value = 'buzz'");
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(2, rows[0].id1);
            Assert.AreEqual(1, rows[0].id2);
        }

        [Test]
        public async Task UpdateByColumnsCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            await db.UpdateAsync("composite_pk", "id1,id2", new { value = "buzz" }, new { id1 = 2, id2 = 1 }, new[] { "value" });

            var rows = await db.FetchAsync<composite_pk>("SELECT * FROM composite_pk WHERE value = 'buzz'");
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(2, rows[0].id1);
            Assert.AreEqual(1, rows[0].id2);
        }

        [Test]
        public async Task DeleteByObjectCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 1, value = "buzz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            await db.DeleteAsync("composite_pk", "id1,id2", new composite_pk { id1 = 2, id2 = 1 });

            var rows = await db.FetchAsync<composite_pk>("SELECT * FROM composite_pk");
            Assert.AreEqual(3, rows.Count);
            Assert.IsTrue(rows.All(x => x.value == "fizz"));
        }

        [Test]
        public async Task DeleteByKeyCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 1, value = "buzz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            await db.DeleteAsync("composite_pk", "id1,id2", null, new { id1 = 2, id2 = 1 });

            var rows = await db.FetchAsync<composite_pk>("SELECT * FROM composite_pk");
            Assert.AreEqual(3, rows.Count);
            Assert.IsTrue(rows.All(x => x.value == "fizz"));
        }

        [Test]
        public async Task ExistsCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            Assert.IsTrue(await db.ExistsAsync<composite_pk>(new { id1 = 1, id2 = 2 }));
            Assert.IsFalse(await db.ExistsAsync<composite_pk>(new { id1 = 2, id2 = 1 }));
        }

        [Test]
        public async Task SingleCompositePK()
        {
            await db.ExecuteAsync("DELETE FROM composite_pk");
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 1, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 1, id2 = 2, value = "fizz" });
            await db.InsertAsync(new composite_pk { id1 = 2, id2 = 2, value = "fizz" });

            Assert.IsNotNull(await db.SingleAsync<composite_pk>(new { id1 = 1, id2 = 2 }));
        }

        [Test]
        public async Task NullableInt()
        {
            var x = await db.ExecuteScalarAsync<int?>(Sql.Builder.Select("NULL"));
            var y = await db.ExecuteScalarAsync<int?>(Sql.Builder.Select("8"));
            Assert.AreEqual(null, x);
            Assert.AreEqual(8, y);
        }
    }
}
