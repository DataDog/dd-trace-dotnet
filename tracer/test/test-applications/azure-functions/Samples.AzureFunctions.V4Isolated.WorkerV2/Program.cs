using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    //.ConfigureFunctionsWorkerDefaults()
    .ConfigureFunctionsWebApplication()
    .Build();

host.Run();
