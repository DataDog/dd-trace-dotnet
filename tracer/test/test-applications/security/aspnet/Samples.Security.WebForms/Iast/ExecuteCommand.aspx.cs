using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class ExecuteCommand : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var file = Request.QueryString["file"];
            var argumentLine = Request.QueryString["argumentLine"];
            var fromShell = string.Equals(Request.QueryString["fromShell"], "true", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = argumentLine,
                        UseShellExecute = fromShell
                    };

                    var result = Process.Start(startInfo);
                    Response.Write("Process launched: " + result.ProcessName);
                }
                catch (Win32Exception ex)
                {
                    Response.Write("Error: " + ex.Message);
                }
            }
            else
            {
                Response.Write("No file was provided");
            }
        }
    }
}
