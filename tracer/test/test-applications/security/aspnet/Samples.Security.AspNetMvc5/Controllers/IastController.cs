using System.Security.Cryptography;
using System.Web.Mvc;
using System.Data.SQLite;
using System;
using Samples.Security.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Web;
using Microsoft.Ajax.Utilities;
using System.Net.Http;
using System.DirectoryServices;
using System.Collections.Generic;

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
            return ExecuteCommandInternal(file, argumentLine);
        }

        private ActionResult ExecuteCommandInternal(string file, string argumentLine)
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
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No file was provided");
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

        // Uses JavaScriptSerializer
        [Route("ExecuteQueryFromBodyQueryData")]
        public ActionResult ExecuteQueryFromBodyQueryData(QueryData queryInstance)
        {
            try
            {
                if (dbConnection is null)
                {
                    dbConnection = IastControllerHelper.CreateDatabase();
                }

                return Query(queryInstance);
            }
            catch (Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }
        }

        private ActionResult Query(QueryData query)
        {
            if (!string.IsNullOrEmpty(query?.Query))
            {
                return ExecuteQuery(query.Query);
            }

            if (query?.Arguments != null)
            {
                foreach (var value in query.Arguments)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return ExecuteQuery(value);
                    }
                }
            }

            if (query?.StringArrayArguments != null)
            {
                foreach (var value in query.StringArrayArguments)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        return ExecuteQuery(value);
                    }
                }
            }

            if (query?.StringMap != null)
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

            if (query?.InnerQuery != null)
            {
                return Query(query.InnerQuery);
            }

            return Content($"No query or username was provided");
        }

        private ActionResult ExecuteQuery(string query)
        {
            var rname = new SQLiteCommand(query, dbConnection).ExecuteScalar();
            return Content($"Result: " + rname);
        }

        [Route("ExecuteCommandFromCookie")]
        public ActionResult ExecuteCommandFromCookie()
        {
            // we test two different ways of obtaining a cookie
            var argumentValue = Request.Cookies["argumentLine"].Values[0];
            return ExecuteCommandInternal(Request.Cookies["file"].Value, argumentValue);
        }

        [Route("ExecuteCommandFromHeader")]
        public ActionResult ExecuteCommandFromHeader()
        {
            return ExecuteCommandInternal(Request.Headers["file"], Request.Headers["argumentLine"]);
        }

        [Route("GetDirectoryContent")]
        public ActionResult GetDirectoryContent(string directory)
        {
            try
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    var result = System.IO.Directory.GetFiles(directory);
                    var resultfiles = string.Empty;
                    result.ForEach(x => resultfiles += x.ToString() + Environment.NewLine);
                    return Content($"directory content: " + resultfiles);
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, $"No directory was provided");
                }
            }
            catch
            {
                return Content("The provided directory could not be opened");
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
                return Content("The provided file " + file + " could not be opened");
            }
        }

        [Route("InsecureCookie")]
        public ActionResult InsecureCookie()
        {
            var cookie = GetDefaultCookie("insecureKey", "insecureValue");
            cookie.Secure = false;
            Response.Cookies.Add(cookie);
            return Content("Sending InsecureCookie");
        }

        [Route("NoHttpOnlyCookie")]
        public ActionResult NoHttpOnlyCookie()
        {
            var cookie = GetDefaultCookie("NoHttpOnlyKey", "NoHttpOnlyValue");
            cookie.HttpOnly = false;
            Response.Cookies.Add(cookie);
            return Content("Sending NoHttpOnlyCookie");
        }

        [Route("NoSameSiteCookie")]
        public ActionResult NoSameSiteCookie()
        {
            var cookieDefault = new HttpCookie("NoSameSiteKeyDefault", "NoSameSiteValueDefault");
            cookieDefault.HttpOnly = true;
            cookieDefault.Secure = true;
            var cookieNone = GetDefaultCookie("NoSameSiteKeyNone", "NoSameSiteValueNOne");
            var cookieLax = GetDefaultCookie("NoSameSiteKeyLax", "NoSameSiteValueLax");
            cookieNone.Values["SameSite"] = "None";
            cookieLax.Values["SameSite"] = "Lax";
            Response.Cookies.Add(cookieDefault);
            Response.Cookies.Add(cookieNone);
            Response.Cookies.Add(cookieLax);
            return Content("Sending NoSameSiteCookies");
        }

        [Route("SafeCookie")]
        public ActionResult SafeCookie()
        {
            var cookie = GetDefaultCookie("SafeCookieKey", "SafeCookieValue");
            Response.Cookies.Add(cookie);
            var cookie2 = GetDefaultCookie("UnsafeEmptyCookie", string.Empty);
            cookie2.Secure = false;
            cookie2.HttpOnly = false;
            cookie2.Values["SameSite"] = "None";
            Response.Cookies.Add(cookie2);
            return Content("Sending SafeCookies");
        }

        [Route("AllVulnerabilitiesCookie")]
        public ActionResult AllVulnerabilitiesCookie()
        {
            HttpCookie cookie = new HttpCookie("AllVulnerabilitiesCookieKey", "AllVulnerabilitiesCookieValue");
            cookie.Values["SameSite"] = "None";
            cookie.HttpOnly = false;
            cookie.Secure = false;
            Response.Cookies.Add(cookie);
            return Content("Sending AllVulnerabilitiesCookie");
        }

        private HttpCookie GetDefaultCookie(string key, string value)
        {
            var cookie = new HttpCookie(key, value);
            cookie.Values["SameSite"] = "Strict";
            cookie.HttpOnly = true;
            cookie.Secure = true;
            return cookie;
        }

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

        [Route("LDAP")]
        public ActionResult Ldap(string path, string userName)
        {
            try
            {
                DirectoryEntry entry = null;
                try
                {
                    var directoryEntryPath = !string.IsNullOrEmpty(path) ? path : "LDAP://fakeorg";
                    entry = new DirectoryEntry(directoryEntryPath, string.Empty, string.Empty, AuthenticationTypes.None);
                }
                catch
                {
                    entry = new DirectoryEntry();
                }
                DirectorySearcher search = new DirectorySearcher(entry);
                if (!string.IsNullOrEmpty(userName))
                {
                    search.Filter = "(uid=" + userName + ")";
                }
                var result = search.FindAll();

                string resultString = string.Empty;

                for (int i = 0; i < result.Count; i++)
                {
                    resultString += result[i].Path + Environment.NewLine;
                }

                return Content($"Result: " + resultString);
            }
            catch
            {
                return Content($"Result: Not connected");
            }
        }

        [Route("WeakRandomness")]
        public ActionResult WeakRandomness()
        {
            return Content("Random number: " + (new Random()).Next().ToString() , "text/html");
        }
    }
}
