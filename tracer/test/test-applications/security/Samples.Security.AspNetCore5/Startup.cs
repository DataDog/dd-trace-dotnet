using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Samples.Security.AspNetCore5.Data;
using Samples.Security.AspNetCore5.Endpoints;

namespace Samples.Security.AspNetCore5
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            if (Configuration.GetValue<bool>("CreateDb"))
            {
                DatabaseHelper.CreateAndFeedDatabase(Configuration.GetConnectionString("DefaultConnection"));
            }
            services.AddRazorPages();
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
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();


            app.Map("/shutdown", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Shutting down");
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IHostApplicationLifetime>().StopApplication());
                });
            });

            app.Use(async (context, next) =>
            {
                // make sure if we go into this middleware after blocking has happened that it s not a second request issued by the developerhandlingpage middleware! if it s that no worries it s another request, not the attack one., its  a redirect.
                // await context.Response.WriteAsync("do smth before all");
                await next.Invoke();
            });
           

            app.UseEndpoints(endpoints =>
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
