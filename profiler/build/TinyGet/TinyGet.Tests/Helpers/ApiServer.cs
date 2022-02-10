using System;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace TinyGet.Tests.Helpers
{
    internal class ApiServer : IDisposable
    {
       //Run for CI: netsh http add urlacl url=http://+:2345/ user="Network Service"

        private readonly HttpSelfHostServer _server;

        public readonly string HostUrl;
        public readonly int Port = 2345;

        public ApiServer()
        {
            const string machineName = "localhost";
            HostUrl = string.Format("http://{0}:{1}/", machineName, Port);

            HttpSelfHostConfiguration config = new HttpSelfHostConfiguration(HostUrl);
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            _server = new HttpSelfHostServer(config);
            _server.OpenAsync().Wait(TimeSpan.FromSeconds(20));
        }

        public void Dispose()
        {
            _server.CloseAsync().Wait(1000);
        }
    }
}
