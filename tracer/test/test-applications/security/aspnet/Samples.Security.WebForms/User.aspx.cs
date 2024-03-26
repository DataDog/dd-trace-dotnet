using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Datadog.Trace;

namespace Samples.Security.WebForms
{
    public partial class User : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var userId = "user3";

            var userDetails = new UserDetails()
            {
                Id = userId,
            };
            Tracer.Instance.ActiveScope?.Span.SetUser(userDetails);
        }
    }
}
