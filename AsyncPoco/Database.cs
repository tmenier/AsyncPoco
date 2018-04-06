/* AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
 *
 * PetaPoco - A Tiny ORMish thing for your POCO's.
 * Copyright © 2011-2012 Topten Software.  All Rights Reserved.
 * 
 * Apache License 2.0 - http://www.toptensoftware.com/petapoco/license
 * 
 * Special thanks to Rob Conery (@robconery) for original inspiration (ie:Massive) and for 
 * use of Subsonic's T4 templates, Rob Sullivan (@DataChomp) for hard core DBA advice 
 * and Adam Schroder (@schotime) for lots of suggestions, improvements and Oracle support
 */

// Define PETAPOCO_NO_DYNAMIC in your project settings on .NET 3.5

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncPoco.Internal;

namespace AsyncPoco
{
	/// <summary>
	///     The main Database class.  You can either use this class directly, or derive from it.
	/// </summary>
	public class Database : IDatabase
	{
		private static IEqualityComparer<string> _columnComparer = StringComparer.OrdinalIgnoreCase;
		public static IEqualityComparer<string> ColumnComparer
		{
			get { return _columnComparer; }
			set
			{
				_columnComparer = value;
				MultiPocoFactory.FieldNameComparer = value;
				PocoData.ColumnComparer = value;
				EnumMapper.FieldComparer = value;
			}
		}

		#region IDisposable

		/// <summary>
		///     Automatically close one open shared connection
		/// </summary>
		public void Dispose()
		{
			// Automatically close one open connection reference
			//  (Works with KeepConnectionAlive and manually opening a shared connection)
			CloseSharedConnection();
		}

		#endregion

		#region Constructors

		public static Database Create<TDbConnection>(string connectionString) where TDbConnection : DbConnection, new() {
			var dbType = DatabaseType.Resolve(typeof(TDbConnection).Name, null);
			return new Database(dbType, () => new TDbConnection { ConnectionString = connectionString });
		}

		public static Database Create<TDbConnection>(Func<TDbConnection> createConnection) where TDbConnection : DbConnection, new() {
			var dbType = DatabaseType.Resolve(typeof(TDbConnection).Name, null);
			return new Database(dbType, createConnection);
		}

		internal Database(DatabaseType dbType, Func<DbConnection> createConnection) {
			_dbType = dbType;
			_connectionFactory = createConnection;
			CommonConstruct();
		}

		/// <summary>
		///     Construct a database using a supplied DbConnection
		/// </summary>
		/// <param name="connection">The DbConnection to use</param>
		/// <remarks>
		///     The supplied DbConnection will not be closed/disposed by PetaPoco - that remains
		///     the responsibility of the caller.
		/// </remarks>
		public Database(DbConnection connection)
		{
			_dbType = DatabaseType.Resolve(connection.GetType().Name, null);
			_sharedConnection = connection;
			_sharedConnectionDepth = 2; // Prevent closing external connection
			CommonConstruct();
		}

#if (NET45)
		/// <summary>
		///     Construct a database using a supplied connections string and optionally a provider name
		/// </summary>
		/// <param name="connectionString">The DB connection string</param>
		/// <param name="providerName">The name of the DB provider to use</param>
		/// <remarks>
		///     PetaPoco will automatically close and dispose any connections it creates.
		/// </remarks>
		public Database(string connectionString, string providerName)
		{
			_dbType = DatabaseType.Resolve(null, providerName);

			_connectionFactory = () => {
				var cn = DbProviderFactories.GetFactory(providerName).CreateConnection();
				cn.ConnectionString = connectionString;
				return cn;
			};

			CommonConstruct();
		}

		/// <summary>
		///     Construct a Database using a supplied connection string and a DbProviderFactory
		/// </summary>
		/// <param name="connectionString">The connection string to use</param>
		/// <param name="provider">The DbProviderFactory to use for instantiating DbConnection's</param>
		public Database(string connectionString, DbProviderFactory provider)
		{
			_dbType = DatabaseType.Resolve(provider.GetType().Name, null);

			_connectionFactory = () => {
				var cn = provider.CreateConnection();
				cn.ConnectionString = connectionString;
				return cn;
			};

			CommonConstruct();
		}

		/// <summary>
		///     Construct a Database using a supplied connectionString Name.  The actual connection string and provider will be
		///     read from app/web.config.
		/// </summary>
		/// <param name="connectionStringName">The name of the connection</param>
		public Database(string connectionStringName) {
			if (string.IsNullOrEmpty(connectionStringName))
				throw new ArgumentNullException(nameof(connectionStringName));

			var cs = System.Configuration.ConfigurationManager.ConnectionStrings[connectionStringName];
			if (cs == null)
				throw new ArgumentException($"Can't find a connection string with the name '{connectionStringName}'");

			_dbType = DatabaseType.Resolve(null, cs.ProviderName);

			_connectionFactory = () => {
				var cn = DbProviderFactories.GetFactory(cs.ProviderName).CreateConnection();
				cn.ConnectionString = cs.ConnectionString;
				return cn;
			};

			CommonConstruct();
		}
#endif

		/// <summary>
		///     Provides common initialization for the various constructors
		/// </summary>
		private void CommonConstruct()
		{
			// Reset
			_transactionDepth = 0;
			EnableAutoSelect = true;
			EnableNamedParams = true;
		}

#endregion

#region Connection Management

		/// <summary>
		///     When set to true the first opened connection is kept alive until this object is disposed
		/// </summary>
		public bool KeepConnectionAlive { get; set; }

		/// <summary>
		///     Open a connection that will be used for all subsequent queries.
		/// </summary>
		/// <remarks>
		///     Calls to Open/CloseSharedConnection are reference counted and should be balanced
		/// </remarks>
		public virtual async Task OpenSharedConnectionAsync()
		{
			if (_sharedConnectionDepth == 0)
			{
				_sharedConnection = _connectionFactory();

				if (_sharedConnection.State == ConnectionState.Broken)
					_sharedConnection.Close();

				if (_sharedConnection.State == ConnectionState.Closed)
					await _sharedConnection.OpenAsync();

				_sharedConnection = OnConnectionOpened(_sharedConnection);

				if (KeepConnectionAlive)
					_sharedConnectionDepth++; // Make sure you call Dispose
			}
			_sharedConnectionDepth++;
		}

		/// <summary>
		///     Releases the shared connection
		/// </summary>
		public void CloseSharedConnection()
		{
			if (_sharedConnectionDepth > 0)
			{
				_sharedConnectionDepth--;
				if (_sharedConnectionDepth == 0)
				{
					OnConnectionClosing(_sharedConnection);
					_sharedConnection.Dispose();
					_sharedConnection = null;
				}
			}
		}

		/// <summary>
		///     Provides access to the currently open shared connection (or null if none)
		/// </summary>
		public DbConnection Connection
		{
			get { return _sharedConnection; }
		}

#endregion

#region Transaction Management

		// Helper to create a transaction scope

