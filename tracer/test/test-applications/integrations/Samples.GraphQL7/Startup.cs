using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
#if GRAPHQL_5_0 || GRAPHQL_7_0
using GraphQL.MicrosoftDI;
using GraphQL.NewtonsoftJson;
#endif
using GraphQL.Server;
using GraphQL.Server.Ui.Playground;
using GraphQL.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CharacterInterface = Samples.GraphQL7.StarWars.Types.CharacterInterface;
using DroidType = Samples.GraphQL7.StarWars.Types.DroidType;
using EpisodeEnum = Samples.GraphQL7.StarWars.Types.EpisodeEnum;
using HumanInputType = Samples.GraphQL7.StarWars.HumanInputType;
using HumanType = Samples.GraphQL7.StarWars.Types.HumanType;
using StarWarsData = Samples.GraphQL7.StarWars.StarWarsData;
using StarWarsMutation = Samples.GraphQL7.StarWars.StarWarsMutation;
using StarWarsQuery = Samples.GraphQL7.StarWars.StarWarsQuery;
using StarWarsSchema = Samples.GraphQL7.StarWars.StarWarsSchema;

namespace Samples.GraphQL7
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
#if !GRAPHQL_5_0 && !GRAPHQL_7_0
            // Not required in GraphQL 5.0
            services.AddSingleton<IDocumentExecuter, SubscriptionDocumentExecuter>();
#endif

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

#if GRAPHQL_5_0 || GRAPHQL_7_0
            services.AddGraphQL(
                _ => _
#if GRAPHQL_5_0
                    .AddHttpMiddleware<ISchema>()
#endif
                    .AddNewtonsoftJson()
                    .AddUserContextBuilder(httpContext => new Dictionary<string, object>()));
#else
            services.AddGraphQL(_ =>
            {
                _.EnableMetrics = true;
                // _.ExposeExceptions = true;
            })
            .AddNewtonsoftJson(_ => { }, _ => { })
            .AddUserContextBuilder(httpContext => new Dictionary<string, object>());
#endif
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
            app.UseWelcomePage("/alive-check");

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
            app.UseGraphQLPlayground("/ui/playground");
        }
    }
}
