using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Karambolo.Extensions.Logging.File;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Formatting.Compact;

namespace Samples.AspNetCoreMvc
{
    public class LogHelpers
    {
        public static void ConfigureCustomLogging(WebHostBuilderContext ctx, ILoggingBuilder logging)
        {
           // delete existing log files
           var logDir = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "log");
           if (Directory.Exists(logDir))
           {
               try
               {
                   Directory.Delete(logDir, recursive: true);
               }
               catch
               {
                   // Don't throw if something's amiss
               }
           }

           // First, set up Serilog
           Log.Logger = new LoggerConfiguration()
                       .Enrich.FromLogContext()
                       .WriteTo.File(Path.Combine(logDir, "Serilog/ILoggerInterface.To.Serilog.Raw.log"), outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Properties} {Message:lj} {NewLine}{Exception}")
                       .WriteTo.File(new CompactJsonFormatter(), Path.Combine(logDir, "Serilog/ILoggerInterface.To.Serilog.CompactJson.log"))
                       .CreateLogger();
           logging.ClearProviders();
           logging.AddConsole();
           logging.AddFile(o =>
           {
               o.IncludeScopes = true;
               o.RootPath = logDir;
               o.BasePath = "Karambolo";
               o.TextBuilder = new JsonLogEntryTextBuilder();
               o.Files = new LogFileOptions[] { new() { Path = "log.txt" } };
           });
           logging.AddSerilog(dispose: true);
        }

        public static void WriteUnTracedLog(Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Building pipeline");
        }

        public class LogMiddleware
        {
            private readonly ILogger<LogMiddleware> _logger;
            private readonly RequestDelegate _next;

            public LogMiddleware(ILogger<LogMiddleware> logger, RequestDelegate next)
            {
                _logger = logger;
                _next = next;
            }

            public Task InvokeAsync(HttpContext httpContext)
            {
                _logger.LogInformation("Visited {Path}", httpContext.Request.Path);
                return _next(httpContext);
            }
        }

        public class JsonLogEntryTextBuilder : IFileLogEntryTextBuilder
        {
            public void BuildEntryText(
                StringBuilder sb,
                string categoryName,
                LogLevel logLevel,
                EventId eventId,
                string message,
                Exception exception,
                IExternalScopeProvider scopeProvider,
                DateTimeOffset timestamp)
            {
                var properties = new Dictionary<string, object>();
                if (scopeProvider is not null)
                {
                    scopeProvider.ForEachScope(
                        (scope, builder) =>
                        {
                            if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                            {
                                foreach (var kvp in pairs)
                                {
                                    properties[kvp.Key] = kvp.Value;
                                }
                            }
                            else
                            {
                                var scopes = builder.TryGetValue("Scope", out var rawScope)
                                                 ? (List<object>)rawScope
                                                 : new List<object>();
                                scopes.Add(scope);
                                builder["Scope"] = scopes;
                            }
                        },
                        properties);
                }

                var logEntry = new LogEntry
                {
                    CategoryName = categoryName,
                    LogLevel = logLevel,
                    EventId = eventId,
                    Message = message ?? "",
                    Exception = exception,
                    Timestamp = timestamp,
                    Properties = properties,
                };

                sb.AppendLine(JsonConvert.SerializeObject(logEntry));
            }

            private class LogEntry
            {
                public string CategoryName { get; set; }
                public LogLevel LogLevel { get; set; }
                public EventId EventId { get; set; }
                public string Message { get; set; }
                public Exception Exception { get; set; }
                public DateTimeOffset Timestamp { get; set; }
                public Dictionary<string, object> Properties { get; set; }
            }
        }
    }
}
