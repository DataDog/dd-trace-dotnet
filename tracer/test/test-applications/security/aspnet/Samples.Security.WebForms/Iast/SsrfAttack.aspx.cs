using System;
using System.Net.Http;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class SsrfAttack : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var host = Request.QueryString["host"];
            if (!string.IsNullOrEmpty(host))
            {
                try
                {
                    var result = new HttpClient().GetStringAsync("https://" + host + "/path").Result;
                    Response.Write(result);
                }
                catch
                {
                    Response.Write("Error in request.");
                }
            }
            else
            {
                Response.Write("No host was provided");
            }
        }
    }
}
