// <copyright file="Startup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Samples.AspNetCoreNetFramework
{
    public class Startup
    {
        private const string DatabaseName = "aspnet-core-net-framework-repro";
        private const string CollectionName = "documents";

        public void ConfigureServices(IServiceCollection services)
        {
            var mongoHost = Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost";
            services.AddSingleton(new MongoClient($"mongodb://{mongoHost}:27017"));
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime applicationLifetime, MongoClient mongoClient)
        {
            app.UseMiddleware<ManualTracingMiddleware>();

            app.Run(async context =>
            {
                var path = context.Request.Path.Value;
                if (path == "/alive-check")
                {
                    await context.Response.WriteAsync("Alive");
                    return;
                }

                if (path == "/baseline/mongo" || path == "/manual/mongo")
                {
                    await QueryMongo(mongoClient);
                    await context.Response.WriteAsync("OK");
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

        private static async Task QueryMongo(MongoClient mongoClient)
        {
            var collection = mongoClient.GetDatabase(DatabaseName).GetCollection<BsonDocument>(CollectionName);
            var filter = new BsonDocument("_id", "fixed-missing-id");
            await collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}
