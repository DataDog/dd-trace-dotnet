
namespace DatadogSymbolsServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Run();
        }

        private static WebApplication CreateHostBuilder(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSystemd();
            builder.Host.UseContentRoot(Directory.GetCurrentDirectory());

            var services = builder.Services;
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddHostedService<DotnetApmSymbolsCache>(s => s.GetService<ISymbolsCache>() as DotnetApmSymbolsCache);
            services.AddSingleton<ISymbolsCache, DotnetApmSymbolsCache>();
            services.AddControllers();
            services.AddHttpClient();

            var app = builder.Build();


            app.UseCors();
            app.UseRouting();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            app.UseEndpoints(app2 => app2.MapControllers());

            return app;

        }
    }
}
