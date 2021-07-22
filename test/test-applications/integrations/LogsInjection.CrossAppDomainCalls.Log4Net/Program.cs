
using System;
using System.IO;
using System.Reflection;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using log4net;
using log4net.Config;

namespace LogsInjection.CrossAppDomainCalls.Log4Net
{
    public class Program
    {
        /// <summary>
        /// Prepend a string to log lines that should not be validated for logs injection.
        /// In other words, they're not written within a Datadog scope 
        /// </summary>
        private static readonly string ExcludeMessagePrefix = "[ExcludeMessage]";

        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        public static int Main(string[] args)
        {
            // Set up the secondary AppDomain first
            // The plugin application we'll call was built and copied to the ApplicationFiles subdirectory
            // Create an AppDomain with that directory as the appBasePath
            var entryDirectory = Directory.GetParent(Assembly.GetEntryAssembly().Location);
            var applicationFilesDirectory = Path.Combine(entryDirectory.FullName, "ApplicationFiles");
            var applicationAppDomain = AppDomain.CreateDomain("ApplicationAppDomain", null, applicationFilesDirectory, applicationFilesDirectory, false);

            // Clean out previous logs
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");

            File.Delete(textFilePath);
            File.Delete(jsonFilePath);

            // Initialize log4net
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
#if LOG4NET_2_0_5
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(appDirectory, "log4net.205.config")));
#else
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(appDirectory, "log4net.Pre205.config")));
#endif
            log.Info($"{ExcludeMessagePrefix}Configured logger");

            // Set up Tracer and start a trace
            var settings = TracerSettings.FromDefaultSources();
            settings.LogsInjectionEnabled = true;
            settings.Environment ??= "dev"; // Later we can test when Environment=null / dd.env=null
            settings.ServiceVersion ??= "1.0.0"; // Later we can test when ServiceVersion=null / dd.service=null
            Tracer.Instance = new Tracer(settings);

            try
            {
                log.Info($"{ExcludeMessagePrefix}Entering Datadog scope.");
                using (var scope = Tracer.Instance.StartActive("transaction"))
                {
                    // In the middle of the trace, make a call across AppDomains
                    // Unless handled properly, this can cause the following error due
                    // to the way log4net stores "AsyncLocal" state in the
                    // System.Runtime.Remoting.Messaging.CallContext:
                    // System.Runtime.Serialization.SerializationException: Type is not resolved for member 'log4net.Util.PropertiesDictionary,log4net, Version=2.0.12.0, Culture=neutral, PublicKeyToken=669e0ddf0bb1aa2a'.

                    log.Info("Calling the PluginApplication.Program in a separate AppDomain");
                    // AppDomainProxy.Call(applicationAppDomain, "PluginApplication", "PluginApplication.Program", "Invoke", null);
                    log.Info("Returned from the PluginApplication.Program call");
                }

                log.Info($"{ExcludeMessagePrefix}Exited Datadog scope.");
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
