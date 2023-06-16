using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Security.AspNetCore5.Data;
using Samples.Security.AspNetCore5.IdentityStores;
using SQLitePCL;

namespace Samples.Security.AspNetCore2
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseStartup<Startup>()
                   .Build();

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            if (Configuration.GetValue<bool>("CreateDb"))
            {
                DatabaseHelper.CreateAndFeedDatabase(Configuration.GetDefaultConnectionString());
            }

            var identityBuilder = services.AddIdentity<IdentityUser, IdentityRole>(
                o =>
                {
                    o.Password.RequireDigit = false;
                    o.Password.RequiredLength = 4;
                    o.Password.RequireLowercase = false;
                    o.Password.RequiredUniqueChars = 0;
                    o.Password.RequireUppercase = false;
                    o.Password.RequireNonAlphanumeric = false;
                });
            identityBuilder.AddRoleStore<RoleStore>();
            if (Configuration.ShouldUseSqlLite())
            {
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
                identityBuilder.AddUserStore<UserStoreSqlLite>();
            }
            else
            {
                identityBuilder.AddUserStore<UserStoreMemory>();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Map(
                "/alive-check",
                builder =>
                {
                    builder.Run(
                        async context =>
                        {
                            await context.Response.WriteAsync("Yes");
                        });
                });

            app.Map(
                "/shutdown",
                builder =>
                {
                    builder.Run(
                        async context =>
                        {
                            await context.Response.WriteAsync("Shutting down");
                            _ = Task.Run(() => builder.ApplicationServices.GetService<IApplicationLifetime>().StopApplication());
                        });
                });
            app.UseAuthentication();
            app.UseMvc(
                routes =>
                {
                    routes.MapRoute(
                        name: "default",
                        template: "{controller=Home}/{action=Index}/{id?}");
                });
        }
    }
}
