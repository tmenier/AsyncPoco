using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace AsyncPoco
{
	public interface IDatabase : IDisposable
	{
		/// <summary>
		/// When set to true the first opened connection is kept alive until this object is disposed
		/// </summary>
		bool KeepConnectionAlive { get; set; }

		/// <summary>
		/// Provides access to the currently open shared connection (or null if none)
		/// </summary>
		IDbConnection Connection { get; }

		/// <summary>
		/// Retrieves the SQL of the last executed statement
		/// </summary>
		string LastSQL { get; }

		/// <summary>
		/// Retrieves the arguments to the last execute statement
		/// </summary>
		object[] LastArgs { get; }

		/// <summary>
		/// Returns a formatted string describing the last executed SQL statement and it's argument values
		/// </summary>
		string LastCommand { get; }

		/// <summary>
		/// When set to true, PetaPoco will automatically create the "SELECT columns" part of any query that looks like it needs it
		/// </summary>
		bool EnableAutoSelect { get; set; }

		/// <summary>
		/// When set to true, parameters can be named ?myparam and populated from properties of the passed in argument values.
		/// </summary>
		bool EnableNamedParams { get; set; }

		/// <summary>
		/// Sets the timeout value for all SQL statements.
		/// </summary>
		int CommandTimeout { get; set; }

		/// <summary>
		/// Sets the timeout value for the next (and only next) SQL statement
		/// </summary>
		int OneTimeCommandTimeout { get; set; }

		/// <summary>
		/// Open a connection that will be used for all subsequent queries.
		/// </summary>
		/// <remarks>
		/// Calls to Open/CloseSharedConnection are reference counted and should be balanced
		/// </remarks>
		Task OpenSharedConnectionAsync();

		/// <summary>
		/// Releases the shared connection
		/// </summary>
		void CloseSharedConnection();

		/// <summary>
		/// Starts or continues a transaction.
		/// </summary>
		/// <returns>An ITransaction reference that must be Completed or disposed</returns>
		/// <remarks>
		/// This method makes management of calls to Begin/End/CompleteTransaction easier.  
		/// 
		/// The usage pattern for this should be:
		/// 
		/// using (var tx = db.GetTransaction())
		/// {
		///		// Do stuff
		///		db.Update(...);
		///		
		///     // Mark the transaction as complete
		///     tx.Complete();
		/// }
		/// 
		/// Transactions can be nested but they must all be completed otherwise the entire
		/// transaction is aborted.
		/// </remarks>
		Task<ITransaction> GetTransactionAsync();

	    /// <summary>
	    ///     Starts or continues a transaction.
	    /// </summary>
	    /// <returns>An ITransaction reference that must be Completed or disposed</returns>
	    /// <remarks>
	    ///     This method makes management of calls to Begin/End/CompleteTransaction easier.
	    ///     The usage pattern for this should be:
	    ///     using (var tx = db.GetTransaction(IsolationLevel.ReadUncommitted))
	    ///     {
	    ///       // Do stuff
	    ///       await db.QueryAsync(...);
	    ///       // Mark the transaction as complete
	    ///       tx.Complete();
	    ///     }
	    ///     Transactions can be nested but they must all be completed otherwise the entire
	    ///     transaction is aborted.
	    /// </remarks>
	    Task<ITransaction> GetTransactionAsync(IsolationLevel isolationLevel);

        /// <summary>
        /// Called when a transaction starts.  Overridden by the T4 template generated database
        /// classes to ensure the same DB instance is used throughout the transaction.
        /// </summary>
        void OnBeginTransaction();

		/// <summary>
		/// Called when a transaction ends.
		/// </summary>
		void OnEndTransaction();

		/// <summary>
		/// Starts a transaction scope, see GetTransaction() for recommended usage
		/// </summary>
		Task BeginTransactionAsync();

        /// <summary>
        /// Starts a transaction scope with a specific IsolationLevel, <see cref="GetTransactionAsync(IsolationLevel)"/> for recommended usage
        /// </summary>
        Task BeginTransactionAsync(IsolationLevel isolationLevel);

        /// <summary>
        /// Aborts the entire outer most transaction scope 
        /// </summary>
        /// <remarks>
        /// Called automatically by Transaction.Dispose()
        /// if the transaction wasn't completed.
        /// </remarks>
        void AbortTransaction();

		/// <summary>
		/// Marks the current transaction scope as complete.
		/// </summary>
		void CompleteTransaction();

		DbCommand CreateCommand(DbConnection connection, string sql, params object[] args);

		/// <summary>
		/// Called if an exception occurs during processing of a DB operation.  Override to provide custom logging/handling.
		/// </summary>
		/// <param name="x">The exception instance</param>
		/// <returns>True to re-throw the exception, false to suppress it</returns>
		bool OnException(Exception x);

		/// <summary>
		/// Called when DB connection opened
		/// </summary>
		/// <param name="conn">The newly opened DbConnection</param>
		/// <returns>The same or a replacement DbConnection</returns>
		/// <remarks>
		/// Override this method to provide custom logging of opening connection, or
		/// to provide a proxy DbConnection.
		/// </remarks>
		DbConnection OnConnectionOpened(DbConnection conn);

		/// <summary>
		/// Called when DB connection closed
		/// </summary>
		/// <param name="conn">The soon to be closed IDBConnection</param>
		void OnConnectionClosing(IDbConnection conn);

		/// <summary>
		/// Called just before an DB command is executed
		/// </summary>
		/// <param name="cmd">The command to be executed</param>
		/// <remarks>
		/// Override this method to provide custom logging of commands and/or
		/// modification of the IDbCommand before it's executed
		/// </remarks>
		void OnExecutingCommand(IDbCommand cmd);

		/// <summary>
		/// Called on completion of command execution
		/// </summary>
		/// <param name="cmd">The IDbCommand that finished executing</param>
		void OnExecutedCommand(IDbCommand cmd);

		/// <summary>
		/// Executes a non-query command
		/// </summary>
		/// <param name="sql">The SQL statement to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of rows affected</returns>
		Task<int> ExecuteAsync(string sql, params object[] args);

		/// <summary>
		/// Executes a non-query command
		/// </summary>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The number of rows affected</returns>
		Task<int> ExecuteAsync(Sql sql);

		/// <summary>
		/// Executes a query and return the first column of the first row in the result set.
		/// </summary>
		/// <typeparam name="T">The type that the result value should be cast to</typeparam>
		/// <param name="sql">The SQL query to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The scalar value cast to T</returns>
		Task<T> ExecuteScalarAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Executes a query and return the first column of the first row in the result set.
		/// </summary>
		/// <typeparam name="T">The type that the result value should be cast to</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The scalar value cast to T</returns>
		Task<T> ExecuteScalarAsync<T>(Sql sql);

		/// <summary>
		/// Runs a query and returns the result set as a typed list
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A List holding the results of the query</returns>
		Task<List<T>> FetchAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Runs a query and returns the result set as a typed list
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A List holding the results of the query</returns>
		Task<List<T>> FetchAsync<T>(Sql sql);

		/// <summary>
		/// Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sqlCount">The SQL to retrieve the total number of records</param>
		/// <param name="countArgs">Arguments to any embedded parameters in the sqlCount statement</param>
		/// <param name="sqlPage">The SQL To retrieve a single page of results</param>
		/// <param name="pageArgs">Arguments to any embedded parameters in the sqlPage statement</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		/// This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		/// The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page object.
		/// </remarks>
		Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, string sqlCount, object[] countArgs, string sqlPage, object[] pageArgs);

		/// <summary>
		/// Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.  It will also execute a second query to retrieve the
		/// total number of records in the result set.
		/// </remarks>
		Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, string sql, params object[] args);

		/// <summary>
		/// Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.  It will also execute a second query to retrieve the
		/// total number of records in the result set.
		/// </remarks>
		Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, Sql sql);

		/// <summary>
		/// Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sqlCount">An SQL builder object representing the SQL to retrieve the total number of records</param>
		/// <param name="sqlPage">An SQL builder object representing the SQL to retrieve a single page of results</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		/// This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		/// The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page object.
		/// </remarks>
		Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, Sql sqlCount, Sql sqlPage);

		/// <summary>
		/// Retrieves a page of records (without the total count)
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.
		/// </remarks>
		Task<List<T>> FetchAsync<T>(long page, long itemsPerPage, string sql, params object[] args);

		/// <summary>
		/// Retrieves a page of records (without the total count)
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.
		/// </remarks>
		Task<List<T>> FetchAsync<T>(long page, long itemsPerPage, Sql sql);

		/// <summary>
		/// Retrieves a range of records from result set
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows at the start of the result set to skip over</param>
		/// <param name="take">The number of rows to retrieve</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified range.
		/// </remarks>
		Task<List<T>> SkipTakeAsync<T>(long skip, long take, string sql, params object[] args);

		/// <summary>
		/// Retrieves a range of records from result set
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows at the start of the result set to skip over</param>
		/// <param name="take">The number of rows to retrieve</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified range.
		/// </remarks>
		Task<List<T>> SkipTakeAsync<T>(long skip, long take, Sql sql);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(string sql, Action<T> action);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(string sql, Func<T, bool> func);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(string sql, object[] args, Action<T> action);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(string sql, object[] args, Func<T, bool> func);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(Sql sql, Action<T> action);

		/// <summary>
		/// Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List.
		/// </remarks>
		Task QueryAsync<T>(Sql sql, Func<T, bool> func);

		/// <summary>
		/// Checks for the existance of a row matching the specified condition
		/// </summary>
		/// <typeparam name="T">The Type representing the table being queried</typeparam>
		/// <param name="sqlCondition">The SQL expression to be tested for (ie: the WHERE expression)</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>True if a record matching the condition is found.</returns>
		Task<bool> ExistsAsync<T>(string sqlCondition, params object[] args);

		/// <summary>
		/// Checks for the existance of a row with the specified primary key value.
		/// </summary>
		/// <typeparam name="T">The Type representing the table being queried</typeparam>
		/// <param name="primaryKey">The primary key value to look for</param>
		/// <returns>True if a record with the specified primary key value exists.</returns>
		Task<bool> ExistsAsync<T>(object primaryKey);

		/// <summary>
		/// Returns the record with the specified primary key value
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="primaryKey">The primary key value of the record to fetch</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one record with the specified primary key value.
		/// </remarks>
		Task<T> SingleAsync<T>(object primaryKey);

		/// <summary>
		/// Returns the record with the specified primary key value, or the default value if not found
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="primaryKey">The primary key value of the record to fetch</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		/// If there are no records with the specified primary key value, default(T) (typically null) is returned.
		/// </remarks>
		Task<T> SingleOrDefaultAsync<T>(object primaryKey);

		/// <summary>
		/// Runs a query that should always return a single row.
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one matching record
		/// </remarks>
		Task<T> SingleAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Runs a query that should always return either a single row, or no rows
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The single record matching the specified primary key value, or default(T) if no matching rows</returns>
		Task<T> SingleOrDefaultAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Runs a query that should always return at least one return
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The first record in the result set</returns>
		Task<T> FirstAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Runs a query and returns the first record, or the default value if no matching records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The first record in the result set, or default(T) if no matching rows</returns>
		Task<T> FirstOrDefaultAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Runs a query that should always return a single row.
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one matching record
		/// </remarks>
		Task<T> SingleAsync<T>(Sql sql);

		/// <summary>
		/// Runs a query that should always return either a single row, or no rows
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The single record matching the specified primary key value, or default(T) if no matching rows</returns>
		Task<T> SingleOrDefaultAsync<T>(Sql sql);

		/// <summary>
		/// Runs a query that should always return at least one return
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The first record in the result set</returns>
		Task<T> FirstAsync<T>(Sql sql);

		/// <summary>
		/// Runs a query and returns the first record, or the default value if no matching records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The first record in the result set, or default(T) if no matching rows</returns>
		Task<T> FirstOrDefaultAsync<T>(Sql sql);

		/// <summary>
		/// Performs an SQL Insert
		/// </summary>
		/// <param name="tableName">The name of the table to insert into</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record</returns>
		Task<object> InsertAsync(string tableName, string primaryKeyName, object poco);

		/// <summary>
		/// Performs an SQL Insert
		/// </summary>
		/// <param name="tableName">The name of the table to insert into</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="autoIncrement">True if the primary key is automatically allocated by the DB</param>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record, or null for non-auto-increment tables</returns>
		/// <remarks>Inserts a poco into a table.  If the poco has a property with the same name 
		/// as the primary key the id of the new record is assigned to it.  Either way,
		/// the new id is returned.</remarks>
		Task<object> InsertAsync(string tableName, string primaryKeyName, bool autoIncrement, object poco);

		/// <summary>
		/// Performs an SQL Insert
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record, or null for non-auto-increment tables</returns>
		/// <remarks>The name of the table, it's primary key and whether it's an auto-allocated primary key are retrieved
		/// from the POCO's attributes</remarks>
		Task<object> InsertAsync(object poco);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <returns>The number of affected records</returns>
		Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco, object primaryKeyValue);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco, object primaryKeyValue, IEnumerable<string> columns);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco, IEnumerable<string> columns);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(object poco, IEnumerable<string> columns);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(object poco);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(object poco, object primaryKeyValue);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync(object poco, object primaryKeyValue, IEnumerable<string> columns);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to update</typeparam>
		/// <param name="sql">The SQL update and condition clause (ie: everything after "UPDATE tablename"</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Performs an SQL update
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to update</typeparam>
		/// <param name="sql">An SQL builder object representing the SQL update and condition clause (ie: everything after "UPDATE tablename"</param>
		/// <returns>The number of affected rows</returns>
		Task<int> UpdateAsync<T>(Sql sql);

		/// <summary>
		/// Performs and SQL Delete
		/// </summary>
		/// <param name="tableName">The name of the table to delete from</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The POCO object whose primary key value will be used to delete the row</param>
		/// <returns>The number of rows affected</returns>
		Task<int> DeleteAsync(string tableName, string primaryKeyName, object poco);

		/// <summary>
		/// Performs and SQL Delete
		/// </summary>
		/// <param name="tableName">The name of the table to delete from</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The POCO object whose primary key value will be used to delete the row (or null to use the supplied primary key value)</param>
		/// <param name="primaryKeyValue">The value of the primary key identifing the record to be deleted (or null, or get this value from the POCO instance)</param>
		/// <returns>The number of rows affected</returns>
		Task<int> DeleteAsync(string tableName, string primaryKeyName, object poco, object primaryKeyValue);

		/// <summary>
		/// Performs an SQL Delete
		/// </summary>
		/// <param name="poco">The POCO object specifying the table name and primary key value of the row to be deleted</param>
		/// <returns>The number of rows affected</returns>
		Task<int> DeleteAsync(object poco);

		/// <summary>
		/// Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class whose attributes identify the table and primary key to be used in the delete</typeparam>
		/// <param name="pocoOrPrimaryKey">The value of the primary key of the row to delete</param>
		/// <returns></returns>
		Task<int> DeleteAsync<T>(object pocoOrPrimaryKey);

		/// <summary>
		/// Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to delete from</typeparam>
		/// <param name="sql">The SQL condition clause identifying the row to delete (ie: everything after "DELETE FROM tablename"</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of affected rows</returns>
		Task<int> DeleteAsync<T>(string sql, params object[] args);

		/// <summary>
		/// Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to delete from</typeparam>
		/// <param name="sql">An SQL builder object representing the SQL condition clause identifying the row to delete (ie: everything after "UPDATE tablename"</param>
		/// <returns>The number of affected rows</returns>
		Task<int> DeleteAsync<T>(Sql sql);

		/// <summary>
		/// Check if a poco represents a new row
		/// </summary>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The object instance whose "newness" is to be tested</param>
		/// <returns>True if the POCO represents a record already in the database</returns>
		/// <remarks>This method simply tests if the POCO's primary key column property has been set to something non-zero.</remarks>
		bool IsNew(string primaryKeyName, object poco);

		/// <summary>
		/// Check if a poco represents a new row
		/// </summary>
		/// <param name="poco">The object instance whose "newness" is to be tested</param>
		/// <returns>True if the POCO represents a record already in the database</returns>
		/// <remarks>This method simply tests if the POCO's primary key column property has been set to something non-zero.</remarks>
		bool IsNew(object poco);

		/// <summary>
		/// Saves a POCO by either performing either an SQL Insert or SQL Update
		/// </summary>
		/// <param name="tableName">The name of the table to be updated</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The POCO object to be saved</param>
		Task SaveAsync(string tableName, string primaryKeyName, object poco);

		/// <summary>
		/// Saves a POCO by either performing either an SQL Insert or SQL Update
		/// </summary>
		/// <param name="poco">The POCO object to be saved</param>
		Task SaveAsync(object poco);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, object[] args, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, object[] args, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql, object[] args, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql, Action<TRet> action);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2>(string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2, T3>(string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2, T3, T4>(string sql, params object[] args);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2>(string sql, object[] args, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3>(string sql, object[] args, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, T4>(string sql, object[] args, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2>(Sql sql);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2, T3>(Sql sql);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<T1>> FetchAsync<T1, T2, T3, T4>(Sql sql);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2>(Sql sql, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3>(Sql sql, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<T1, T2, T3, T4>(Sql sql, Action<T1> action);

		/// <summary>
		/// Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="TRet">The type of objects to pass to the action</typeparam>
		/// <param name="types">An array of Types representing the POCO types of the returned result set.</param>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		Task<List<TRet>> FetchAsync<TRet>(Type[] types, object cb, string sql, params object[] args);

		/// <summary>
		/// Performs a multi-poco query
		/// </summary>
		/// <typeparam name="TRet">The type of objects to pass to the action</typeparam>
		/// <param name="types">An array of Types representing the POCO types of the returned result set.</param>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		Task QueryAsync<TRet>(Type[] types, object cb, string sql, object[] args, Action<TRet> action);

		/// <summary>
		/// Formats the contents of a DB command for display
		/// </summary>
		/// <param name="cmd"></param>
		/// <returns></returns>
		string FormatCommand(IDbCommand cmd);

		/// <summary>
		/// Formats an SQL query and it's arguments for display
		/// </summary>
		/// <param name="sql"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		string FormatCommand(string sql, object[] args);
	}
}