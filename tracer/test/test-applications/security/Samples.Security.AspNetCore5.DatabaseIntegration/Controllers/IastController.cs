using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Diagnostics;
using System.DirectoryServices;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
#if NETCOREAPP3_0_OR_GREATER
using System.Text.Json;
#endif
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Samples.Security.Data;

#pragma warning disable ASP0019 // warning ASP0019: Use IHeaderDictionary.Append or the indexer to append or set headers. IDictionary.Add will throw an ArgumentException when attempting to add a duplicate key
namespace Samples.Security.AspNetCore5.Controllers.DatabaseIntegration
{

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
    public class IastController : Controller
    {
        private static SQLiteConnection _dbConnectionSystemData = null;
        private static SqliteConnection _dbConnectionSystemDataMicrosoftData = null;
        private static SqlConnection _dbConnectionSystemDataSqlClient = null;
        private static NpgsqlConnection _dbConnectionNpgsql = null;
        private static MySqlConnection _dbConnectionMySql = null;
        private static OracleConnection _dbConnectionOracle = null;

        public IActionResult Index()
        {
            return Content("Ok\n");
        }

        private static SQLiteConnection DbConnectionSystemData
        {
            get { return _dbConnectionSystemData ??= IastControllerHelper.CreateSystemDataDatabase(); }
        }
        
        private static SqliteConnection DbConnectionSystemDataMicrosoftData
        {
            get { return _dbConnectionSystemDataMicrosoftData ??= IastControllerHelper.CreateMicrosoftDataDatabase(); }
        }

        private static NpgsqlConnection DbConnectionNpgsql
        {
            get { return _dbConnectionNpgsql ??= IastControllerHelper.CreatePostgresDatabase(); }
        }

        private static SqlConnection DbConnectionSystemDataSqlClient
        {
            get { return _dbConnectionSystemDataSqlClient ??= IastControllerHelper.CreateSqlServerDatabase(); }
        }

        private static MySqlConnection DbConnectionMySql
        {
            get { return _dbConnectionMySql ??= IastControllerHelper.CreateMySqlDatabase(); }
        }

        private static OracleConnection DbConnectionOracle
        {
            get { return _dbConnectionOracle ??= IastControllerHelper.CreateOracleDatabase(); }
        }

        [HttpGet("StoredXss")]
        [Route("StoredXss")]
        public IActionResult StoredXss(string database = null)
        {
            var db = GetDbConnectionFromName(database);

            var param = GetDbValue(db);
            ViewData["XSS"] = param + "<b>More Text</b>";
            return View("Xss");
        }

        [HttpGet("StoredXssEscaped")]
        [Route("StoredXssEscaped")]
        public IActionResult StoredXssEscaped(string database = null)
        {
            var db = GetDbConnectionFromName(database);

            var param = GetDbValue(db);
            var escapedText = System.Net.WebUtility.HtmlEncode($"System.Net.WebUtility.HtmlEncode({param})") + Environment.NewLine
                            + System.Web.HttpUtility.HtmlEncode($"System.Web.HttpUtility.HtmlEncode({param})") + Environment.NewLine;
            ViewData["XSS"] = escapedText;
            return View("Xss");
        }


        [HttpGet("StoredSqli")]
        [Route("StoredSqli")]
        public IActionResult StoredSqli(string database = null)
        {
            try
            {

                var db = GetDbConnectionFromName(database);

                var details = GetDbValue(db, "Michael");
                var taintedQuery = "SELECT name from Persons where Details = '" + details + "'";

                var name = db switch
                {
                    SQLiteConnection connection => new SQLiteCommand(taintedQuery, connection).ExecuteScalar(),
                    SqliteConnection sqliteConnection => new SqliteCommand(taintedQuery, sqliteConnection).ExecuteScalar(),
                    SqlConnection connection => new SqlCommand(taintedQuery, connection).ExecuteScalar(),
                    NpgsqlConnection connection => new NpgsqlCommand(taintedQuery, connection).ExecuteScalar(),
                    MySqlConnection connection => new MySqlCommand(taintedQuery, connection).ExecuteScalar(),
                    OracleConnection connection => new OracleCommand(taintedQuery, connection).ExecuteScalar(),
                    _ => null
                };

                return Content($"Result: " + name);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500);
            }
        }

        private static IDbConnection GetDbConnectionFromName(string database)
        {
            IDbConnection db =
                database switch
                {
                    "System.Data.SQLite" => DbConnectionSystemDataMicrosoftData,
                    "System.Data.SqlClient" => DbConnectionSystemDataSqlClient,
                    "Microsoft.Data.Sqlite" => DbConnectionSystemData,
                    "Npgsql" => DbConnectionNpgsql,
                    "MySql.Data" => DbConnectionMySql,
                    "Oracle" => DbConnectionOracle,
                    null => DbConnectionSystemData,
                    _ => throw new Exception($"unknown db type: {database}")
                };
            return db;
        }

        private static string GetDbValue(IDbConnection db, string name = "Name1")
        {
            var taintedQuery = $"SELECT Details from Persons where name = '{name}'";

            using IDataReader reader = db switch
            {
                SQLiteConnection connection => new SQLiteCommand(taintedQuery, connection).ExecuteReader(),
                SqliteConnection connection => new SqliteCommand(taintedQuery, connection).ExecuteReader(),
                SqlConnection connection => new SqlCommand(taintedQuery, connection).ExecuteReader(),
                NpgsqlConnection connection => new NpgsqlCommand(taintedQuery, connection).ExecuteReader(),
                MySqlConnection connection => new MySqlCommand(taintedQuery, connection).ExecuteReader(),
                OracleConnection connection => new OracleCommand(taintedQuery, connection).ExecuteReader(),
                _ => throw new ArgumentException("Invalid db connection")
            };

            reader.Read();
            var res = reader.GetString(0);
            return res;
        }
    }
}
