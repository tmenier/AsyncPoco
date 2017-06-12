// AsyncPoco is a fork of PetaPoco and is bound by the same licensing terms.
// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.
 
using System;
using System.Data;
using System.Threading.Tasks;

namespace AsyncPoco
{
	public interface ITransaction : IDisposable
	{
		void Complete();
	}

	/// <summary>
	/// Transaction object helps maintain transaction depth counts
	/// </summary>
	public class Transaction : ITransaction
	{
		public static async Task<ITransaction> BeginAsync(Database db) 
		{
		    return await BeginAsyncInternal(db, db.BeginTransactionAsync);
		}

	    public static async Task<ITransaction> BeginAsync(Database db, IsolationLevel isolationLevel)
	    {
	        return await BeginAsyncInternal(db, () => db.BeginTransactionAsync(isolationLevel));
	    }

	    private static async Task<ITransaction> BeginAsyncInternal(Database db, Func<Task> beginTransactionAction)
	    {
	        var trans = new Transaction(db);
	        await beginTransactionAction();
	        return trans;
        }

        private Transaction(Database db)
		{
			_db = db;
		}

		public void Complete()
		{
			_db.CompleteTransaction();
			_db = null;
		}

		public void Dispose()
		{
			if (_db != null)
				_db.AbortTransaction();
		}

		Database _db;
	}
}
