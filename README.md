# AsyncPoco

AsyncPoco is a fork of the popular PetaPoco micro-ORM for .NET, with a fully asynchronous API and broad cross-platform support, including .NET Core. If you're familiar with PetaPoco and the [TAP pattern](http://msdn.microsoft.com/en-us/library/hh873175.aspx) (i.e. `async`/`await`), the transition to AsyncPoco should be quite intuitive.

```C#
var db = new AsyncPoco.Database("connectionStringName");

var count = await db.ExecuteScalarAsync<long>("SELECT Count(*) FROM articles");
var a = await db.SingleOrDefaultAsync<Article>("SELECT * FROM articles WHERE article_id = @0", 123);
var result = await db.PageAsync<Article>(1, 20, // <-- page number and items per page
        "SELECT * FROM articles WHERE category = @0 ORDER BY date_posted DESC", "coolstuff");

await db.ExecuteAsync("DELETE FROM articles WHERE draft<>0");
await db.DeleteAsync<Article>("WHERE article_id = @0", 123);
await db.UpdateAsync<Article>("SET title = @0 WHERE article_id = @1", "New Title", 123);
await db.SaveAsync(a);
```

One imporant note is that **the constructor in the example above is not supported in .NET Core**. In a config file, a connection string generally includes a `providerName`, which resolves to a globally registered ADO.NET provider. Unfortunately, this functionality is absent in .NET Core, so AsyncPoco requires that you pass the provider a bit more directly. This is still pretty painless; either of these will work:

```c#
var db = Database.Create<MySqlConnection>("connectionString");
var db = Database.Create(() => new OracleConnection("connectionString"));
```

One case where the transition to AsyncPoco might be less straightforward is the `Query` method. In PetaPoco, `Query<T>` (and its various overloads) returns `IEnumerable<T>`, and its implementation `yield return`s POCOs as it streams results from the underlying DataReader. But AsyncPoco's `QueryAsync<T>` methods do not return `Task<IEnumerable<T>>`. The reason is that if you `await` a method with that signature, you will not have results to work with until the `Task` completes, meaning all results are pulled into memory, at which point you may as well `Fetch` a `List<T>`. Ideally, you want to be able to process the results asynchronously *as they become available*. So instead of returning a result that can be enumerated, `QueryAsync<T>` accepts a callback that is invoked for each POCO in the result set as it becomes available.

```C#
await db.QueryAsync<Article>("SELECT * FROM articles", a =>
{
	Console.WriteLine("{0} - {1}", a.article_id, a.title);
});
```

What if you want to stop processing results before you reach the end of the DataReader's stream? There is a set of `QueryAsync<T>` overloads that take a `Func<T, bool>` callback; simply return `false` from the callback to hault the iteration immediately and close/dispose the `DataReader`.

```C#
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
```

## What databases are supported?

AsyncPoco supports the following database platforms:

- SQL Server
- Oracle
- MySQL
- PostgreSQL
- SQLite
- SQL Server CE

## What flavors of .NET are supported?

AsyncPoco targets full .NET Framework as well as .NET Standard 1.3 and 2.0, meaning it will run on the following platforms:

- .NET Framework 4.5 and above
- .NET Core 1.0 and 2.0
- Mono
- Xamarin.iOS
- Xamarin.Mac
- Xamarin.Android
- UWP (Windows 10)

## Is it faster than PetaPoco?

No. But that's not the point of asynchronous code. The point is to free up threads while waiting on I/O-bound work to complete, making desktop and mobile apps more responsive and web applications more scalable.

## Why *shouldn't* I switch from PetaPoco?

Once you start converting synchronous code to async, it's said to spread like a zombie virus, meaning that if you're dealing with a large codebase, be prepared to make a substantial number of changes. If don't have the time or resources needed for this commitment, AsyncPoco is probably not a good fit. Going only partially async is an [invitation for deadlocks](http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html). A good rule of thumb is if you've used `.Wait()` or `.Result` anywhere in your code (other than perhaps the `Main` method of a console app), you've done something wrong. You need to either use async all the way up and down your call stack, or not at all.

## Where do I get it?

AsyncPoco is available via [NuGet](https://www.nuget.org/packages/AsyncPoco/):

`PM> Install-Package AsyncPoco`

## How do I get help?

- Ask specific programming questions on [Stack Overflow](http://stackoverflow.com/questions/ask?tags=asyncpoco+c%23+orm+micro-orm+async-await). I'll answer personally (unless someone beats me to it).
- For announcements and (light) discussions, follow [@AsyncPoco](https://twitter.com/AsyncPoco) on Twitter.
- To report bugs or suggest improvements, no matter how opinionated, [create an issue](https://github.com/tmenier/AsyncPoco/issues/new).

## How can I contribute?

I'll gladly accept pull requests that address issues and implement cool features, although I generally prefer that you [create an issue](https://github.com/tmenier/AsyncPoco/issues/new) first so we can discuss the specifics. I'd also be grateful for your help spreading the word via [Twitter](https://twitter.com/intent/tweet?text=Check%20out%20AsyncPoco!&tw_p=tweetbutton&url=https%3A%2F%2Fgithub.com%2Ftmenier%2FAsyncPoco), blog posts, etc.

## Credit where credit is due

Well over 90% of this code is the brainchild of Brad Robinson ([@toptensoftware](https://twitter.com/toptensoftware)); I'm merely riding the coattails of [PetaPoco](http://www.toptensoftware.com/petapoco)'s success. Brad in turn credits Rob Conery ([@robconery](https://twitter.com/robconery)) for original inspiration (ie: [Massive](https://github.com/robconery/massive)), Rob Sullivan ([@DataChomp](https://twitter.com/DataChomp)) for hard core DBA advice, and Adam Schroder ([@schotime](https://twitter.com/schotime)) for lots of suggestions, improvements and Oracle support.
