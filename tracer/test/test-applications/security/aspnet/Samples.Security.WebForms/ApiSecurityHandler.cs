using System.IO;
using System.Web;
using System.Web.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Samples.Security.WebForms
{
    public class ApiSecurityHandler : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";

            string body;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                body = reader.ReadToEnd();
            }

            object received;
            try
            {
                received = string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject(body);
            }
            catch (JsonException)
            {
                received = body;
            }

            var id = context.Request.RequestContext.RouteData?.Values["id"];
            var response = new JObject
            {
                ["success"] = true,
                ["id"] = id?.ToString(),
                ["received"] = received == null ? JValue.CreateNull() : JToken.FromObject(received),
            };

            context.Response.StatusCode = 200;
            context.Response.Write(response.ToString(Formatting.None));
        }
    }

    public class ApiSecurityRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new ApiSecurityHandler();
        }
    }
}
