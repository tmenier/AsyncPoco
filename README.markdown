# AsyncPoco

## A tiny *async-y* ORM-ish thing for your POCOs

AsyncPoco is a fork of the popular [PetaPoco](http://www.toptensoftware.com/petapoco) micro-ORM for .NET, with a fully asynchronous API and support for the async/await keywords in C# 5.0 and VB 11. It does not supercede PetaPoco; the two can peacefully co-exist in the same project. When making the decision to go asynchronous, it's generally best to go "all in", but keeping both around can be helpful while making a gradual transition.

## How do I use it?

If you're familiar with PetaPoco and the [TAP pattern](http://msdn.microsoft.com/en-us/library/hh873175.aspx) for asynchronous programming in .NET 4.5, you should easily be able to figure out how to use AsyncPoco. If you're new to PetaPoco, I highly recommend reading [the excellent tutorial](http://www.toptensoftware.com/petapoco) first. Then just note that the TAP pattern was followed consistently in porting PetaPoco's synchronous public methods to their async equivalents. In other words, all public methods that interact with the database were suffixed with `Async`, and instead of returning `void` or `T`, they return `Task` or `Task<T>`, respectively.

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

There is one case where the port from sync to async was not so straightforward: the `Query` method. In PetaPoco, `Query<T>` and its various overloads return `IEnumerable<T>`, and its implementation `yield return`s POCOs as it streams results from the underlying DataReader. But AsyncPoco's `QueryAsync<T>` methods do not return `Task<IEnumerable<T>>`. The reason is that if you `await` a method with that signature, you will not have results to work with until the `Task` completes, meaning all results are pulled into memory, at which point you may as well `Fetch` a `List<T>`. Ideally, you want to be able to process the results asynchronously *as they become available*. So instead of returning a result that can be enumerated, `QueryAsync<T>` accepts a callback that is invoked for each poco in the result set as it becomes available.

Example:

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

All PetaPoco tests have been ported to their async equivalents and are passing when run against SQL Server 2008 R2, SQL Server CE, MySQL, and PostgreSQL.

## Why should I use it?

If you're finding that threads in your application are spending a significant percentage of CPU time waiting for database calls to complete, you should notice big improvements with AsyncPoco. If you're already writing asynchronous code on .NET 4.5 and using a supported database platform, there's virtually no reason to prefer PetaPoco over AsyncPoco.

## Why shouldn't I use it?

If you're not on .NET 4.5 or one of the supported database platforms, you're out of luck. Also bear in mind that if you're not already coding against asynchronous APIs using async/await and the TAP pattern, You may be committing yourself to a substantial number of changes to your code base. Going only partially async is an [invitation for deadlocks](http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html); you'll want to use async all the way up and down your call stack. If you're dealing with legacy code and don't have the time or resources to make that leap, AsyncPoco is probably not a good fit.

## Besides async, are there any other functional differences between PetaPoco and AsyncPoco?

As of version 1.1, I've begun implementing a few features not found in PetaPoco, including support for nullable enums, composite primary keys (Ported from [NPoco](https://github.com/schotime/NPoco/wiki/Composite-Primary-Keys)), and the `[ComputedColumn]` marker attribute.

## Is it faster than PetaPoco?

No. But that's not the point of asynchronous code. The point is to free up threads while waiting on I/O-bound work to complete, making desktop and mobile apps more responsive and web applications more scalable. The context switching magic wired up by the compiler when async/await are used actually adds a small amount of overhead to the running code. I have done some informal benchmarking and saw no significant performance differences between PetaPoco and AsyncPoco.

## Where do I get it?

The recommended way to install AsyncPoco is via the [NuGet package](https://www.nuget.org/packages/AsyncPoco/).

`PM> Install-Package AsyncPoco`

Note that while the [single file](https://github.com/tmenier/AsyncPoco/blob/master/AsyncPoco/AsyncPoco.cs) approach and [T4 templates](https://github.com/tmenier/AsyncPoco/tree/master/AsyncPoco/T4%20Templates) have been carried over from PetaPoco and are supported, neither is currently installed via NuGet, so you'll need to grab them directly from the source code for now. I don't know if these are things that people want, so I encourage you to [create an issue](https://github.com/tmenier/AsyncPoco/issues/new) to request them and I'll consider adding them.

## How do I get help?

- Ask specific programming questions on [Stack Overflow](http://stackoverflow.com/questions/ask?tags=asyncpoco+c%23+orm+micro-orm+async-await). I'll answer personally (unless someone beats me to it).
- For announcements and (light) discussions, follow [@AsyncPoco](https://twitter.com/AsyncPoco) on Twitter.
- To report bugs or suggest improvements, no matter how opinionated, [create an issue](https://github.com/tmenier/AsyncPoco/issues/new).
- To contact me personally, email tmenier at that google mail service dot com.

## How do I contribute?

I'll gladly accept pull requests that address issues and implement cool features. I'd also be grateful for your help spreading the word via [Twitter](https://twitter.com/intent/tweet?text=Check%20out%20AsyncPoco!&tw_p=tweetbutton&url=https%3A%2F%2Fgithub.com%2Ftmenier%2FAsyncPoco), blog posts, etc.

## Credit where credit is due

Well over 90% of this code is the brainchild of Brad Robinson ([@toptensoftware](https://twitter.com/toptensoftware)); I'm merely riding the coattails of [PetaPoco](http://www.toptensoftware.com/petapoco)'s success. Brad in turn credits Rob Conery ([@robconery](https://twitter.com/robconery)) for original inspiration (ie: [Massive](https://github.com/robconery/massive)) and for use of [Subsonic](https://github.com/subsonic/SubSonic-3.0)'s T4 templates, Rob Sullivan ([@DataChomp](https://twitter.com/DataChomp)) for hard core DBA advice, and Adam Schroder ([@schotime](https://twitter.com/schotime)) for lots of suggestions, improvements and Oracle support. Adam's excellent [NPoco](https://github.com/schotime/NPoco) (another PetaPoco fork) was also the source of inspiration and code for some of the new 1.1 features.

