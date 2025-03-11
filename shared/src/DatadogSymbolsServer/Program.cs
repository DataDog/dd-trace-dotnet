using DatadogSymbolsServer;
using Polly;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSystemd();

var services = builder.Services;
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();
services.AddSingleton<ISymbolsCache, DotnetApmSymbolsCache>();
services.AddControllers();
// Add named httpclient, with resiliency policy
services.AddHttpClient("github", client =>
    {
        client.BaseAddress = new Uri("https://github.com/");
        client.Timeout = TimeSpan.FromMinutes(3);
    })
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.RetryAsync(3))
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(20)));


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

// Configure endpoints
app.MapControllers();

// prep the cache
using (var scope = app.Services.CreateScope())
{
    var cache = scope.ServiceProvider.GetRequiredService<ISymbolsCache>();
    await cache.Initialize(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
