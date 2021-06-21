using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using Samples.AspNetMvc5.Data;
using Samples.AspNetMvc5.Models;

namespace Samples.AspNetMvc5.Controllers
{
    public class PostsController : Controller
    {
        private List<dynamic> SelectDynamic(string query)
        {
            var connString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            using (var conn = new SqlConnection(connString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.CommandType = CommandType.Text;
                    using (var reader = cmd.ExecuteReader())
                    {

                        var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                        var result = reader.Cast<IDataRecord>().Select(record =>
                        {
                            var expando = new ExpandoObject() as IDictionary<string, object>;
                            foreach (var name in names)
                            {
                                expando[name] = record[name];
                            }

                            return expando as dynamic;
                        }).ToList();
                        return result;
                    }
                }
            }
        }

        [Route("posts/{id?: string}")]
        public ActionResult Index(string id)
        {
            var query = string.IsNullOrEmpty(id) ? "select * from Post" : $"select * from Post where PostId = {id}";
            // var contents = SelectDynamic(query);
            return View(new[] { new Post { PostId = 1, Title = "test", Body = "test" } });
        }
    }
}
