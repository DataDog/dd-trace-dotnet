using Samples.HotChocolate;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();


var app = builder.Build();

app.MapGraphQL();

app.MapGet("/", () => "Hello World!");

app.Run();
