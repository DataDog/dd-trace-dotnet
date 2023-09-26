using System.Web.Http;
using Samples.AspNetMvc5.Models;

namespace Samples.Security.WebApi.Controllers
{
    public class IastController : ApiController
    {
        [AcceptVerbs("GET")]
        public string PathTraversal([FromBody] MiscModel miscModel)
        {
            try
            {
                if (!string.IsNullOrEmpty(miscModel.Id))
                {
                    var result = System.IO.File.ReadAllText(miscModel.Id);
                    return ($"file content: " + result);
                }
                else
                {
                    return "No file was provided";
                }
            }
            catch
            {
                return ("The provided file " + miscModel.Id + " could not be opened");
            }
        }
    }
}