		/// <summary>
		///     Starts or continues a transaction.
		/// </summary>
		/// <returns>An ITransaction reference that must be Completed or disposed</returns>
		/// <remarks>
		///     This method makes management of calls to Begin/End/CompleteTransaction easier.
		///     The usage pattern for this should be:
		///     using (var tx = db.GetTransaction())
		///     {
		///     // Do stuff
		///     db.Update(...);
		///     // Mark the transaction as complete
		///     tx.Complete();
		///     }
		///     Transactions can be nested but they must all be completed otherwise the entire
		///     transaction is aborted.
		/// </remarks>
		public Task<ITransaction> GetTransactionAsync()
		{
			return Transaction.BeginAsync(this);
		}

		/// <summary>
		///     Called when a transaction starts.  Overridden by the T4 template generated database
		///     classes to ensure the same DB instance is used throughout the transaction.
		/// </summary>
		public virtual void OnBeginTransaction()
		{
		}

		/// <summary>
		///     Called when a transaction ends.
		/// </summary>
		public virtual void OnEndTransaction()
		{
		}

		/// <summary>
		///     Starts a transaction scope, see GetTransaction() for recommended usage
		/// </summary>
		public virtual async Task BeginTransactionAsync()
		{
			_transactionDepth++;

			if (_transactionDepth == 1)
			{
				await OpenSharedConnectionAsync();
				_transaction = _sharedConnection.BeginTransaction();
				_transactionCancelled = false;
				OnBeginTransaction();
			}
		}

		/// <summary>
		///     Internal helper to cleanup transaction
		/// </summary>
		protected virtual void CleanupTransaction()
		{
			OnEndTransaction();

			if (_transactionCancelled)
				_transaction.Rollback();
			else
				_transaction.Commit();

			_transaction.Dispose();
			_transaction = null;

			CloseSharedConnection();
		}

		/// <summary>
		///     Aborts the entire outer most transaction scope
		/// </summary>
		/// <remarks>
		///     Called automatically by Transaction.Dispose()
		///     if the transaction wasn't completed.
		/// </remarks>
		public void AbortTransaction()
		{
			_transactionCancelled = true;
			if (--_transactionDepth == 0)
				CleanupTransaction();
		}

		/// <summary>
		///     Marks the current transaction scope as complete.
		/// </summary>
		public void CompleteTransaction()
		{
			if (--_transactionDepth == 0)
				CleanupTransaction();
		}

#endregion

#region Command Management

		/// <summary>
		///     Add a parameter to a DB command
		/// </summary>
		/// <param name="cmd">A reference to the IDbCommand to which the parameter is to be added</param>
		/// <param name="value">The value to assign to the parameter</param>
		/// <param name="pi">Optional, a reference to the property info of the POCO property from which the value is coming.</param>
		private void AddParam(IDbCommand cmd, object value, PropertyInfo pi)
		{
			// Convert value to from poco type to db type
			if (pi != null)
			{
				var mapper = Mappers.GetMapper(pi.DeclaringType);
				var fn = mapper.GetToDbConverter(pi);
				if (fn != null)
					value = fn(value);
			}

			// Support passed in parameters
			var idbParam = value as IDbDataParameter;
			if (idbParam != null)
			{
				idbParam.ParameterName = string.Format("{0}{1}", _dbType.ParameterPrefix, cmd.Parameters.Count);
				cmd.Parameters.Add(idbParam);
				return;
			}

			// Create the parameter
			var p = cmd.CreateParameter();
			p.ParameterName = string.Format("{0}{1}", _dbType.ParameterPrefix, cmd.Parameters.Count);

			// Assign the parmeter value
			if (value == null)
			{
				p.Value = DBNull.Value;
			}
			else
			{
				// Give the database type first crack at converting to DB required type
				value = _dbType.MapParameterValue(value);

				var t = value.GetType();
				if (t.GetTypeInfo().IsEnum) // PostgreSQL .NET driver wont cast enum to int
				{
					p.Value = (int)value;
				}
				else if (t == typeof(Guid))
				{
					p.Value = value.ToString();
					p.DbType = DbType.String;
					p.Size = 40;
				}
				else if (t == typeof(string))
				{
					// out of memory exception occurs if trying to save more than 4000 characters to SQL Server CE NText column. Set before attempting to set Size, or Size will always max out at 4000
					if ((value as string).Length + 1 > 4000 && p.GetType().Name == "SqlCeParameter")
						p.GetType().GetProperty("SqlDbType").SetValue(p, SqlDbType.NText, null);

					p.Size = Math.Max((value as string).Length + 1, 4000); // Help query plan caching by using common size
					p.Value = value;
				}
				else if (t == typeof(AnsiString))
				{
					// Thanks @DataChomp for pointing out the SQL Server indexing performance hit of using wrong string type on varchar
					p.Size = Math.Max((value as AnsiString).Value.Length + 1, 4000);
					p.Value = (value as AnsiString).Value;
					p.DbType = DbType.AnsiString;
				}
				else if (value.GetType().Name == "SqlGeography") //SqlGeography is a CLR Type
				{
					p.GetType().GetProperty("UdtTypeName").SetValue(p, "geography", null);
					//geography is the equivalent SQL Server Type
					p.Value = value;
				}

				else if (value.GetType().Name == "SqlGeometry") //SqlGeometry is a CLR Type
				{
					p.GetType().GetProperty("UdtTypeName").SetValue(p, "geometry", null); //geography is the equivalent SQL Server Type
					p.Value = value;
				}
				else
				{
					p.Value = value;
				}
			}

			// Add to the collection
			cmd.Parameters.Add(p);
		}

		// Create a command
		private static readonly Regex rxParamsPrefix = new Regex(@"(?<!@)@\w+", RegexOptions.Compiled);

		public DbCommand CreateCommand(DbConnection connection, string sql, params object[] args)
		{
			// Perform named argument replacements
			if (EnableNamedParams)
			{
				var new_args = new List<object>();
				sql = ParametersHelper.ProcessParams(sql, args, new_args);
				args = new_args.ToArray();
			}

			// Perform parameter prefix replacements
			if (_dbType.ParameterPrefix != "@")
				sql = rxParamsPrefix.Replace(sql, m => _dbType.ParameterPrefix + m.Value.Substring(1));
			sql = sql.Replace("@@", "@"); // <- double @@ escapes a single @

			// Create the command and add parameters
			var cmd = connection.CreateCommand();
			cmd.Connection = connection;
			cmd.CommandText = sql;
			cmd.Transaction = _transaction;
			foreach (var item in args)
			{
				AddParam(cmd, item, null);
			}

			// Notify the DB type
			_dbType.PreExecute(cmd);

			// Call logging
			if (!string.IsNullOrEmpty(sql))
				DoPreExecute(cmd);

			return cmd;
		}

#endregion

#region Exception Reporting and Logging

		/// <summary>
		///     Called if an exception occurs during processing of a DB operation.  Override to provide custom logging/handling.
		/// </summary>
		/// <param name="x">The exception instance</param>
		/// <returns>True to re-throw the exception, false to suppress it</returns>
		public virtual bool OnException(Exception x)
		{
			Debug.WriteLine(x.ToString());
			Debug.WriteLine(LastCommand);
			return true;
		}

