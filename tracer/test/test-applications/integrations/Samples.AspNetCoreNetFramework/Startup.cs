// <copyright file="Startup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.AspNetCoreNetFramework
{
    public class Startup
    {
        private const string DefaultSqlConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;Connection Timeout=30";

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime applicationLifetime)
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                app.UseMiddleware<ManualTracingMiddleware>();
            }

            app.UseMvc(
                routes =>
                {
                    routes.MapRoute(
                        name: "area",
                        template: "{area:exists}/{controller}/{action}/{id?}");
                    routes.MapRoute(
                        name: "default",
                        template: "{controller}/{action}/{id?}");
                });

            app.Run(async context =>
            {
                var path = context.Request.Path.Value;
                if (path == "/alive-check")
                {
                    await context.Response.WriteAsync("Alive");
                    return;
                }

                if (path == "/baseline/sql" || path == "/manual/sql")
                {
                    await QuerySql();
                    await context.Response.WriteAsync("OK");
                    return;
                }

                if (path == "/diagnostics/assembly-bindings")
                {
                    var bindings = AppDomain.CurrentDomain.GetAssemblies()
                                            .Select(assembly => assembly.GetName())
                                            .Where(
                                                 assemblyName => assemblyName.Name == "System.Diagnostics.DiagnosticSource"
                                                              || assemblyName.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
                                            .OrderBy(assemblyName => assemblyName.Name, StringComparer.Ordinal)
                                            .Select(assemblyName => $"{assemblyName.Name}={assemblyName.Version}");
                    await context.Response.WriteAsync(string.Join("\n", bindings));
                    return;
                }

                if (path == "/error")
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Unhandled request failure");
                }

                if (path == "/shutdown")
                {
                    await context.Response.WriteAsync("Shutting down");
                    _ = Task.Run(applicationLifetime.StopApplication);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
        }

        private static async Task QuerySql()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING") ?? DefaultSqlConnectionString;
            using (var connection = new SqlConnection(connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT 1";
                await connection.OpenAsync();
                await command.ExecuteScalarAsync();
            }
        }
    }
}
