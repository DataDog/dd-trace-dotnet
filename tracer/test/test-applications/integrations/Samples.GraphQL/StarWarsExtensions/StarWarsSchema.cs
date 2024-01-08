using System;
using GraphQL;
using GraphQL.StarWars;
using GraphQL.Types;

namespace Samples.GraphQL.StarWarsExtensions
{
    public class StarWarsSchema : Schema
    {
        public StarWarsSchema(IDependencyResolver resolver)
            : base(resolver)
        {
            this.Query = (IObjectGraphType) resolver.Resolve<StarWarsQuery>();
            this.Mutation = (IObjectGraphType) resolver.Resolve<StarWarsMutation>();
        }
    }
}