		/// <summary>
		///     Called when DB connection opened
		/// </summary>
		/// <param name="conn">The newly opened DbConnection</param>
		/// <returns>The same or a replacement DbConnection</returns>
		/// <remarks>
		///     Override this method to provide custom logging of opening connection, or
		///     to provide a proxy DbConnection.
		/// </remarks>
		public virtual DbConnection OnConnectionOpened(DbConnection conn)
		{
			return conn;
		}

		/// <summary>
		///     Called when DB connection closed
		/// </summary>
		/// <param name="conn">The soon to be closed IDBConnection</param>
		public virtual void OnConnectionClosing(DbConnection conn)
		{
		}

		/// <summary>
		///     Called just before an DB command is executed
		/// </summary>
		/// <param name="cmd">The command to be executed</param>
		/// <remarks>
		///     Override this method to provide custom logging of commands and/or
		///     modification of the IDbCommand before it's executed
		/// </remarks>
		public virtual void OnExecutingCommand(IDbCommand cmd)
		{
		}

		/// <summary>
		///     Called on completion of command execution
		/// </summary>
		/// <param name="cmd">The IDbCommand that finished executing</param>
		public virtual void OnExecutedCommand(IDbCommand cmd)
		{
		}

#endregion

#region operation: Execute 

		/// <summary>
		///     Executes a non-query command
		/// </summary>
		/// <param name="sql">The SQL statement to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of rows affected</returns>
		public virtual async Task<int> ExecuteAsync(string sql, params object[] args)
		{
			try
			{
				await OpenSharedConnectionAsync();
				try
				{
					using (var cmd = CreateCommand(_sharedConnection, sql, args))
					{
						var retv = await cmd.ExecuteNonQueryAsync();
						OnExecutedCommand(cmd);
						return retv;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch (Exception x)
			{
				if (OnException(x))
					throw;
				return -1;
			}
		}

		/// <summary>
		///     Executes a non-query command
		/// </summary>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The number of rows affected</returns>
		public Task<int> ExecuteAsync(Sql sql)
		{
			return ExecuteAsync(sql.SQL, sql.Arguments);
		}

#endregion

#region operation: ExecuteScalarAsync

		/// <summary>
		///     Executes a query and return the first column of the first row in the result set.
		/// </summary>
		/// <typeparam name="T">The type that the result value should be cast to</typeparam>
		/// <param name="sql">The SQL query to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The scalar value cast to T</returns>
		public virtual async Task<T> ExecuteScalarAsync<T>(string sql, params object[] args)
		{
			try
			{
				await OpenSharedConnectionAsync();
				try
				{
					using (var cmd = CreateCommand(_sharedConnection, sql, args))
					{
						var val = await cmd.ExecuteScalarAsync();
						OnExecutedCommand(cmd);

						// Handle nullable types
						var u = Nullable.GetUnderlyingType(typeof(T));
						if (u != null && (val == null || val == DBNull.Value))
							return default(T);

						return (T)Convert.ChangeType(val, u ?? typeof(T));
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch (Exception x)
			{
				if (OnException(x))
					throw;
				return default(T);
			}
		}

		/// <summary>
		///     Executes a query and return the first column of the first row in the result set.
		/// </summary>
		/// <typeparam name="T">The type that the result value should be cast to</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The scalar value cast to T</returns>
		public Task<T> ExecuteScalarAsync<T>(Sql sql)
		{
			return ExecuteScalarAsync<T>(sql.SQL, sql.Arguments);
		}

#endregion

#region operation: Fetch

		/// <summary>
		///     Runs a query and returns the result set as a typed list
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query to execute</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A List holding the results of the query</returns>
		public async Task<List<T>> FetchAsync<T>(string sql, params object[] args)
		{
			var list = new List<T>();
			await QueryAsync<T>(sql, args, v => list.Add(v));
			return list;
		}

		/// <summary>
		///     Runs a query and returns the result set as a typed list
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A List holding the results of the query</returns>
		public Task<List<T>> FetchAsync<T>(Sql sql)
		{
			return FetchAsync<T>(sql.SQL, sql.Arguments);
		}

#endregion

#region operation: Page

		/// <summary>
		///     Starting with a regular SELECT statement, derives the SQL statements required to query a
		///     DB for a page of records and the total number of records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows to skip before the start of the page</param>
		/// <param name="take">The number of rows in the page</param>
		/// <param name="sql">The original SQL select statement</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="sqlCount">Outputs the SQL statement to query for the total number of matching rows</param>
		/// <param name="sqlPage">Outputs the SQL statement to retrieve a single page of matching rows</param>
		private void BuildPageQueries<T>(long skip, long take, string sql, ref object[] args, out string sqlCount,
			out string sqlPage)
		{
			// Add auto select clause
			if (EnableAutoSelect)
				sql = AutoSelectHelper.AddSelectClause<T>(_dbType, sql);

			// Split the SQL
			PagingHelper.SQLParts parts;
			if (!PagingHelper.SplitSQL(sql, out parts))
				throw new Exception("Unable to parse SQL statement for paged query");

			sqlPage = _dbType.BuildPageQuery(skip, take, parts, ref args);
			sqlCount = parts.sqlCount;
		}

		/// <summary>
		///     Retrieves a page of records	and the total number of available records
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
		///     This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		///     The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page
		///     object.
		/// </remarks>
		public async Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, string sqlCount, object[] countArgs,
			string sqlPage, object[] pageArgs)
		{
			// Save the one-time command time out and use it for both queries
			var saveTimeout = OneTimeCommandTimeout;

			// Setup the paged result
			var result = new Page<T>
			{
				CurrentPage = page,
				ItemsPerPage = itemsPerPage,
				TotalItems = await ExecuteScalarAsync<long>(sqlCount, countArgs)
			};
			result.TotalPages = result.TotalItems / itemsPerPage;

			if (result.TotalItems % itemsPerPage != 0)
				result.TotalPages++;

			OneTimeCommandTimeout = saveTimeout;

			// Get the records
			result.Items = await FetchAsync<T>(sqlPage, pageArgs);

			// Done
			return result;
		}

		/// <summary>
		///     Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified page.  It will also execute a second query to retrieve the
		///     total number of records in the result set.
		/// </remarks>
		public Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, string sql, params object[] args)
		{
			string sqlCount, sqlPage;
			BuildPageQueries<T>((page - 1) * itemsPerPage, itemsPerPage, sql, ref args, out sqlCount, out sqlPage);
			return PageAsync<T>(page, itemsPerPage, sqlCount, args, sqlPage, args);
		}

		/// <summary>
		///     Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified page.  It will also execute a second query to retrieve the
		///     total number of records in the result set.
		/// </remarks>
		public Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, Sql sql)
		{
			return PageAsync<T>(page, itemsPerPage, sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Retrieves a page of records	and the total number of available records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sqlCount">An SQL builder object representing the SQL to retrieve the total number of records</param>
		/// <param name="sqlPage">An SQL builder object representing the SQL to retrieve a single page of results</param>
		/// <returns>A Page of results</returns>
		/// <remarks>
		///     This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		///     The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page
		///     object.
		/// </remarks>
		public Task<Page<T>> PageAsync<T>(long page, long itemsPerPage, Sql sqlCount, Sql sqlPage)
		{
			return PageAsync<T>(page, itemsPerPage, sqlCount.SQL, sqlCount.Arguments, sqlPage.SQL, sqlPage.Arguments);
		}

#endregion

#region operation: Fetch (page)

		/// <summary>
		///     Retrieves a page of records (without the total count)
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified page.
		/// </remarks>
		public Task<List<T>> FetchAsync<T>(long page, long itemsPerPage, string sql, params object[] args)
		{
			return SkipTakeAsync<T>((page - 1) * itemsPerPage, itemsPerPage, sql, args);
		}

		/// <summary>
		///     Retrieves a page of records (without the total count)
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="page">The 1 based page number to retrieve</param>
		/// <param name="itemsPerPage">The number of records per page</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified page.
		/// </remarks>
		public Task<List<T>> FetchAsync<T>(long page, long itemsPerPage, Sql sql)
		{
			return SkipTakeAsync<T>((page - 1) * itemsPerPage, itemsPerPage, sql.SQL, sql.Arguments);
		}

#endregion

#region operation: SkipTakeAsync

		/// <summary>
		///     Retrieves a range of records from result set
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows at the start of the result set to skip over</param>
		/// <param name="take">The number of rows to retrieve</param>
		/// <param name="sql">The base SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified range.
		/// </remarks>
		public Task<List<T>> SkipTakeAsync<T>(long skip, long take, string sql, params object[] args)
		{
			string sqlCount, sqlPage;
			BuildPageQueries<T>(skip, take, sql, ref args, out sqlCount, out sqlPage);
			return FetchAsync<T>(sqlPage, args);
		}

		/// <summary>
		///     Retrieves a range of records from result set
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows at the start of the result set to skip over</param>
		/// <param name="take">The number of rows to retrieve</param>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <returns>A List of results</returns>
		/// <remarks>
		///     PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		///     records for the specified range.
		/// </remarks>
		public Task<List<T>> SkipTakeAsync<T>(long skip, long take, Sql sql)
		{
			return SkipTakeAsync<T>(skip, take, sql.SQL, sql.Arguments);
		}

#endregion

#region operation: Query

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public Task QueryAsync<T>(string sql, Action<T> action)
		{
			return QueryAsync(sql, null, action);
		}

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public Task QueryAsync<T>(string sql, Func<T, bool> func)
		{
			return QueryAsync(sql, null, func);
		}

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public Task QueryAsync<T>(string sql, object[] args, Action<T> action)
		{
			return QueryAsync<T>(sql, args, v =>
			{
				action(v);
				return true;
			});
		}

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public virtual async Task QueryAsync<T>(string sql, object[] args, Func<T, bool> func)
		{
			if (EnableAutoSelect)
				sql = AutoSelectHelper.AddSelectClause<T>(_dbType, sql);

			await OpenSharedConnectionAsync();
			try
			{
				using (var cmd = CreateCommand(_sharedConnection, sql, args))
				{
					DbDataReader r;
					var pd = PocoData.ForType(typeof(T));
					try
					{
						r = await cmd.ExecuteReaderAsync();
						OnExecutedCommand(cmd);
					}
					catch (Exception x)
					{
						if (OnException(x))
							throw;

						return;
					}
					var factory =
						pd.GetFactory(cmd.CommandText, _sharedConnection.ConnectionString, 0, r.FieldCount, r) as Func<IDataReader, T>;
					using (r)
					{
						var keepGoing = true;
						while (keepGoing)
						{
							T poco;
							try
							{
								if (!await r.ReadAsync())
									break;

								poco = factory(r);
							}
							catch (Exception x)
							{
								if (OnException(x))
									throw;

								break;
							}

							keepGoing = func(poco);
						}
					}
				}
			}
			finally
			{
				CloseSharedConnection();
			}
		}

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public Task QueryAsync<T>(Sql sql, Action<T> action)
		{
			return QueryAsync(sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Runs an SQL query, asynchronously passing each result to a callback
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the base SQL query and it's arguments</param>
		/// <param name="func">Callback to process each result, return false to stop iterating</param>
		/// <remarks>
		///     For some DB providers, care should be taken to not start a new Query before finishing with
		///     and disposing the previous one. In cases where this is an issue, consider using Fetch which
		///     returns the results as a List.
		/// </remarks>
		public Task QueryAsync<T>(Sql sql, Func<T, bool> func)
		{
			return QueryAsync(sql.SQL, sql.Arguments, func);
		}

#endregion

#region operation: Exists

		/// <summary>
		///     Checks for the existance of a row matching the specified condition
		/// </summary>
		/// <typeparam name="T">The Type representing the table being queried</typeparam>
		/// <param name="sqlCondition">The SQL expression to be tested for (ie: the WHERE expression)</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>True if a record matching the condition is found.</returns>
		public async Task<bool> ExistsAsync<T>(string sqlCondition, params object[] args)
		{
			var poco = PocoData.ForType(typeof(T)).TableInfo;
			var result = await ExecuteScalarAsync<int>(string.Format(_dbType.GetExistsSql(), poco.TableName, sqlCondition), args);
			return result != 0;
		}

		/// <summary>
		///     Checks for the existance of a row with the specified primary key value.
		/// </summary>
		/// <typeparam name="T">The Type representing the table being queried</typeparam>
		/// <param name="primaryKey">The primary key value to look for</param>
		/// <returns>True if a record with the specified primary key value exists.</returns>
		public Task<bool> ExistsAsync<T>(object primaryKey)
		{
			var index = 0;
			var pk = GetPrimaryKeyValues(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey, primaryKey);
			return ExistsAsync<T>(BuildPrimaryKeySql(pk, ref index), pk.Select(x => x.Value).ToArray());
		}

#endregion

#region operation: linq style (Exists, Single, SingleOrDefault etc...)

		/// <summary>
		///     Returns the record with the specified primary key value
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="primaryKey">The primary key value of the record to fetch</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		///     Throws an exception if there are zero or more than one record with the specified primary key value.
		/// </remarks>
		public Task<T> SingleAsync<T>(object primaryKey)
		{
			var index = 0;
			var pk = GetPrimaryKeyValues(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey, primaryKey);
			return SingleAsync<T>("WHERE " + BuildPrimaryKeySql(pk, ref index), pk.Select(x => x.Value).ToArray());
		}

		/// <summary>
		///     Returns the record with the specified primary key value, or the default value if not found
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="primaryKey">The primary key value of the record to fetch</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		///     If there are no records with the specified primary key value, default(T) (typically null) is returned.
		/// </remarks>
		public Task<T> SingleOrDefaultAsync<T>(object primaryKey)
		{
			var index = 0;
			var pk = GetPrimaryKeyValues(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey, primaryKey);
			return SingleOrDefaultAsync<T>("WHERE " + BuildPrimaryKeySql(pk, ref index), pk.Select(x => x.Value).ToArray());
		}

		/// <summary>
		///     Runs a query that should always return a single row.
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		///     Throws an exception if there are zero or more than one matching record
		/// </remarks>
		public async Task<T> SingleAsync<T>(string sql, params object[] args)
		{
			var count = 0;
			var poco = default(T);
			await QueryAsync<T>(sql, args, v =>
			{
				poco = v;
				count++;
				return count <= 2;
			});
			if (count == 0)
				throw new InvalidOperationException("Sequence contains no elements.");
			if (count > 1)
				throw new InvalidOperationException("Sequence contains more than one element.");

			return poco;
		}

		/// <summary>
		///     Runs a query that should always return either a single row, or no rows
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The single record matching the specified primary key value, or default(T) if no matching rows</returns>
		public async Task<T> SingleOrDefaultAsync<T>(string sql, params object[] args)
		{
			var count = 0;
			var poco = default(T);
			await QueryAsync<T>(sql, args, v =>
			{
				poco = v;
				count++;
				return count <= 2;
			});
			if (count > 1)
				throw new InvalidOperationException("Sequence contains more than one element.");

			return poco;
		}

		/// <summary>
		///     Runs a query that should always return at least one return
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The first record in the result set</returns>
		public async Task<T> FirstAsync<T>(string sql, params object[] args)
		{
			var gotIt = false;
			var poco = default(T);
			await QueryAsync<T>(sql, args, v =>
			{
				poco = v;
				gotIt = true;
				return false;
			});
			if (!gotIt)
				throw new InvalidOperationException("Sequence contains no elements.");

			return poco;
		}

		/// <summary>
		///     Runs a query and returns the first record, or the default value if no matching records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">The SQL query</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL statement</param>
		/// <returns>The first record in the result set, or default(T) if no matching rows</returns>
		public async Task<T> FirstOrDefaultAsync<T>(string sql, params object[] args)
		{
			var poco = default(T);
			await QueryAsync<T>(sql, args, v =>
			{
				poco = v;
				return false;
			});
			return poco;
		}

		/// <summary>
		///     Runs a query that should always return a single row.
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The single record matching the specified primary key value</returns>
		/// <remarks>
		///     Throws an exception if there are zero or more than one matching record
		/// </remarks>
		public Task<T> SingleAsync<T>(Sql sql)
		{
			return SingleAsync<T>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Runs a query that should always return either a single row, or no rows
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The single record matching the specified primary key value, or default(T) if no matching rows</returns>
		public Task<T> SingleOrDefaultAsync<T>(Sql sql)
		{
			return SingleOrDefaultAsync<T>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Runs a query that should always return at least one return
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The first record in the result set</returns>
		public Task<T> FirstAsync<T>(Sql sql)
		{
			return FirstAsync<T>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Runs a query and returns the first record, or the default value if no matching records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>The first record in the result set, or default(T) if no matching rows</returns>
		public Task<T> FirstOrDefaultAsync<T>(Sql sql)
		{
			return FirstOrDefaultAsync<T>(sql.SQL, sql.Arguments);
		}

#endregion

#region operation: Insert

		/// <summary>
		///     Performs an SQL Insert
		/// </summary>
		/// <param name="tableName">The name of the table to insert into</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record</returns>
		public Task<object> InsertAsync(string tableName, string primaryKeyName, object poco)
		{
			return InsertAsync(tableName, primaryKeyName, true, poco);
		}

		/// <summary>
		///     Performs an SQL Insert
		/// </summary>
		/// <param name="tableName">The name of the table to insert into</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="autoIncrement">True if the primary key is automatically allocated by the DB</param>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record, or null for non-auto-increment tables</returns>
		/// <remarks>
		///     Inserts a poco into a table.  If the poco has a property with the same name
		///     as the primary key the id of the new record is assigned to it.  Either way,
		///     the new id is returned.
		/// </remarks>
		public virtual async Task<object> InsertAsync(string tableName, string primaryKeyName, bool autoIncrement, object poco)
		{
			try
			{
				await OpenSharedConnectionAsync();
				try
				{
					using (var cmd = CreateCommand(_sharedConnection, ""))
					{
						var pd = PocoData.ForObject(poco, primaryKeyName);
						var names = new List<string>();
						var values = new List<string>();
						var index = 0;
						foreach (var i in pd.Columns)
						{
							// Don't insert result columns
							if (i.Value.ResultColumn)
								continue;

							// Don't insert computed columns
							if (i.Value.ComputedColumn)
								continue;

							// Don't insert the primary key (except under oracle where we need bring in the next sequence value)
							if (autoIncrement && primaryKeyName != null && string.Compare(i.Key, primaryKeyName, true) == 0)
							{
								// Setup auto increment expression
								var autoIncExpression = _dbType.GetAutoIncrementExpression(pd.TableInfo);
								if (autoIncExpression != null)
								{
									names.Add(i.Key);
									values.Add(autoIncExpression);
								}
								continue;
							}

							names.Add(_dbType.EscapeSqlIdentifier(i.Key));
							values.Add(string.Format(i.Value.InsertTemplate ?? "{0}{1}", _dbType.ParameterPrefix, index++));
							AddParam(cmd, i.Value.GetValue(poco), i.Value.PropertyInfo);
						}

						var outputClause = string.Empty;
						if (autoIncrement)
						{
							outputClause = _dbType.GetInsertOutputClause(primaryKeyName);
						}


						cmd.CommandText = string.Format("INSERT INTO {0} ({1}){2} VALUES ({3})",
							_dbType.EscapeTableName(tableName),
							string.Join(",", names.ToArray()),
							outputClause,
							string.Join(",", values.ToArray())
							);

						if (!autoIncrement)
						{
							DoPreExecute(cmd);
							await cmd.ExecuteNonQueryAsync();
							OnExecutedCommand(cmd);

							PocoColumn pkColumn;
							if (primaryKeyName != null && pd.Columns.TryGetValue(primaryKeyName, out pkColumn))
								return pkColumn.GetValue(poco);
							else
								return null;
						}


						var id = await _dbType.ExecuteInsertAsync(this, cmd, primaryKeyName);

						// Assign the ID back to the primary key property
						if (primaryKeyName != null)
						{
							PocoColumn pc;
							if (pd.Columns.TryGetValue(primaryKeyName, out pc))
							{
								pc.SetValue(poco, pc.ChangeType(id));
							}
						}

						return id;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch (Exception x)
			{
				if (OnException(x))
					throw;
				return null;
			}
		}

		/// <summary>
		///     Performs an SQL Insert
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be inserted</param>
		/// <returns>The auto allocated primary key of the new record, or null for non-auto-increment tables</returns>
		/// <remarks>
		///     The name of the table, it's primary key and whether it's an auto-allocated primary key are retrieved
		///     from the POCO's attributes
		/// </remarks>
		public Task<object> InsertAsync(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			return InsertAsync(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, pd.TableInfo.AutoIncrement, poco);
		}

#endregion

#region operation: Update

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <returns>The number of affected records</returns>
		public Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco, object primaryKeyValue)
		{
			return UpdateAsync(tableName, primaryKeyName, poco, primaryKeyValue, null);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		public virtual async Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco,
			object primaryKeyValue, IEnumerable<string> columns)
		{
			try
			{
				await OpenSharedConnectionAsync();
				try
				{
					// update
					using (var cmd = CreateCommand(_sharedConnection, ""))
					{
						var sb = new StringBuilder();
						var index = 0;
						var pd = PocoData.ForObject(poco, primaryKeyName);
						var primaryKeyValuePairs = GetPrimaryKeyValues(primaryKeyName, primaryKeyValue);

						if (columns == null)
						{
							foreach (var i in pd.Columns)
							{
								// Don't update the primary key, but grab the value if we don't have it
								if (primaryKeyValue == null && primaryKeyValuePairs.ContainsKey(i.Key))
								{
									primaryKeyValuePairs[i.Key] = i.Value.GetValue(poco);
									continue;
								}

								// Dont update result only columns
								if (i.Value.ResultColumn)
									continue;

								// Dont update computed columns
								if (i.Value.ComputedColumn)
									continue;

								// Build the sql
								if (index > 0)
									sb.Append(", ");
								sb.AppendFormat(i.Value.UpdateTemplate ?? "{0} = {1}{2}", _dbType.EscapeSqlIdentifier(i.Key), _dbType.ParameterPrefix, index++);

								// Store the parameter in the command
								AddParam(cmd, i.Value.GetValue(poco), i.Value.PropertyInfo);
							}
						}
						else
						{
							foreach (var colname in columns)
							{
								var pc = pd.Columns[colname];

								// Build the sql
								if (index > 0)
									sb.Append(", ");
								sb.AppendFormat(pc.UpdateTemplate ?? "{0} = {1}{2}", _dbType.EscapeSqlIdentifier(colname), _dbType.ParameterPrefix, index++);

								// Store the parameter in the command
								AddParam(cmd, pc.GetValue(poco), pc.PropertyInfo);
							}
						}

						cmd.CommandText = string.Format("UPDATE {0} SET {1} WHERE {2}",
							_dbType.EscapeTableName(tableName),
							sb,
							BuildPrimaryKeySql(primaryKeyValuePairs, ref index));

						foreach (var keyValue in primaryKeyValuePairs)
						{
							var pi = pd.Columns.ContainsKey(keyValue.Key) ? pd.Columns[keyValue.Key].PropertyInfo : null;
							AddParam(cmd, keyValue.Value, pi);
						}

						DoPreExecute(cmd);

						// Do it
						var retv = await cmd.ExecuteNonQueryAsync();
						OnExecutedCommand(cmd);
						return retv;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch (Exception x)
			{
				if (OnException(x))
					throw;
				return -1;
			}
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco)
		{
			return UpdateAsync(tableName, primaryKeyName, poco, null);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="tableName">The name of the table to update</param>
		/// <param name="primaryKeyName">The name of the primary key column of the table</param>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(string tableName, string primaryKeyName, object poco, IEnumerable<string> columns)
		{
			return UpdateAsync(tableName, primaryKeyName, poco, null, columns);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(object poco, IEnumerable<string> columns)
		{
			return UpdateAsync(poco, null, columns);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(object poco)
		{
			return UpdateAsync(poco, null, null);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(object poco, object primaryKeyValue)
		{
			return UpdateAsync(poco, primaryKeyValue, null);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <param name="poco">The POCO object that specifies the column values to be updated</param>
		/// <param name="primaryKeyValue">The primary key of the record to be updated</param>
		/// <param name="columns">The column names of the columns to be updated, or null for all</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync(object poco, object primaryKeyValue, IEnumerable<string> columns)
		{
			var pd = PocoData.ForType(poco.GetType());
			return UpdateAsync(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco, primaryKeyValue, columns);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to update</typeparam>
		/// <param name="sql">The SQL update and condition clause (ie: everything after "UPDATE tablename"</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync<T>(string sql, params object[] args)
		{
			var pd = PocoData.ForType(typeof(T));
			return ExecuteAsync(string.Format("UPDATE {0} {1}", _dbType.EscapeTableName(pd.TableInfo.TableName), sql), args);
		}

		/// <summary>
		///     Performs an SQL update
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to update</typeparam>
		/// <param name="sql">
		///     An SQL builder object representing the SQL update and condition clause (ie: everything after "UPDATE
		///     tablename"
		/// </param>
		/// <returns>The number of affected rows</returns>
		public Task<int> UpdateAsync<T>(Sql sql)
		{
			var pd = PocoData.ForType(typeof(T));
			return ExecuteAsync(new Sql(string.Format("UPDATE {0}", _dbType.EscapeTableName(pd.TableInfo.TableName))).Append(sql));
		}

#endregion

#region operation: Delete

		/// <summary>
		///     Performs and SQL Delete
		/// </summary>
		/// <param name="tableName">The name of the table to delete from</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The POCO object whose primary key value will be used to delete the row</param>
		/// <returns>The number of rows affected</returns>
		public Task<int> DeleteAsync(string tableName, string primaryKeyName, object poco)
		{
			return DeleteAsync(tableName, primaryKeyName, poco, null);
		}

		/// <summary>
		///     Performs and SQL Delete
		/// </summary>
		/// <param name="tableName">The name of the table to delete from</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">
		///     The POCO object whose primary key value will be used to delete the row (or null to use the supplied
		///     primary key value)
		/// </param>
		/// <param name="primaryKeyValue">
		///     The value of the primary key identifing the record to be deleted (or null, or get this
		///     value from the POCO instance)
		/// </param>
		/// <returns>The number of rows affected</returns>
		public virtual Task<int> DeleteAsync(string tableName, string primaryKeyName, object poco, object primaryKeyValue)
		{
			var primaryKeyValuePairs = GetPrimaryKeyValues(primaryKeyName, primaryKeyValue);

			if (poco != null)
			{
				// If primary key value not specified, pick it up from the object
				var pd = PocoData.ForObject(poco, primaryKeyName);
				foreach (var i in pd.Columns)
				{
					if (primaryKeyValue == null && primaryKeyValuePairs.ContainsKey(i.Key))
					{
						primaryKeyValuePairs[i.Key] = i.Value.GetValue(poco);
					}
				}
			}

			// Do it
			var index = 0;
			var sql = string.Format("DELETE FROM {0} WHERE {1}", tableName, BuildPrimaryKeySql(primaryKeyValuePairs, ref index));
			return ExecuteAsync(sql, primaryKeyValuePairs.Select(x => x.Value).ToArray());
		}

		/// <summary>
		///     Performs an SQL Delete
		/// </summary>
		/// <param name="poco">The POCO object specifying the table name and primary key value of the row to be deleted</param>
		/// <returns>The number of rows affected</returns>
		public Task<int> DeleteAsync(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			return DeleteAsync(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco);
		}

		/// <summary>
		///     Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class whose attributes identify the table and primary key to be used in the delete</typeparam>
		/// <param name="pocoOrPrimaryKey">The value of the primary key of the row to delete</param>
		/// <returns></returns>
		public Task<int> DeleteAsync<T>(object pocoOrPrimaryKey)
		{
			if (pocoOrPrimaryKey.GetType() == typeof(T))
				return DeleteAsync(pocoOrPrimaryKey);
			var pd = PocoData.ForType(typeof(T));
			return DeleteAsync(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, null, pocoOrPrimaryKey);
		}

		/// <summary>
		///     Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to delete from</typeparam>
		/// <param name="sql">The SQL condition clause identifying the row to delete (ie: everything after "DELETE FROM tablename"</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>The number of affected rows</returns>
		public Task<int> DeleteAsync<T>(string sql, params object[] args)
		{
			var pd = PocoData.ForType(typeof(T));
			return ExecuteAsync(string.Format("DELETE FROM {0} {1}", _dbType.EscapeTableName(pd.TableInfo.TableName), sql), args);
		}

		/// <summary>
		///     Performs an SQL Delete
		/// </summary>
		/// <typeparam name="T">The POCO class who's attributes specify the name of the table to delete from</typeparam>
		/// <param name="sql">
		///     An SQL builder object representing the SQL condition clause identifying the row to delete (ie:
		///     everything after "UPDATE tablename"
		/// </param>
		/// <returns>The number of affected rows</returns>
		public Task<int> DeleteAsync<T>(Sql sql)
		{
			var pd = PocoData.ForType(typeof(T));
			return
				ExecuteAsync(new Sql(string.Format("DELETE FROM {0}", _dbType.EscapeTableName(pd.TableInfo.TableName))).Append(sql));
		}

#endregion

#region operation: IsNew

		/// <summary>
		///     Check if a poco represents a new row
		/// </summary>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The object instance whose "newness" is to be tested</param>
		/// <returns>True if the POCO represents a record already in the database</returns>
		/// <remarks>This method simply tests if the POCO's primary key column property has been set to something non-zero.</remarks>
		public bool IsNew(string primaryKeyName, object poco)
		{
			var pd = PocoData.ForObject(poco, primaryKeyName);
			object pk;
			PocoColumn pc;
			if (pd.Columns.TryGetValue(primaryKeyName, out pc))
			{
				pk = pc.GetValue(poco);
			}
#if !PETAPOCO_NO_DYNAMIC
			else if (poco.GetType() == typeof(ExpandoObject))
			{
				return true;
			}
#endif
			else
			{
				var pi = poco.GetType().GetProperty(primaryKeyName);
				if (pi == null)
					throw new ArgumentException(
						string.Format("The object doesn't have a property matching the primary key column name '{0}'", primaryKeyName));
				pk = pi.GetValue(poco, null);
			}

			if (pk == null)
				return true;

			var type = pk.GetType();

			if (type.GetTypeInfo().IsValueType)
			{
				// Common primary key types
				if (type == typeof(long))
					return (long)pk == default(long);
				if (type == typeof(ulong))
					return (ulong)pk == default(ulong);
				if (type == typeof(int))
					return (int)pk == default(int);
				if (type == typeof(uint))
					return (uint)pk == default(uint);
				if (type == typeof(Guid))
					return (Guid)pk == default(Guid);

				// Create a default instance and compare
				return pk == Activator.CreateInstance(pk.GetType());
			}
			return pk == null;
		}

		/// <summary>
		///     Check if a poco represents a new row
		/// </summary>
		/// <param name="poco">The object instance whose "newness" is to be tested</param>
		/// <returns>True if the POCO represents a record already in the database</returns>
		/// <remarks>This method simply tests if the POCO's primary key column property has been set to something non-zero.</remarks>
		public bool IsNew(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			if (!pd.TableInfo.AutoIncrement)
				throw new InvalidOperationException(
					"IsNew() and Save() are only supported on tables with auto-increment/identity primary key columns");
			return IsNew(pd.TableInfo.PrimaryKey, poco);
		}

#endregion

#region operation: Save

		/// <summary>
		///     Saves a POCO by either performing either an SQL Insert or SQL Update
		/// </summary>
		/// <param name="tableName">The name of the table to be updated</param>
		/// <param name="primaryKeyName">The name of the primary key column</param>
		/// <param name="poco">The POCO object to be saved</param>
		public Task SaveAsync(string tableName, string primaryKeyName, object poco)
		{
			if (IsNew(primaryKeyName, poco))
			{
				return InsertAsync(tableName, primaryKeyName, true, poco);
			}
			return UpdateAsync(tableName, primaryKeyName, poco);
		}

		/// <summary>
		///     Saves a POCO by either performing either an SQL Insert or SQL Update
		/// </summary>
		/// <param name="poco">The POCO object to be saved</param>
		public Task SaveAsync(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			return SaveAsync(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco);
		}

#endregion

#region operation: Multi-Poco Query/Fetch

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<TRet>> FetchAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, params object[] args)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2) }, cb, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<TRet>> FetchAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, params object[] args)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco fetch
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
		public Task<List<TRet>> FetchAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql,
			params object[] args)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, object[] args, Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2) }, cb, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, object[] args, Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco query
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
		public Task QueryAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql, object[] args,
			Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<TRet>> FetchAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2) }, cb, sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<TRet>> FetchAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The returned list POCO type</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<TRet>> FetchAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql)
		{
			return FetchAsync<TRet>(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql, Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2) }, cb, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql, Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <typeparam name="TRet">The type of objects passed to the action callback</typeparam>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql, Action<TRet> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2>(string sql, params object[] args)
		{
			return FetchAsync<T1>(new[] { typeof(T1), typeof(T2) }, null, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2, T3>(string sql, params object[] args)
		{
			return FetchAsync<T1>(new[] { typeof(T1), typeof(T2), typeof(T3) }, null, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2, T3, T4>(string sql, params object[] args)
		{
			return FetchAsync<T1>(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, sql, args);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2>(string sql, object[] args, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2) }, null, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3>(string sql, object[] args, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3) }, null, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3, T4>(string sql, object[] args, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, sql, args, action);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2>(Sql sql)
		{
			return FetchAsync<T1, T2>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2, T3>(Sql sql)
		{
			return FetchAsync<T1, T2, T3>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <returns>A collection of POCO's as a List</returns>
		public Task<List<T1>> FetchAsync<T1, T2, T3, T4>(Sql sql)
		{
			return FetchAsync<T1, T2, T3, T4>(sql.SQL, sql.Arguments);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2>(Sql sql, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2) }, null, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3>(Sql sql, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3) }, null, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco query
		/// </summary>
		/// <typeparam name="T1">The first POCO type</typeparam>
		/// <typeparam name="T2">The second POCO type</typeparam>
		/// <typeparam name="T3">The third POCO type</typeparam>
		/// <typeparam name="T4">The fourth POCO type</typeparam>
		/// <param name="sql">An SQL builder object representing the query and it's arguments</param>
		/// <param name="action">Callback to process each result</param>
		public Task QueryAsync<T1, T2, T3, T4>(Sql sql, Action<T1> action)
		{
			return QueryAsync(new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, sql.SQL, sql.Arguments, action);
		}

		/// <summary>
		///     Perform a multi-poco fetch
		/// </summary>
		/// <typeparam name="TRet">The type of objects to pass to the action</typeparam>
		/// <param name="types">An array of Types representing the POCO types of the returned result set.</param>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <returns>A collection of POCO's as a List</returns>
		public async Task<List<TRet>> FetchAsync<TRet>(Type[] types, object cb, string sql, params object[] args)
		{
			var list = new List<TRet>();
			await QueryAsync<TRet>(types, cb, sql, args, list.Add);
			return list;
		}

		/// <summary>
		///     Performs a multi-poco query
		/// </summary>
		/// <typeparam name="TRet">The type of objects to pass to the action</typeparam>
		/// <param name="types">An array of Types representing the POCO types of the returned result set.</param>
		/// <param name="cb">A callback function to connect the POCO instances, or null to automatically guess the relationships</param>
		/// <param name="sql">The SQL query to be executed</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="action">Callback to process each result</param>
		public virtual async Task QueryAsync<TRet>(Type[] types, object cb, string sql, object[] args, Action<TRet> action)
		{
			await OpenSharedConnectionAsync();
			try
			{
				using (var cmd = CreateCommand(_sharedConnection, sql, args))
				{
					DbDataReader r;
					try
					{
						r = await cmd.ExecuteReaderAsync();
						OnExecutedCommand(cmd);
					}
					catch (Exception x)
					{
						if (OnException(x))
							throw;
						return;
					}
					var factory = MultiPocoFactory.GetFactory<TRet>(types, _sharedConnection.ConnectionString, sql, r);
					if (cb == null)
						cb = MultiPocoFactory.GetAutoMapper(types.ToArray());
					var bNeedTerminator = false;
					using (r)
					{
						while (true)
						{
							TRet poco;
							try
							{
								if (!await r.ReadAsync())
									break;
								poco = factory(r, cb);
							}
							catch (Exception x)
							{
								if (OnException(x))
									throw;
								return;
							}

							if (poco != null)
								action(poco);
							else
								bNeedTerminator = true;
						}
						if (bNeedTerminator)
						{
							var poco = (TRet)(cb as Delegate).DynamicInvoke(new object[types.Length]);
							if (poco != null)
								action(poco);
						}
					}
				}
			}
			finally
			{
				CloseSharedConnection();
			}
		}

#endregion

#region Last Command

		/// <summary>
		///     Retrieves the SQL of the last executed statement
		/// </summary>
		public string LastSQL { get; private set; }

		/// <summary>
		///     Retrieves the arguments to the last execute statement
		/// </summary>
		public object[] LastArgs { get; private set; }

		/// <summary>
		///     Returns a formatted string describing the last executed SQL statement and it's argument values
		/// </summary>
		public string LastCommand
		{
			get { return FormatCommand(LastSQL, LastArgs); }
		}

#endregion

#region FormatCommand

		/// <summary>
		///     Formats the contents of a DB command for display
		/// </summary>
		/// <param name="cmd"></param>
		/// <returns></returns>
		public string FormatCommand(IDbCommand cmd)
		{
			return FormatCommand(cmd.CommandText,
				(from IDataParameter parameter in cmd.Parameters select parameter.Value).ToArray());
		}

		/// <summary>
		///     Formats an SQL query and it's arguments for display
		/// </summary>
		/// <param name="sql"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public string FormatCommand(string sql, object[] args)
		{
			var sb = new StringBuilder();
			if (sql == null)
				return "";
			sb.Append(sql);
			if (args != null && args.Length > 0)
			{
				sb.Append("\n");
				for (var i = 0; i < args.Length; i++)
				{
					sb.AppendFormat("\t -> {0}{1} [{2}] = \"{3}\"\n", _dbType.ParameterPrefix, i, args[i].GetType().Name, args[i]);
				}
				sb.Remove(sb.Length - 1, 1);
			}
			return sb.ToString();
		}

#endregion

#region Public Properties

		/*
		public static IMapper Mapper
		{
			get;
			set;
		} */

		/// <summary>
		///     When set to true, PetaPoco will automatically create the "SELECT columns" part of any query that looks like it
		///     needs it
		/// </summary>
		public bool EnableAutoSelect { get; set; }

		/// <summary>
		///     When set to true, parameters can be named ?myparam and populated from properties of the passed in argument values.
		/// </summary>
		public bool EnableNamedParams { get; set; }

		/// <summary>
		///     Sets the timeout value for all SQL statements.
		/// </summary>
		public int CommandTimeout { get; set; }

		/// <summary>
		///     Sets the timeout value for the next (and only next) SQL statement
		/// </summary>
		public int OneTimeCommandTimeout { get; set; }

#endregion

#region Member Fields

		// Member variables
		internal DatabaseType _dbType;
		private Func<DbConnection> _connectionFactory;
		private DbConnection _sharedConnection;
		private DbTransaction _transaction;
		private int _sharedConnectionDepth;
		private int _transactionDepth;
		private bool _transactionCancelled;

#endregion

#region Internal operations

		internal async Task ExecuteNonQueryHelperAsync(DbCommand cmd)
		{
			DoPreExecute(cmd);
			await cmd.ExecuteNonQueryAsync();
			OnExecutedCommand(cmd);
		}

		internal async Task<object> ExecuteScalarHelperAsync(DbCommand cmd)
		{
			DoPreExecute(cmd);
			var r = await cmd.ExecuteScalarAsync();
			OnExecutedCommand(cmd);
			return r;
		}

		internal void DoPreExecute(DbCommand cmd)
		{
			// Setup command timeout
			if (OneTimeCommandTimeout != 0)
			{
				cmd.CommandTimeout = OneTimeCommandTimeout;
				OneTimeCommandTimeout = 0;
			}
			else if (CommandTimeout != 0)
			{
				cmd.CommandTimeout = CommandTimeout;
			}

			// Call hook
			OnExecutingCommand(cmd);

			// Save it
			LastSQL = cmd.CommandText;
			LastArgs = (from IDataParameter parameter in cmd.Parameters select parameter.Value).ToArray();
		}

#endregion

#region Composite primary key support

		private Dictionary<string, object> GetPrimaryKeyValues(string primaryKeyName, object primaryKeyValue)
		{
			Dictionary<string, object> primaryKeyValues;

			var multiplePrimaryKeysNames =
				primaryKeyName.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
			if (primaryKeyValue != null)
			{
				if (multiplePrimaryKeysNames.Length == 1)
				{
					primaryKeyValues = new Dictionary<string, object>(ColumnComparer)
					{
						{primaryKeyName, primaryKeyValue}
					};
				}
				else
				{
					var dict = primaryKeyValue as Dictionary<string, object>;
					primaryKeyValues = dict ??
									   multiplePrimaryKeysNames.ToDictionary(x => x,
										   x =>
											   primaryKeyValue.GetType()
												   .GetProperties()
												   .Single(y => string.Equals(x, y.Name, StringComparison.OrdinalIgnoreCase))
												   .GetValue(primaryKeyValue, null), ColumnComparer);
				}
			}
			else
			{
				primaryKeyValues = multiplePrimaryKeysNames.ToDictionary(x => x, x => (object)null,
					StringComparer.OrdinalIgnoreCase);
			}

			return primaryKeyValues;
		}

		private string BuildPrimaryKeySql(Dictionary<string, object> primaryKeyValuePair, ref int index)
		{
			var tempIndex = index;
			index += primaryKeyValuePair.Count;
			return string.Join(" AND ",
				primaryKeyValuePair.Select(
					(x, i) =>
						x.Value == null || x.Value == DBNull.Value
							? $"{_dbType.EscapeSqlIdentifier(x.Key)} IS NULL"
							: $"{_dbType.EscapeSqlIdentifier(x.Key)} = {_dbType.ParameterPrefix}{tempIndex + i}").ToArray());
		}

#endregion
	}
}