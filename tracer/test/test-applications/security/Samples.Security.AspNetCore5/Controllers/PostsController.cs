using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Dynamic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Samples.Security.AspNetCore5.Data;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    public class PostsController : Controller
    {
        private readonly IConfiguration configuration;

        public PostsController(IConfiguration configuration) => this.configuration = configuration;



        // GET: posts
        [HttpGet]
        public ActionResult Index()
        {
            var query = $"select * from Post";
            var posts = DatabaseHelper.SelectDynamic(configuration, query);
            return View(posts);
        }

        // GET: posts/5
        [HttpGet("{str}")]
        public ActionResult Index(string str)
        {
            var query = $"select * from Post where PostId = {str}";
            var contents = DatabaseHelper.SelectDynamic(configuration, query);

            return View(contents);
        }
    }
}
