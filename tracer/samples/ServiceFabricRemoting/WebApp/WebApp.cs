using System.Collections.Generic;
using System.Fabric;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace WebApp
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class WebApp : StatelessService
    {
        public WebApp(StatelessServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            yield return new ServiceInstanceListener(
                serviceContext =>
                    new KestrelCommunicationListener(serviceContext,
                                                     "ServiceEndpoint",
                                                     (url, listener) =>
                                                     {
                                                         return new WebHostBuilder()
                                                               .UseKestrel()
                                                               .ConfigureServices(services => services.AddSingleton(serviceContext))
                                                               .UseContentRoot(Directory.GetCurrentDirectory())
                                                               .UseStartup<Startup>()
                                                               .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                                               .UseUrls(url)
                                                               .Build();
                                                     }));
        }
    }
}
