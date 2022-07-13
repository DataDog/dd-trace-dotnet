using Samples.HotChocolate;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();


var app = builder.Build();

app.MapGraphQL();

app.Map("/shutdown", builder =>
{
    builder.Run(async context =>
    {
        await context.Response.WriteAsync("Shutting down");

#pragma warning disable CS0618 // Type or member is obsolete
        _ = Task.Run(() => builder.ApplicationServices.GetService<Microsoft.Extensions.Hosting.IApplicationLifetime>().StopApplication());
#pragma warning restore CS0618 // Type or member is obsolete
    });
});

app.MapGet("/", () => "Hello World!");

app.Run();
