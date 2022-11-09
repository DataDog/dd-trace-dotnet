using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class IastController : ControllerBase
    {
        public IActionResult Index()
        {
            return Content("Ok\n");
        }

        [HttpGet("WeakHashing")]
        public IActionResult WeakHashing()
        {
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            var byteArg = new byte[] { 3, 5, 6 };
            MD5.Create().ComputeHash(byteArg);
            SHA1.Create().ComputeHash(byteArg);
            return Content($"Weak hashes launched\n");
#pragma warning restore SYSLIB0021 // Type or member is obsolete
        }
    }
}
