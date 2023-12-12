using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [Route("[controller]")]
    [ApiController]
    public class DataApiController : Controller
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


        [Route("empty-model")]
        public ActionResult<MyResponseModel> EmptyModel(MyModel model)
        {
            return new ActionResult<MyResponseModel>((MyResponseModel)null);
        }

        [Route("array")]
        public IActionResult Array(IEnumerable<string> model)
        {
            return Content($"Received array : {string.Join(',', model)}");
        }

        [Route("complex-model")]
        public IActionResult Model(ComplexModel model)
        {
            return Content($"Received model with properties: {model}");
        }
    }
}
