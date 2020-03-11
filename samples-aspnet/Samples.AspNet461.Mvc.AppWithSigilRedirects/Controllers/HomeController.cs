using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;

namespace Samples.AspNet461.Mvc.AppWithSigilRedirects.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            // Add dependency on Sigil
            var sigilEnum = Sigil.OptimizationOptions.All;

            // Add dependency on System.Net.Http
            // Also, add a call that instruments internal calls within System.Net.Http
            try
            {
                var client = new HttpClient();
                var response = client.GetAsync("http://www.contoso.com/").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // do nothing
            }

            return View();
        }

        public ActionResult About()
        {
            // Add a call that instruments internal calls within System.Data
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

            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
