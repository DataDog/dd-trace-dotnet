using System;
using System.Web.UI;
using Ninject;
using Samples.WebForms.Ninject.Services;

namespace Samples.WebForms.Ninject
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Resolve a service through the Ninject kernel, similar to the customer's
            // NinjectProvider.Get<IAcaRepository>() pattern.
            var repository = Global.Kernel.Get<IDataRepository>();
            var items = repository.GetItems();

            rptItems.DataSource = items;
            rptItems.DataBind();
        }
    }
}
