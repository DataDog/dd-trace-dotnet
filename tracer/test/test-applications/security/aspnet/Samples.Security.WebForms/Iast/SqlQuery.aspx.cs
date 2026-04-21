using System;
using System.Data.SQLite;
using System.Web.UI;
using Samples.Security.Data;

namespace Samples.Security.WebForms.Iast
{
    public partial class SqlQuery : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (DatabaseHelper.DbConnection == null)
                {
                    DatabaseHelper.DbConnection = IastControllerHelper.CreateSystemDataDatabase();
                }

                var username = Request.QueryString["username"];
                if (!string.IsNullOrEmpty(username))
                {
                    var query = "SELECT Surname from Persons where name = '" + username + "'";
                    var result = new SQLiteCommand(query, DatabaseHelper.DbConnection).ExecuteScalar();
                    Response.Write("Result: " + result);
                }
                else
                {
                    Response.Write("No username was provided");
                }
            }
            catch (SQLiteException ex)
            {
                Response.Write(IastControllerHelper.ToFormattedString(ex));
            }
        }
    }
}
