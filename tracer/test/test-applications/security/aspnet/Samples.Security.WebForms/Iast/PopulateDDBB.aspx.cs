using System;
using System.Data.SQLite;
using System.Web.UI;
using Samples.Security.Data;

namespace Samples.Security.WebForms.Iast
{
    public partial class PopulateDDBB : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (DatabaseHelper.DbConnection == null)
                {
                    DatabaseHelper.DbConnection = IastControllerHelper.CreateSystemDataDatabase();
                }

                Response.Write("OK");
            }
            catch (SQLiteException ex)
            {
                Response.Write(IastControllerHelper.ToFormattedString(ex));
            }
        }
    }
}
