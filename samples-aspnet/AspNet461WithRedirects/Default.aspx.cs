using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Sigil;

namespace AspNet461WithRedirects
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Add dependency on Sigil
            var sigilEnum = Sigil.OptimizationOptions.All;

            // Add dependency on System.Net.Http
            try
            {
                var client = new HttpClient();
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
