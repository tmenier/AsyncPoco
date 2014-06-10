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
        private readonly Func<IDataReader, T> factory;

        public QueryReader(Database database, DbDataReader dataReader, Func<IDataReader, T> factory)
        {
            this.database = database;
            this.dataReader = dataReader;
            this.factory = factory;
        }

        public async Task<bool> MoveNext()
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
            dataReader.Dispose();
        }
    }
}