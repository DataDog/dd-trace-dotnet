using System.Security.Claims;

namespace Samples.GraphQL4
{
    public class GraphQLUserContext
    {
        public ClaimsPrincipal User { get; set; }
    }
}
