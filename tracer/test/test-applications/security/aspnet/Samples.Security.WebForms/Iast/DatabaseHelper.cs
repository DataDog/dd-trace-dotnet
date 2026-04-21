using System.Data.SQLite;

namespace Samples.Security.WebForms.Iast
{
    public static class DatabaseHelper
    {
        public static SQLiteConnection DbConnection { get; set; }
    }
}
