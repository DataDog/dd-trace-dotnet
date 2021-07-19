
using System;
using System.IO;
using System.Reflection;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Serilog;
using Serilog.Formatting.Json;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace LogsInjection.CrossAppDomainCalls.Serilog
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // Set up the secondary AppDomain first
            // The plugin application we'll call was built and copied to the ApplicationFiles subdirectory
            // Create an AppDomain with that directory as the appBasePath
            var entryDirectory = Directory.GetParent(Assembly.GetEntryAssembly().Location);
            var applicationFilesDirectory = Path.Combine(entryDirectory.FullName, "ApplicationFiles");
            var applicationAppDomain = AppDomain.CreateDomain("ApplicationAppDomain", null, applicationFilesDirectory, applicationFilesDirectory, false);

            // Initialize Serilog
            var log = new LoggerConfiguration()
                                        .Enrich.FromLogContext()
                                        .MinimumLevel.Is(LogEventLevel.Information)
                                        .WriteTo.File(
                                            "log-Serilog-textFile.log",
                                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Properties} {Message:lj} {NewLine}{Exception}")
                                        .WriteTo.File(
                                            new JsonFormatter(),
                                            "log-Serilog-jsonFile-allProperties.log")
                                        .CreateLogger();
            log.Information("Configured logger");

            // Set up Tracer and start a trace
            var settings = new TracerSettings()
            {
                LogsInjectionEnabled = true,
                // Set Environment=null, resulting in dd.env=null
                // Set ServiceVersion=null, resulting in dd.service=null
                Environment = "dev",
                ServiceVersion = "1.0.0"
            };
            Tracer.Instance = new Tracer(settings);

            try
            {
                log.Information("Entering Datadog scope.");
                using (var scope = Tracer.Instance.StartActive("transaction"))
                {
                    // In the middle of the trace, make a call across AppDomains.
                    // Historically, Serilog correctly handles "AsyncLocal" state in the
                    // System.Runtime.Remoting.Messaging.CallContext by wrapping it in
                    // an object that will not be serialized/deserialized across AppDomains.
                    // This is a smoke test to ensure that is the case

                    log.Information("Calling the PluginApplication.Program in a separate AppDomain");
                    AppDomainProxy.Call(applicationAppDomain, "PluginApplication", "PluginApplication.Program", "Invoke", null);
                    log.Information("Returned from the PluginApplication.Program call");
                }

                log.Information("Exited Datadog scope.");
                AppDomain.Unload(applicationAppDomain);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }

        public class AppDomainProxy : MarshalByRefObject
        {
            object CallInternal(string assemblyName, string typeName, string methodName, object[] parameters)
            {
                Assembly remoteAssembly = Assembly.Load(assemblyName);
                Type remoteType = remoteAssembly.GetType(typeName);
                object remoteObject = Activator.CreateInstance(remoteType);
                MethodInfo remoteMethod = remoteType.GetMethod(methodName);

                return remoteMethod.Invoke(remoteObject, parameters);
            }

            public static object Call(AppDomain domain, string assemblyName, string typeName, string methodName, params object[] parameters)
            {
                AppDomainProxy proxy = (AppDomainProxy)domain.CreateInstanceFromAndUnwrap(typeof(AppDomainProxy).Assembly.Location, typeof(AppDomainProxy).FullName);
                object result = proxy.CallInternal(assemblyName, typeName, methodName, parameters);
                return result;
            }
        }
    }
}
