using System;
using System.Web.UI;
using NLog;

namespace Samples.WebForms
{
    public partial class _Default : Page
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        protected void Page_Load(object sender, EventArgs e)
        {
            _log.Debug("In Coffeehouse.WebForms PageLoad");
        }
    }
}
