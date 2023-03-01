using System.Security.Claims;

namespace Samples.GraphQL7
{
    public class GraphQLUserContext
    {
        public ClaimsPrincipal User { get; set; }
    }
}
