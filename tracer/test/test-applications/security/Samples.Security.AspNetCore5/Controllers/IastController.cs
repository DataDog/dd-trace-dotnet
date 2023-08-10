using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.Data;

namespace Samples.Security.AspNetCore5.Controllers
{
    public class QueryData
    {
        public string Query { get; set; }
        public int IntField { get; set; }

        public List<string> Arguments { get; set; }

        public Dictionary<string, string> StringMap { get; set; }

        public string[] StringArrayArguments { get; set; }

        public QueryData InnerQuery { get; set; }
    }

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
            return ExecuteCommandInternal(file, argumentLine);
        }

        private IActionResult ExecuteCommandInternal(string file, string argumentLine)
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

        [Route("ExecuteQueryFromBodyQueryData")]
        public ActionResult ExecuteQueryFromBodyQueryData([FromBody] QueryData query)
        {
            try
            {
                if (dbConnection is null)
                {
                    dbConnection = IastControllerHelper.CreateDatabase();
                }

                return Query(query);
            }
            catch (Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }
        }

        private ActionResult Query(QueryData query)
        {
            if (!string.IsNullOrEmpty(query.Query))
            {
                return ExecuteQuery(query.Query);
            }

            if (query.Arguments is not null)
            {
                foreach (var value in query.Arguments)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return ExecuteQuery(value);
                    }
                }
            }

            if (query.StringArrayArguments is not null)
            {
                foreach (var value in query.StringArrayArguments)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return ExecuteQuery(value);
                    }
                }
            }

            if (query.StringMap is not null)
            {
                foreach (var value in query.StringMap)
                {
                    if (!string.IsNullOrEmpty(value.Value))
                    {
                        return ExecuteQuery(value.Value);
                    }
                    if (!string.IsNullOrEmpty(value.Key))
                    {
                        return ExecuteQuery(value.Key);
                    }
                }
            }

            if (query.InnerQuery !=null)
            {
                return Query(query.InnerQuery);
            }

            return Content($"No query or username was provided");
        }

        [Route("ExecuteQueryFromBodyText")]
        [Consumes("text/plain")]
        public ActionResult ExecuteQueryFromBodyText([FromBody] string query)
        {
            try
            {
                if (dbConnection is null)
                {
                    dbConnection = IastControllerHelper.CreateDatabase();
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

        [HttpGet("ExecuteCommandFromCookie")]
        [Route("ExecuteCommandFromCookie")]
        public IActionResult ExecuteCommandFromCookie()
        {
            return ExecuteCommandInternal(Request.Cookies["file"], Request.Cookies["argumentLine"]);
        }

        [HttpGet("GetDirectoryContent")]
        [Route("GetDirectoryContent")]
        public IActionResult GetDirectoryContent(string directory)
        {
            try
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    var result = System.IO.Directory.GetFiles(directory);
                    var resultfiles = string.Empty;
                    Array.ForEach(result, x => resultfiles += x.ToString() + Environment.NewLine);
                    return Content($"directory content: " + resultfiles);
                }
                else
                {
                    return BadRequest($"No directory was provided");
                }
            }
            catch
            {
                return Content("The provided directory could not be opened");
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
                return Content("The provided file could not be opened");
            }
        }

        [HttpGet("InsecureCookie")]
        [Route("InsecureCookie")]
        public IActionResult InsecureCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.Secure = false;
            Response.Cookies.Append("insecureKey", "insecureValue", cookieOptions);
            return Content("Sending InsecureCookie");
        }

        [HttpGet("NoHttpOnlyCookie")]
        [Route("NoHttpOnlyCookie")]
        public IActionResult NoHttpOnlyCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.HttpOnly = false;
            Response.Cookies.Append("NoHttpOnlyKey", "NoHttpOnlyValue", cookieOptions);
            return Content("Sending NoHttpOnlyCookie");
        }

        [HttpGet("NoSameSiteCookie")]
        [Route("NoSameSiteCookie")]
        public IActionResult NoSameSiteCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.SameSite = SameSiteMode.None;
            Response.Cookies.Append("NoSameSiteKey", "NoSameSiteValue", cookieOptions);
            var cookieOptionsLax = GetDefaultCookieOptionsInstance();
            cookieOptionsLax.SameSite = SameSiteMode.Lax;
            Response.Cookies.Append("NoSameSiteKeyLax", "NoSameSiteValueLax", cookieOptionsLax);
            var cookieOptionsDefault = new CookieOptions();
            cookieOptionsDefault.HttpOnly = true;
            cookieOptionsDefault.Secure = true;
            Response.Cookies.Append("NoSameSiteKeyDef", "NoSameSiteValueDef", cookieOptionsDefault);
            return Content("Sending NoSameSiteCookie");
        }

        [HttpGet("SafeCookie")]
        [Route("SafeCookie")]
        public IActionResult SafeCookie()
        {
            var cookieOptions = new CookieOptions();
            cookieOptions.SameSite = SameSiteMode.Strict;
            cookieOptions.HttpOnly = true;
            cookieOptions.Secure = true;
            Response.Cookies.Append("SafeCookieKey", "SafeCookieValue", cookieOptions);
            var cookie2 = new CookieOptions();
            cookie2.Secure = false;
            cookie2.HttpOnly = false;
            cookieOptions.SameSite = SameSiteMode.None;
            Response.Cookies.Append("UnsafeEmptyCookie", string.Empty, cookieOptions);
            return Content("Sending SafeCookie");
        }

        [HttpGet("AllVulnerabilitiesCookie")]
        [Route("AllVulnerabilitiesCookie")]
        public IActionResult AllVulnerabilitiesCookie()
        {
            var cookieOptions = new CookieOptions();
            cookieOptions.SameSite = SameSiteMode.None;
            cookieOptions.HttpOnly = false;
            cookieOptions.Secure = false;
            Response.Cookies.Append("AllVulnerabilitiesCookieKey", "AllVulnerabilitiesCookieValue", cookieOptions);
            return Content("Sending AllVulnerabilitiesCookie");
        }

        [HttpGet("SSRF")]
        [Route("SSRF")]
        public ActionResult Ssrf(string url, string host)
        {
            string result = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    result = new HttpClient().GetStringAsync(url).Result;
                }
                else
                {
                    result = new HttpClient().GetStringAsync("https://user:password@" + host + ":443/api/v1/test/123/?param1=pone&param2=ptwo#fragment1=fone&fragment2=ftwo").Result;
                }
            }
            catch
            {
                result = "Error in request.";
            }

            return Content(result, "text/html");
        }

        private ActionResult ExecuteQuery(string query)
        {
            var rname = new SQLiteCommand(query, dbConnection).ExecuteScalar();
            return Content($"Result: " + rname);
        }

        private CookieOptions GetDefaultCookieOptionsInstance()
        {
            var cookieOptions = new CookieOptions();
            cookieOptions.SameSite = SameSiteMode.Strict;
            cookieOptions.HttpOnly = true;
            cookieOptions.Secure = true;

            return cookieOptions;
        }
    }
}
