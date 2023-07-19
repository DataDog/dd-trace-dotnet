#nullable enable

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;
using Samples.Security.AspNetCore5.IdentityStores;
using Datadog.Trace;

namespace Samples.Security.AspNetCore5.Controllers;

public class CreateExtraServiceController: Controller
{
    [HttpGet]
    public IActionResult Index(string? serviceName)
    {
        using var scope = Tracer.Instance.StartActive("create-extra-service");
        scope.Span.ServiceName = serviceName;

        // fake a http.url tag so it'll appear in the snapshot
        scope.Span.SetTag("http.url", "http://localhost:00000/createextraservice/?serviceName=extraVegetables");

        return Content("Created: " + serviceName);
    }
}
