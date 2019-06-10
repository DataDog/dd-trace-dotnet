using System;
using System.Data.Common;
using System.Data.SqlClient;

namespace Samples.WebForms.Empty
{
    public partial class SqlServer : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var sql = new SqlWrapper();
            sql.GetValue();
        }
    }

    public class SqlWrapper
    {
        public object GetValue()
        {
            var connectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true";

            using (var connection = (DbConnection)new SqlConnection(connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT 1;";
                connection.Open();
                var reader = command.ExecuteReader();
                reader.Read();
                return reader[0];
            }
        }
    }
}
