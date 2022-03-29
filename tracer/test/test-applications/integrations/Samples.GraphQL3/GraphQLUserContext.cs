using System.Security.Claims;

namespace Samples.GraphQL3
{
    public class GraphQLUserContext
    {
        public ClaimsPrincipal User { get; set; }
    }
}
