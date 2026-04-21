using System;
using System.Data.SQLite;
using System.IO;
using System.Web.Script.Serialization;
using System.Web.UI;
using Samples.Security.Data;

namespace Samples.Security.WebForms.Iast
{
    public partial class ExecuteQueryFromBodyQueryData : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (DatabaseHelper.DbConnection == null)
                {
                    DatabaseHelper.DbConnection = IastControllerHelper.CreateSystemDataDatabase();
                }

                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(body))
                {
                    Response.Write("No body was provided");
                    return;
                }

                var serializer = new JavaScriptSerializer();
                var queryData = serializer.Deserialize<QueryData>(body);

                if (!string.IsNullOrEmpty(queryData?.UserName))
                {
                    var query = "SELECT Surname from Persons where name = '" + queryData.UserName + "'";
                    var result = new SQLiteCommand(query, DatabaseHelper.DbConnection).ExecuteScalar();
                    Response.Write("Result: " + result);
                }
                else
                {
                    Response.Write("No query or username was provided");
                }
            }
            catch (SQLiteException ex)
            {
                Response.Write(IastControllerHelper.ToFormattedString(ex));
            }
        }
    }

    public class QueryData
    {
        public string UserName { get; set; }
        public string Query { get; set; }
    }
}
