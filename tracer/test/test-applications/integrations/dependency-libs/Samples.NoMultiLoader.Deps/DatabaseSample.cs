using System.Data.SqlClient;
using System.Runtime.CompilerServices;

namespace Samples.NoMultiLoader.Deps
{
    public class DatabaseSample
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateSqlConnection()
        {
            _ = new SqlConnection();
        }
    }
}
