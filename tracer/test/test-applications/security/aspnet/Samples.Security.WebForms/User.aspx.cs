using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Samples.Security.WebForms
{
    public partial class User : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Samples.SampleHelpers.SetUser("user3");
        }
    }
}
