using System.Security.Claims;

namespace Samples.GraphQL
{
    public class GraphQLUserContext
    {
        public ClaimsPrincipal User { get; set; }
    }
}
