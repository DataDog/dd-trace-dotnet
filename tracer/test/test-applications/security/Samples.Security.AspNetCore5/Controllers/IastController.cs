using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.Data;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class IastController : ControllerBase
    {
        static SQLiteConnection dbConnection = null;

        static readonly string txt1 = "Text 1";
        static readonly string txt2 = "Text 2";

        public IActionResult Index()
        {
            return Content(txt1 + txt2);
        }

        [HttpGet("WeakHashing")]
        [Route("WeakHashing/{delay1}")]
        public IActionResult WeakHashing(int delay1 = 0, int delay2 = 0)
        {
            System.Threading.Thread.Sleep(delay1 + delay2);
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            var byteArg = new byte[] { 3, 5, 6 };
            MD5.Create().ComputeHash(byteArg);
            SHA1.Create().ComputeHash(byteArg);
            return Content($"Weak hashes launched with delays {delay1} and {delay2}.\n");
#pragma warning restore SYSLIB0021 // Type or member is obsolete
        }

        [HttpGet("SqlQuery")]
        [Route("SqlQuery")]
        public IActionResult SqlQuery(string username, string query)
        {
            string aux = username + query;
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
                return StatusCode(500, IastControllerHelper.ToFormattedString(ex));
            }

            return BadRequest($"No query or username was provided");
        }
    }
}
