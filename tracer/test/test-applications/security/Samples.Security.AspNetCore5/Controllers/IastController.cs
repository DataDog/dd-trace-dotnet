using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
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

        public IActionResult Index()
        {
            return Content("Ok\n");
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

        [HttpGet("ExecuteCommand")]
        [Route("ExecuteCommand")]
        public IActionResult ExecuteCommand(string file, string argumentLine)
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
                    return BadRequest($"No file was provided");
                }
            }
            catch (Win32Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }
            catch (Exception ex)
            {
                return StatusCode(500, IastControllerHelper.ToFormattedString(ex));
            }
        }

        [HttpGet("GetFileContent")]
        [Route("GetFileContent")]
        public IActionResult GetFileContent(string file)
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
                    return BadRequest($"No file was provided");
                }
            }
            catch
            {
                return StatusCode(500, "The provided file could not be opened");
            }
        }
    }
}
