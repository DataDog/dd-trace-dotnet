using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Datadog.Trace.ClrProfiler;
using Samples.WebApi.Async.EntityFramework;

namespace Samples.WebApi.Async.Controllers
{
    public class HomeController : AsyncController
    {
        public async Task<ActionResult> Index()
        {
            if (Instrumentation.ProfilerAttached == false)
            {
                // throw new Exception("Need the profiler attached.");
            }

            ViewBag.Title = "Home Page";

            var dbContext = new CodeFirstContext();

            dbContext.Jokes.Add(new Joke() { Text = "Did you hear about the duck who got into soap operas? He was so mallard-dramatic." });
            dbContext.Jokes.Add(new Joke() { Text = "Did you hear about the diamond they tried to shape like a duck? It quacked under the pressure." });
            dbContext.Jokes.Add(new Joke() { Text = "Did you hear about the cheese factory that burned down? There was nothing left but de brie." });

            await dbContext.SaveChangesAsync();

            var duckJokes = await dbContext.Jokes.Where(j => j.Text.Contains("duck")).ToListAsync();

            ViewBag.Jokes = duckJokes;

            return View();
        }
    }
}
