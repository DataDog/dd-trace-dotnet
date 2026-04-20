using System;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class Xss : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var input = Request.QueryString["input"];
            if (!string.IsNullOrEmpty(input))
            {
                Response.Write("<div>" + input + "</div>");
            }
            else
            {
                Response.Write("no input");
            }
        }
    }
}
