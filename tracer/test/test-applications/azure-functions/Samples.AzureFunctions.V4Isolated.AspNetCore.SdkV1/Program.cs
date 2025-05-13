using Microsoft.Extensions.Hosting;

// this uses the ASP.NET Core integration
// this will cause the one function app (func.exe) to proxy
// the HTTP trigger functions HTTP request to the ASP NET Core app
// instead of sending it (primarily) as a gRPC message
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .Build();

host.Run();
