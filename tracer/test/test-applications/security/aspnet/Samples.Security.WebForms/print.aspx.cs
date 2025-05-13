using System;
using System.IO;
using System.Linq;
using System.Web.UI;

public partial class Iast_Print : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        var key = Request.QueryString.AllKeys.ElementAt(1);

        try
        {
            _ = File.ReadAllLines(key);
        }
        catch
        {

        }
    }
}
