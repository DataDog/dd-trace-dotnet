using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseFunctionExecutionMiddleware();
builder.UseOutputBindingsMiddleware();

builder.Build().Run();
