using System;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class TrustBoundary : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var name = Request.QueryString["name"];
            var value = Request.QueryString["value"];

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                Session[name] = value;
                Response.Write("stored " + name);
            }
            else
            {
                Response.Write("no input");
            }
        }
    }
}
