using Microsoft.Owin;
using Owin;
using Samples.WebForms;

[assembly: OwinStartup(typeof(Startup))]
namespace Samples.WebForms
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
