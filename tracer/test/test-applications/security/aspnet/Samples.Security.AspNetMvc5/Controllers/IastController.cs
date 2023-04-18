using System.Security.Cryptography;
using System.Web.Mvc;
using System.Data.SQLite;
using System;
using System.Text;
using Samples.Security.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    public class IastController : Controller
    {
        static SQLiteConnection dbConnection = null;

        public ActionResult Index()
        {
            return Content("Ok\n");
        }

        [Route("WeakHashing/{delay1}")]
        public ActionResult WeakHashing(int delay1 = 0, int delay2 = 0)
        {
            System.Threading.Thread.Sleep(delay1 + delay2);
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            var byteArg = new byte[] { 3, 5, 6 };
            MD5.Create().ComputeHash(byteArg);
            SHA1.Create().ComputeHash(byteArg);
            return Content($"Weak hashes launched with delays {delay1} and {delay2}.\n");
#pragma warning restore SYSLIB0021 // Type or member is obsolete
        }

        [Route("SqlQuery")]
        public ActionResult SqlQuery(string username, string query)
        {
            try
            {
                if (dbConnection is null)
                {
                    dbConnection = IastControllerHelper.CreateDatabase();
                }

                if (!string.IsNullOrEmpty(username))
                {
                    var taintedQuery = "SELECT Surname from Persons where name = '" + username + "'";
                    var rname = new SQLiteCommand(taintedQuery, dbConnection).ExecuteScalar();
                    return Content($"Result: " + rname);
                }

                if (!string.IsNullOrEmpty(query))
                {
                    var rname = new SQLiteCommand(query, dbConnection).ExecuteScalar();
                    return Content($"Result: " + rname);
                }
            }
            catch (Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }

            return Content($"No query or username was provided");
        }

        [Route("ExecuteCommand")]
        public ActionResult ExecuteCommand(string file, string argumentLine)
        {
            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    var result = Process.Start(file, argumentLine);
                    return Content($"Process launched: " + result.ProcessName);
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, $"No file was provided");
                }
            }
            catch (Win32Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, IastControllerHelper.ToFormattedString(ex));
            }
        }

        [Route("GetFileContent")]
        public ActionResult GetFileContent(string file)
        {
            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    var result = System.IO.File.ReadAllText(file);
                    return Content($"file content: " + result);
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, $"No file was provided");
                }
            }
            catch
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "The provided file could not be opened");
            }
        }
    }
}
