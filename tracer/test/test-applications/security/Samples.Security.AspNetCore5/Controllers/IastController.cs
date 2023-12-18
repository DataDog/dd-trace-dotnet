using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.DirectoryServices;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
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

    public class XContentTypeOptionsAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            if (!filterContext.HttpContext.Request.Path.Value.Contains("XContentTypeHeaderMissing"))
            {
                filterContext.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            }

            base.OnResultExecuting(filterContext);
        }
    }

    [XContentTypeOptionsAttribute]
    [Route("[controller]")]
    [ApiController]
    public class IastController : ControllerBase
    {
        static SQLiteConnection dbConnection = null;

        public IActionResult Index()
        {
            return Content("Ok\n");
        }

        [HttpGet("HardcodedSecrets")]
        [Route("HardcodedSecrets")]
        public IActionResult HardcodedSecrets()
        {
            string[] hardcodedSecrets = new[] {
                "ghu_123456123456123456123456123456123456",
                "glpat--A7DO-8ZdceglrnsrMJ5",
                "glsa_6NVhs0hQUXFVHroLsch9IslQFSgd4Lum_324AC0da",
                "xapp-1-MGVEG-1-xswt",
            }; 
            
            return Content($"Loaded {hardcodedSecrets.Length} strings with potential hardcoded secrets.\n");
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

        //It uses Newtonsoft by default for netcore 2.1
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

            if (query.InnerQuery != null)
            {
                return Query(query.InnerQuery);
            }

            return Content($"No query or username was provided");
        }

        [Route("ExecuteQueryFromBodyText")]
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

        [Route("ExecuteQueryFromBodySerializeManual")]
        public ActionResult ExecuteQueryFromBodySerializeManual(string queryjson)
        {
            try
            {
                if (dbConnection is null)
                {
                    dbConnection = IastControllerHelper.CreateDatabase();
                }

                if (!string.IsNullOrEmpty(queryjson))
                {
                    var query = JsonConvert.DeserializeObject<QueryData>(queryjson);
                    return Query(query);
                }
            }
            catch (Exception ex)
            {
                return Content(IastControllerHelper.ToFormattedString(ex));
            }

            return Content($"No query was provided");
        }

        [HttpGet("ExecuteCommandFromHeader")]
        [Route("ExecuteCommandFromHeader")]
        public IActionResult ExecuteCommandFromHeader()
        {
            return ExecuteCommandInternal(Request.Headers["file"], Request.Headers["argumentLine"]);
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
                return Content("The provided file " + file + " could not be opened");
            }
        }

        [HttpGet("InsecureCookie")]
        [Route("InsecureCookie")]
        public IActionResult InsecureCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.Secure = false;
            Response.Cookies.Append("insecureKey", "insecureValue", cookieOptions);
            Response.Cookies.Append(".AspNetCore.Correlation.oidc.xxxxxxxxxxxxxxxxxxx", "ExcludedCookieVulnValue", cookieOptions);
            return Content("Sending InsecureCookie");
        }

        [HttpGet("NoHttpOnlyCookie")]
        [Route("NoHttpOnlyCookie")]
        public IActionResult NoHttpOnlyCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.HttpOnly = false;
            Response.Cookies.Append("NoHttpOnlyKey", "NoHttpOnlyValue", cookieOptions);
            Response.Cookies.Append(".AspNetCore.Correlation.oidc.xxxxxxxxxxxxxxxxxxx", "ExcludedCookieVulnValue", cookieOptions);
            return Content("Sending NoHttpOnlyCookie");
        }

        [HttpGet("TestCookieName")]
        public IActionResult TestCookieName()
        {
            var cookieName = Request.Cookies.Keys.First(x => x == "cookiename");

            try
            {
                if (!string.IsNullOrEmpty(cookieName))
                {
                    var result = System.IO.File.ReadAllText(cookieName);
                    return Content($"file content: " + result);
                }
                else
                {
                    return BadRequest($"No file was provided");
                }
            }
            catch
            {
                return Content("The provided file " + cookieName + " could not be opened");
            }
        }

        [HttpGet("NoSameSiteCookie")]
        [Route("NoSameSiteCookie")]
        public IActionResult NoSameSiteCookie()
        {
            var cookieOptions = GetDefaultCookieOptionsInstance();
            cookieOptions.SameSite = SameSiteMode.None;
            Response.Cookies.Append("NoSameSiteKey", "NoSameSiteValue", cookieOptions);
            Response.Cookies.Append(".AspNetCore.Correlation.oidc.xxxxxxxxxxxxxxxxxxx", "ExcludedCookieVulnValue", cookieOptions);
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
            Response.Cookies.Append(".AspNetCore.Correlation.oidc.xxxxxxxxxxxxxxxxxxx", "ExcludedCookieVulnValue", cookieOptions);
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

#if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
#endif
        [HttpGet("LDAP")]
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

        [HttpGet("WeakRandomness")]
        [Route("WeakRandomness")]
        public ActionResult WeakRandomness()
        {
            return Content("Random number: " + (new Random()).Next().ToString(), "text/html");
        }

        [HttpGet("XContentTypeHeaderMissing")]
        [Route("XContentTypeHeaderMissing")]
        public ActionResult XContentTypeHeaderMissing(string contentType = "text/html", int returnCode = 200, string xContentTypeHeaderValue = "")
        {
            // We don't want a header injection vulnerability here, so we untaint the header values.

            var xContentTypeHeaderValueUntainted = CopyStringAvoidTainting(xContentTypeHeaderValue);

            if (!string.IsNullOrEmpty(xContentTypeHeaderValueUntainted))
            {
                Response.Headers.Add("X-Content-Type-Options", xContentTypeHeaderValueUntainted);
            }

            if (returnCode != (int) HttpStatusCode.OK)
            {
                return StatusCode(returnCode);
            }

            if (!string.IsNullOrEmpty(contentType))
            {
                return Content("XContentTypeHeaderMissing", contentType);
            }
            else
            {
                return Content("XContentTypeHeaderMissing");
            }
        }

        [HttpGet("TBV")]
        [Route("TBV")]
        public ActionResult TrustBoundaryViolation(string name, string value)
        {
            string result = string.Empty;
            try
            {
                if (HttpContext.Session == null)
                {
                    result = "No session";
                }
                else
                {
                    HttpContext.Session.SetString(name, value);
                    HttpContext.Session.SetInt32(name + "-" + value, 42);
                    result = "Request parameters added to session (TrustBoundaryViolation)";
                }
            }
            catch (Exception err)
            {
                result = "Error in request. " + err.ToString();
            }

            return Content(result, "text/html");
        }

        [HttpGet("UnvalidatedRedirect")]
        [Route("UnvalidatedRedirect")]
        public ActionResult UnvalidatedRedirect(string param)
        {
            string result = string.Empty;
            try
            {
                var location = $"Redirected?param={param}";
                HttpContext.Response.Redirect(location);
                result = $"Request redirected to {location}";
            }
            catch (Exception err)
            {
                result = "Error in request. " + err.ToString();
            }

            return Content(result, "text/html");
        }

        [Route("Redirected")]
        public IActionResult Redirected(string param)
        {
            return Content($"Redirected param:{param}\n");
        }

        [HttpGet("StrictTransportSecurity")]
        [Route("StrictTransportSecurity")]
        public ActionResult StrictTransportSecurity(string contentType = "text/html", int returnCode = 200, string hstsHeaderValue = "", string xForwardedProto ="")
        {
            // We don't want a header injection vulnerability here, so we untaint the header values by
            // using reflection to access the private field "m_string" from the String class.
            var hstsHeaderValueUntainted = CopyStringAvoidTainting(hstsHeaderValue);
            var xForwardedProtoUntainted = CopyStringAvoidTainting(xForwardedProto);

            if (!string.IsNullOrEmpty(hstsHeaderValueUntainted))
            {
                Response.Headers.Add("Strict-Transport-Security", hstsHeaderValueUntainted);
            }

            if (!string.IsNullOrEmpty(xForwardedProtoUntainted))
            {
                Response.Headers.Add("X-Forwarded-Proto", xForwardedProtoUntainted);
            }

            if (returnCode != (int)HttpStatusCode.OK)
            {
                return StatusCode(returnCode);
            }

            if (!string.IsNullOrEmpty(contentType))
            {
                return Content("StrictTransportSecurityMissing", contentType);
            }
            else
            {
                return Content("StrictTransportSecurityMissing");
            }
        }

        // We should exclude some headers to prevent false positives:
        // location: it is already reported in UNVALIDATED_REDIRECT vulnerability detection.
        // Sec-WebSocket-Location, Sec-WebSocket-Accept, Upgrade, Connection: Usually the framework gets info from request
        // access-control-allow-origin: when the header is access-control-allow-origin and the source of the tainted range is the request header origin
        // set-cookie: We should ignore set-cookie header if the source of all the tainted ranges are cookies
        // We should exclude the injection when the tainted string only has one range which comes from a request header with the same name that the header that we are checking in the response.
        // Headers could store sensitive information, we should redact whole <header_value> if:
        // <header_name> matches with this RegExp
        // <header_value> matches with  this RegExp
        // We should redact the sensitive information from the evidence when:
        // Tainted range is considered sensitive value

        [HttpGet("HeaderInjection")]
        [Route("HeaderInjection")]
        public ActionResult HeaderInjection(bool UseValueFromOriginHeader = false)
        {
            string defaultHeaderName = "defaultName";
            string defaultHeaderValue = "defaultValue";

            string Combine(string name1, string name2, string defaultValue)
            {
                var null1 = string.IsNullOrWhiteSpace(name1);
                var null2 = string.IsNullOrWhiteSpace(name2);

                if (null1 && null2)
                {
                    return defaultValue;
                }
                if (!null1 && !null2) 
                { 
                    return name1 + name2;
                }
                else
                {
                    return null1 ? name2 : name1;
                }
            }

            var originValue = Request.Headers["origin"];
            var headerValue = Request.Headers["value"];
            var cookieValue = Request.Cookies["value"];
            var headerName = Request.Headers["name"];
            var cookieName = Request.Cookies["name"];
            string propagationHeader = Request.Headers["propagation"];

            if (!string.IsNullOrEmpty(propagationHeader))
            {
                Response.Headers.Add("propagation", propagationHeader);
                return Content($"returned propagation header");
            }

            var returnedName = Combine(headerName, cookieName, defaultHeaderName);
            var returnedValue = UseValueFromOriginHeader ? originValue.ToString() : Combine(headerValue, cookieValue, defaultHeaderValue);
            Response.Headers.Add(returnedName, returnedValue);
            return Content($"returned header {returnedName},{returnedValue}");
        }

        static string CopyStringAvoidTainting(string original)
        {
            return new string(original.AsEnumerable().ToArray());
        }
    }
}
