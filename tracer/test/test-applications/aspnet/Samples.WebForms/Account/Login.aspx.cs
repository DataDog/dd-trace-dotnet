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
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { this, EventArgs.Empty });
                    }
                }
            }
        }
    }
}
