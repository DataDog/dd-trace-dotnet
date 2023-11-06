using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
#if NET7_0_OR_GREATER
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Samples.Security.AspNetCore5.Data;
using Samples.Security.AspNetCore5.Endpoints;
using Samples.Security.AspNetCore5.IdentityStores;
using SQLitePCL;

namespace Samples.Security.AspNetCore5
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            if (Configuration.GetValue<bool>("CreateDb"))
            {
                DatabaseHelper.CreateAndFeedDatabase(Configuration.GetConnectionString("DefaultConnection"));
            }

            services.AddRazorPages();
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
            // sql lite provider doesnt seem to work on linux (even with EF libs) so use in memory store 
            if (Configuration.ShouldUseSqlLite())
            {
#if NET7_0_OR_GREATER
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(Configuration.GetDefaultConnectionString()));
                identityBuilder.AddEntityFrameworkStores<ApplicationDbContext>();
#else
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
                identityBuilder.AddUserStore<UserStoreSqlLite>();
                identityBuilder.AddRoleStore<RoleStore>();
#endif
            }
            else
            {

                identityBuilder.AddUserStore<UserStoreMemory>();
                identityBuilder.AddRoleStore<RoleStore>();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // todo, for now disable to test that the block exception isn't caught by the call target integrations. 
                // later an exception filter should be added closer to the mvc pipeline so that this can't trigger because of a BlockException

                // app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();
            app.UseAuthentication();
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
                            _ = Task.Run(() => builder.ApplicationServices.GetService<IHostApplicationLifetime>().StopApplication());
                        });
                });

            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(state =>
                {
                    var httpContext = (HttpContext)state;
                    if (!httpContext.Request.Path.Value.ToLowerInvariant().Contains("xcontenttypeheadermissing"))
                    {
                        if (!httpContext.Response.Headers.ContainsKey("X-Content-Type-Options"))
                        {
                            httpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                        }
                    }
                    return Task.CompletedTask;
                }, context);

                await next.Invoke(); // Pass control to the next middleware.
            });


            app.UseEndpoints(
                endpoints =>
                {
                    endpoints.RegisterEndpointsRouting();
                    endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}");

                    endpoints.MapRazorPages();
                });
        }
    }
}
