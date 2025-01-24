using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// this uses the ASP.NET Core integration
// this will cause the one function app (func.exe) to proxy
// the HTTP trigger functions HTTP request to the ASP NET Core app
// instead of sending it (primarily) as a gRPC message
// Note: this is similar to the V1 Program.cs, just a different way to configure the builder
builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
