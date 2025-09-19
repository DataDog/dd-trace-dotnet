using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
        .ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddFilter("*", LogLevel.Trace);
        })

    .Build();
host.Run();

// This application is used to test the Azure Functions V4 Isolated worker with host logs disabled.
// It is the same as V4Isolated, but with the host logs disabled.
// When attempting to run the same Azure Functions project multiple times in our tests
// we were hitting a func.exe Singleton lock issue causing a deadlock of 60s.
// Making a new application seemed to be the simplest solution to avoid the issue.
