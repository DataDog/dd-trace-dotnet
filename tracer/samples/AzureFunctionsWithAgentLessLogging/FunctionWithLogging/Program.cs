using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging( (_, logging) =>
    {
        logging.ClearProviders().AddJsonConsole(opts =>
        {
            opts.IncludeScopes = true;
        });
    })
    .Build();

host.Run();
