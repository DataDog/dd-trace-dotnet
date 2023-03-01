using GraphQL;
using GraphQL.Types;
using Samples.GraphQL7.StarWars.Types;

namespace Samples.GraphQL7.StarWars
{
    public class StarWarsMutation : ObjectGraphType<object>
    {
        public StarWarsMutation(StarWarsData data)
        {
            Name = "Mutation";
#if GRAPHQL_7_0
            Field<HumanType>("createHuman")
                .Argument<NonNullGraphType<HumanInputType>>("human")
                .Resolve(context => data.AddHuman(context.GetArgument<Human>("human")));
#else 
            var queryArgumentArray = new QueryArgument[1];
            var queryArgument = new QueryArgument<NonNullGraphType<HumanInputType>> { Name = "human" };
            queryArgumentArray[0] = queryArgument;
            
            Field<HumanType>("createHuman", null, new QueryArguments(queryArgumentArray), context => data.AddHuman(context.GetArgument<Human>("human")));
#endif
        }
    }
}
