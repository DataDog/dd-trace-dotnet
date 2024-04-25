using System.Collections.Generic;
using System.Web.Http;
using System.Web.Mvc;

namespace Samples.Security.WebApi.Controllers
{
    public class UserController : ApiController
    {
        // GET api/user
        public string Get()
        {
            var userId = "user3";

            return Get(userId);
        }

        // GET api/user/<userid>
        public string Get(string id)
        {
            var userId = id ?? "user3";
            Samples.SampleHelpers.SetUser(userId);

            return userId;
        }
    }
}
