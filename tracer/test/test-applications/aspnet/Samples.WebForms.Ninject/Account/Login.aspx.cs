using System;
using System.Web.UI;

namespace Samples.WebForms.Ninject.Account
{
    public partial class Login : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.QueryString["shutdown"] == "1")
            {
                SampleHelpers.RunShutDownTasks(this);
            }
        }
    }
}
