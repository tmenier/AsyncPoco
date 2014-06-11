using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace AsyncPoco
{
    public class QueryReader<T> : IDisposable
    {
        private readonly Database database;
        private readonly DbDataReader dataReader;
        private readonly IDbCommand command;
        private readonly Func<IDataReader, T> factory;

        public QueryReader(Database database, IDbCommand command, DbDataReader dataReader, Func<IDataReader, T> factory)
        {
            this.database = database;
            this.command = command;
            this.dataReader = dataReader;
            this.factory = factory;
        }

        public async Task<bool> MoveNextAsync()
        {
            try
            {
                if (!await dataReader.ReadAsync())
                {
                    return false;
                }

                Current = factory(dataReader);
            }
            catch (Exception x)
            {
                if (database.OnException(x))
                    throw;

                return false;
            }
            return true;
        }

        public T Current { get; private set; }

        public void Dispose()
        {
            database.CloseSharedConnection();
            command.Dispose();
            dataReader.Dispose();
        }
    }

}