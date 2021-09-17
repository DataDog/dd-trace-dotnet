using System;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.GraphQL3.StarWars
{
    public class StarWarsSchema : Schema
    {
        public StarWarsSchema(IServiceProvider resolver)
            : base(resolver)
        {
            Query = resolver.GetService<StarWarsQuery>();
            Mutation = resolver.GetService<StarWarsMutation>();
        }
    }
}
