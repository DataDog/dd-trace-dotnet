using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [Route("[controller]")]
    public class DataController : Controller
    {
        [HttpPost]
        public IActionResult Index([FromBody]object body)
        {
            return Content("Received\n");
        }

        [Route("model")]
        public ActionResult<MyResponseModel> Model(MyModel model)
        {
            return new ActionResult<MyResponseModel>(new MyResponseModel
            {
                PropertyResponse = "toto",
                PropertyResponse2 = (long)30.5,
                PropertyResponse3 = model.Property4 + 2.5,
                PropertyResponse4 = model.Property4 + 2
            });
        }
    }
}
