using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Http;
using Samples.AspNetMvc5.Models;
using Samples.Security.Data;

namespace Samples.Security.WebApi.Controllers
{
    public class IastController : ApiController
    {
        private static SQLiteConnection _dbConnection;

        private static SQLiteConnection DbConnection
        {
            get
            {
                if (_dbConnection == null)
                {
                    _dbConnection = IastControllerHelper.CreateSystemDataDatabase();
                }

                return _dbConnection;
            }
        }

        private static string Combine(string first, string second, string defaultValue)
        {
            var firstIsEmpty = string.IsNullOrWhiteSpace(first);
            var secondIsEmpty = string.IsNullOrWhiteSpace(second);

            if (firstIsEmpty && secondIsEmpty)
            {
                return defaultValue;
            }

            if (!firstIsEmpty && !secondIsEmpty)
            {
                return first + second;
            }

            return firstIsEmpty ? second : first;
        }

        [AcceptVerbs("POST")]
        [Route("Iast/PathTraversal")]
        public string PathTraversal([FromBody] MiscModel miscModel)
        {
            try
            {
                if (miscModel != null && !string.IsNullOrEmpty(miscModel.Id))
                {
                    var result = System.IO.File.ReadAllText(miscModel.Id);
                    return "file content: " + result;
                }

                return "No file was provided";
            }
            catch
            {
                return "The provided file " + miscModel?.Id + " could not be opened";
            }
        }

        [AcceptVerbs("GET")]
        [Route("Iast/SqlQuery")]
        public string SqlQuery(string username, string query)
        {
            try
            {
                if (!string.IsNullOrEmpty(username))
                {
                    var taintedQuery = "SELECT Surname from Persons where name = '" + username + "'";
                    var result = new SQLiteCommand(taintedQuery, DbConnection).ExecuteScalar();
                    return "Result: " + result;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    var result = new SQLiteCommand(query, DbConnection).ExecuteScalar();
                    return "Result: " + result;
                }

                return "No query or username was provided";
            }
            catch (SQLiteException ex)
            {
                return IastControllerHelper.ToFormattedString(ex);
            }
        }

        [AcceptVerbs("GET")]
        [Route("Iast/ExecuteCommand")]
        public string ExecuteCommand(string file, string argumentLine, bool fromShell = false)
        {
            return ExecuteCommandInternal(file, argumentLine, fromShell);
        }

        [AcceptVerbs("GET")]
        [Route("Iast/WeakHashing")]
        public string WeakHashing()
        {
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            var byteArg = new byte[] { 3, 5, 6 };
            MD5.Create().ComputeHash(byteArg);
            SHA1.Create().ComputeHash(byteArg);
#pragma warning restore SYSLIB0021 // Type or member is obsolete
            return "Weak hashes launched.\n";
        }

        [AcceptVerbs("GET")]
        [Route("Iast/Ldap")]
        public string Ldap(string path, string userName, bool skipQueryExecution = false)
        {
            var resultString = string.Empty;
            DirectoryEntry entry;

            try
            {
                var directoryEntryPath = !string.IsNullOrEmpty(path) ? path : "LDAP://fakeorg";
                entry = new DirectoryEntry(directoryEntryPath, string.Empty, string.Empty, AuthenticationTypes.None);
            }
            catch
            {
                entry = new DirectoryEntry();
            }

            var search = new DirectorySearcher(entry);
            if (!string.IsNullOrEmpty(userName))
            {
                search.Filter = "(uid=" + userName + ")";
            }

            if (skipQueryExecution)
            {
                return "LDAP filter prepared";
            }

            try
            {
                var result = search.FindAll();
                for (var i = 0; i < result.Count; i++)
                {
                    resultString += result[i].Path + Environment.NewLine;
                }
            }
            catch (Exception ex)
            {
                return IastControllerHelper.ToFormattedString(ex);
            }

            return "Result: " + resultString;
        }

        private string GetHeaderValue(string name)
        {
            IEnumerable<string> values;
            return Request.Headers.TryGetValues(name, out values) ? values.FirstOrDefault() : null;
        }

        [AcceptVerbs("GET")]
        [Route("Iast/AllVulnerabilitiesCookie")]
        public string AllVulnerabilitiesCookie()
        {
            var cookie = new HttpCookie("AllVulnerabilitiesCookieKey", "AllVulnerabilitiesCookieValue");
            cookie.Values["SameSite"] = "None";
            cookie.HttpOnly = false;
            cookie.Secure = false;
            HttpContext.Current.Response.Cookies.Add(cookie);
            return "Sending AllVulnerabilitiesCookie";
        }

        [AcceptVerbs("GET")]
        [Route("Iast/UnvalidatedRedirect")]
        public IHttpActionResult UnvalidatedRedirect(string param)
        {
            var location = $"Redirected?param={param}";
            return Redirect(location);
        }

        [AcceptVerbs("GET")]
        [Route("Iast/Redirected")]
        public string Redirected(string param)
        {
            return $"Redirected param:{param}\n";
        }

        [AcceptVerbs("GET")]
        [Route("Iast/HeaderInjection")]
        public string HeaderInjection(bool useValueFromOriginHeader = false)
        {
            const string defaultHeaderName = "defaultName";
            const string defaultHeaderValue = "defaultValue";

            var headerName = GetHeaderValue("name");
            var headerValue = GetHeaderValue("value");
            var originValue = GetHeaderValue("origin");
            var propagationHeader = GetHeaderValue("propagation");
            var cookieName = HttpContext.Current.Request.Cookies["name"]?.Value;
            var cookieValue = HttpContext.Current.Request.Cookies["value"]?.Value;

            if (!string.IsNullOrEmpty(propagationHeader))
            {
                HttpContext.Current.Response.AddHeader("propagation", propagationHeader);
                return "returned propagation header";
            }

            var returnedName = Combine(headerName, cookieName, defaultHeaderName);
            var returnedValue = useValueFromOriginHeader ? originValue : Combine(headerValue, cookieValue, defaultHeaderValue);

            HttpContext.Current.Response.AddHeader(returnedName, returnedValue);
            HttpContext.Current.Response.AddHeader("extraName", "extraValue");
            return $"returned header {returnedName},{returnedValue}";
        }

        [AcceptVerbs("GET")]
        [Route("Iast/ExecuteCommandFromHeader")]
        public string ExecuteCommandFromHeader()
        {
            return ExecuteCommandInternal(GetHeaderValue("file"), GetHeaderValue("argumentLine"));
        }

        [AcceptVerbs("GET")]
        [Route("Iast/ExecuteCommandFromCookie")]
        public string ExecuteCommandFromCookie()
        {
            var file = HttpContext.Current.Request.Cookies["file"]?.Value;
            var argumentLine = HttpContext.Current.Request.Cookies["argumentLine"]?.Value;
            return ExecuteCommandInternal(file, argumentLine);
        }

        private string ExecuteCommandInternal(string file, string argumentLine, bool fromShell = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = argumentLine,
                        UseShellExecute = fromShell
                    };

                    var result = Process.Start(startInfo);
                    return "Process launched: " + result.ProcessName;
                }

                return "No file was provided";
            }
            catch (Win32Exception ex)
            {
                return IastControllerHelper.ToFormattedString(ex);
            }
        }
    }
}
