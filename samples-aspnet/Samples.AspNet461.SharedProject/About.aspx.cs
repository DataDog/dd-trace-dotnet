using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Samples.AspNet461
{
    public partial class About : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // We'll create a new table at the start
            var connection = new SqlConnection(Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ?? "Server=localhost;User=SA;Password=Strong!Passw0rd");
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                // command.CommandText = "DROP TABLE IF EXISTS Employees; CREATE TABLE Employees (Id int PRIMARY KEY, Name varchar(100));";
                command.CommandText = "IF OBJECT_ID('dbo.Test', 'U') IS NOT NULL DROP TABLE dbo.Test"; // SQL Server 2012

                var parameter = command.CreateParameter();
                parameter.ParameterName = "Id";
                parameter.Value = 1;
                command.Parameters.Add(parameter);

                int records = command.ExecuteNonQueryAsync().GetAwaiter().GetResult();
                // Console.WriteLine($"Deleted {records} record(s).");
            }
            connection.Close();
        }
    }
}
