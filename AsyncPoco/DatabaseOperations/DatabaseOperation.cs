using System.Data.Common;
using System.Threading.Tasks;

namespace AsyncPoco.DatabaseOperations
{
    public abstract class DatabaseOperation
    {
        protected static DbConnection sharedConnection;
        protected static int sharedConnectionDepth;

        protected DatabaseOperation()
        {
            initialize();
        }

        private void initialize()
        {
            throw new System.NotImplementedException();
        }

        public object Execute()
        {
            return null;
        }

        private async Task OpenSharedConnectionAsync()
        {
            
        }
    }
}
