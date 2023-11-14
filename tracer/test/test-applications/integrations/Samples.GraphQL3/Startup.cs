using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Server;
using GraphQL.Server.Ui.Playground;
using GraphQL.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CharacterInterface = Samples.GraphQL3.StarWars.Types.CharacterInterface;
using DroidType = Samples.GraphQL3.StarWars.Types.DroidType;
using EpisodeEnum = Samples.GraphQL3.StarWars.Types.EpisodeEnum;
using HumanInputType = Samples.GraphQL3.StarWars.HumanInputType;
using HumanType = Samples.GraphQL3.StarWars.Types.HumanType;
using StarWarsData = Samples.GraphQL3.StarWars.StarWarsData;
using StarWarsMutation = Samples.GraphQL3.StarWars.StarWarsMutation;
using StarWarsQuery = Samples.GraphQL3.StarWars.StarWarsQuery;
using StarWarsSchema = Samples.GraphQL3.StarWars.StarWarsSchema;

namespace Samples.GraphQL3
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IDocumentExecuter, DocumentExecuter>();

            services.AddSingleton<StarWarsData>();
            services.AddSingleton<StarWarsQuery>();
            services.AddSingleton<StarWarsMutation>();
            services.AddSingleton<StarWarsExtensions.StarWarsSubscription>();
            services.AddSingleton<HumanType>();
            services.AddSingleton<HumanInputType>();
            services.AddSingleton<DroidType>();
            services.AddSingleton<CharacterInterface>();
            services.AddSingleton<EpisodeEnum>();
            services.AddSingleton<ISchema, StarWarsSchema>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddLogging(builder => builder.AddConsole());

            services.AddGraphQL(_ =>
            {
                _.EnableMetrics = true;
                // _.ExposeExceptions = true;
            })
            .AddNewtonsoftJson(_ => { }, _ => { })
            .AddUserContextBuilder(httpContext => new Dictionary<string, object>());
        }

        public void Configure(IApplicationBuilder app)
        {
            // Get StarWarsSchema Singleton
            var starWarsSchema = (StarWarsSchema)app.ApplicationServices.GetService(typeof(ISchema));

            // Get StarWarsSubscription Singleton
            var starWarsSubscription = (StarWarsExtensions.StarWarsSubscription)app.ApplicationServices.GetService(typeof(StarWarsExtensions.StarWarsSubscription));

            // Set the subscription
            // We do this roundabout mechanism to keep using the GraphQL.StarWars NuGet package
            starWarsSchema.Subscription = starWarsSubscription;
            app.UseDeveloperExceptionPage();
#if NETFRAMEWORK
// we run tests under net462 but this is still aspnet core, so neither aspnetcore diagnostic observer will work neither the TracingHttpModule will kick off. so no span will be created under http
            app.UseWhen(
                ctx => ctx.Request.Path == "/alive-check",
                x => x.Run(
                    next =>
                    {
                        using var scope = SampleHelpers.CreateScope("alive-check");
                        next.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    }));
#else
            app.UseWelcomePage("/alive-check");
#endif

            app.Map("/shutdown", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Shutting down");

#pragma warning disable CS0618 // Type or member is obsolete
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IApplicationLifetime>().StopApplication());
#pragma warning restore CS0618 // Type or member is obsolete
                });
            });

            // add http for Schema at default url /graphql
            app.UseGraphQL<ISchema>("/graphql");

            // use graphql-playground at default url /ui/playground
            app.UseGraphQLPlayground(new GraphQLPlaygroundOptions
            {
                Path = "/ui/playground"
            });
        }
    }
}
