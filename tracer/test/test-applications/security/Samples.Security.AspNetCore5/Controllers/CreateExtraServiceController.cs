#nullable enable

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;
using Samples.Security.AspNetCore5.IdentityStores;

namespace Samples.Security.AspNetCore5.Controllers;

public class CreateExtraServiceController: Controller
{
    [HttpGet]
    public IActionResult Index(string? serviceName)
    {
        using var scope = Samples.SampleHelpers.CreateScope("create-extra-service");
        Samples.SampleHelpers.TrySetServiceName(scope, serviceName);

        // fake a http.url tag so it'll appear in the snapshot
        Samples.SampleHelpers.TrySetTag(scope, "http.url", $"http://localhost:00000/createextraservice/?serviceName={serviceName}");

        return Content("Created: " + serviceName);
    }
}
