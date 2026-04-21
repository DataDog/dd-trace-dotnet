using System;
using System.Net.Http;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class SsrfAttackNoCatch : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var host = Request.QueryString["host"];
            var result = new HttpClient().GetStringAsync("https://" + host + "/path").Result;
            Response.Write(result);
        }
    }
}
