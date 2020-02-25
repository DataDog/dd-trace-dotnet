using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace AspNet461
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var client = new HttpClient();

            // Add dependency on System.Net.Http
            try
            {
                var response = client.GetAsync("http://www.contoso.com/").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // do nothing
            }
        }
    }
}
