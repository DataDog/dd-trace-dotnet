using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Samples.AspNetCore5.Data;

namespace Samples.AspNetCore5.Controllers
{
    [Route("[controller]")]
    public class PostsController : Controller
    {
        private readonly IConfiguration configuration;

        public PostsController(IConfiguration configuration) => this.configuration = configuration;


        public IImmutableList<dynamic> SelectDynamic(string query)
        {
            var connString = configuration.GetConnectionString("DefaultConnection");
            using var conn = DatabaseHelper.GetConnectionForDb(connString);
            conn!.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
            using var reader = cmd.ExecuteReader();

            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var result = reader.Cast<IDataRecord>().Select(record =>
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;
                foreach (var name in names)
                {
                    expando[name] = record[name];
                }

                return expando as dynamic;
            }).ToImmutableList();
            return result;
        }

        // GET: posts
        [HttpGet]
        public ActionResult Index()
        {
            var query = $"select * from Post";
            var posts = SelectDynamic(query);
            return View(posts);
        }

        // GET: posts/5
        [HttpGet("{str}")]
        public ActionResult Index(string str)
        {
            var query = $"select * from Post where PostId = {str}";
            var contents = SelectDynamic(query);

            return View(contents);
        }
    }
}
