####*UPDATE: On track for 1.0 release Monday Feb 3! Please follow [@AsyncPoco](https://twitter.com/AsyncPoco) on Twitter for further announcements.*

# AsyncPoco

## A tiny *async-y* ORM-ish thing for your POCOs

AsyncPoco is a fork of the popular [PetaPoco](http://www.toptensoftware.com/petapoco) micro-ORM for .NET, with a fully asynchronous API and support for the async/await keywords in C# 5.0. It does not supercede PetaPoco; the two can peacefully co-exist in the same project if both synchronous and asynchronous data access is needed.

## How do I use it?

If you're familiar with PetaPoco and the [TAP pattern](http://msdn.microsoft.com/en-us/library/hh873175.aspx) for asynchronous programming in .NET 4.5, you should easily be able to figure out how to use AsyncPoco. If you're new to PetaPoco, I highly recommend reading [their excellent tutorial](http://www.toptensoftware.com/petapoco) first. Then just note that the TAP pattern was followed consistently in porting PetaPoco's synchronous public methods to their async equivalents. In other words, all public methods that interact with the database were given an `Async` suffix, and instead of returning `void` or `T`, they return `Task` or `Task<T>`, respectively.

Here are some examples taken directly from the PetaPoco tutorial and converted to their AsyncPoco equivalent:

````C#
var db = new AsyncPoco.Database("connectionStringName");

var count = await db.ExecuteScalarAsync<long>("SELECT Count(*) FROM articles");
var a = await db.SingleOrDefaultAsync<Article>("SELECT * FROM articles WHERE article_id=@0", 123);
var result = await db.PageAsync<Article>(1, 20, // <-- page number and items per page
        "SELECT * FROM articles WHERE category=@0 ORDER BY date_posted DESC", "coolstuff");

await db.ExecuteAsync("DELETE FROM articles WHERE draft<>0");
await db.DeleteAsync<Article>("WHERE article_id=@0", 123);
await db.UpdateAsync<Article>("SET title=@0 WHERE article_id=@1", "New Title", 123);
await db.SaveAsync(a);
````

There is one case where the port from sync to async was not so straightforward: the `Query` method. In PetaPoco, `Query<T>` and its various overloads return `IEnumerable<T>`, and its implementation `yield return`s POCOs as it streams results from the underlying DataReader. But AsyncPoco's `QueryAsync<T>` methods do not return `Task<IEnumerable<T>>`. The reason is that if you await a method with that signature, you will not have results to work with until the Task completes, meaning all results are pulled into memory, at which point you may as well `Fetch` a `List<T>`. Ideally you want to get the results asynchronously *as they become available*. So instead of returning any sort of result that can be enumerated, `QueryAsync<T>` accepts a callback that is invoked for each poco in the result set.

Its usage looks like this:

````C#
await db.QueryAsync<Article>("SELECT * FROM articles", a =>
{
	Console.WriteLine("{0} - {1}", a.article_id, a.title);
});
````

What if you want to stop processing results before you reach the end of the DataReader's stream? There is a set of `QueryAsync<T>` overloads that take a `Func<T, bool>` callback; simply return `false` from the callback to hault the iteration immediately and close/dispose the `DataReader`.

````C#
await db.QueryAsync<Article>("SELECT * FROM articles", a =>
{
	if (IsWhatIWant(a))
	{
		Console.WriteLine("Found it! {0} - {1}", a.article_id, a.title);
		return false; // stop iterating and close/dispose the DataReader
	}
	else
	{
		return true; // continue iterating
	}
});
````

## What databases are supported?

All PetaPoco tests have been ported to their async equivalents and are passing with SQL Server 2008 R2, SQL Server CE, MySQL, and PostgreSQL. To my knowledge, AsyncPoco has not been deployed any production environment yet, but I intend to do so soon in an ASP.NET MVC / SQL Server 2008 R2 environment soon. For other platforms, proceed with caution for now and please let me know your findings - I'll update this section and gladly credit you for any help verifying platform support.

## When should I use it?

If you're finding that threads in your application are spending a significant percentage of CPU time waiting for database calls to return, you should notice big improvements with AsyncPoco. If you're already writing asynchronous code on .NET 4.5 and using a supported database platform, there's virtually no reason to prefer PetaPoco over AsyncPoco.

## When shouldn't I use it?

If you're not on .NET 4.5 or one of the supported database platforms, you're out of luck. Also bear in mind that if you're not already coding against asynchronous APIs using async/await and the TAP pattern, You may be committing yourself to a substantial number of changes to your code base. Going only partially async is an [invitation for deadlocks](http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html); you'll want to use async all the way up and down your call stack. If you're dealing with legacy code and don't have the time/resources to make that leap, AsyncPoco is probably not a good fit.

## Is it faster than PetaPoco?

No. But that's not the point of asynchronous code. The point is to free up threads while waiting on I/O-bound work to complete, making desktop and mobile apps more responsive and web applications more scalable. The context switching magic wired up by the compiler when async/await are used actually adds a small amount of overhead to the running code. Fortunately, some initial benchmarking shows no significant performance differences between PetaPoco and AsyncPoco.

## Is it ready for prime time?

Although all PetaPoco unit tests have been ported and are passing, this project is currently a work in progress and has <i>not</i> been thoroughly tested in any production environment. Ping me you're interested in contributing, testing, or otherwise helping to get this project to 1.0.