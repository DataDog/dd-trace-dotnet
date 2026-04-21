using System;
using System.Web.UI;

namespace Samples.Security.WebForms.Iast
{
    public partial class GetFileContent : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var file = Request.QueryString["file"];
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var result = System.IO.File.ReadAllText(file);
                    Response.Write("file content: " + result);
                }
                catch
                {
                    Response.Write("The provided file " + file + " could not be opened");
                }
            }
            else
            {
                Response.Write("No file was provided");
            }
        }
    }
}
