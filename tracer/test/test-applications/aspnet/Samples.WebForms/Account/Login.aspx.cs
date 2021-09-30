using System;
using System.Web.UI;

namespace Samples.WebForms.Account
{
    public partial class Login : Page
    {
        protected void Page_Load(object sender, EventArgs e) 
        {
            if (Request.QueryString["shutdown"] == "1")
            {
                Shutdown();
            }
        }

        protected void LogIn(object sender, EventArgs e)
        {
            FailureText.Text = "Invalid username or password.";
            ErrorMessage.Visible = true;
        }

        private void Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);
        }
    }
}
