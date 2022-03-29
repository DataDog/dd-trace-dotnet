using GraphQL;
using GraphQL.Types;
using Samples.GraphQL4.StarWars.Types;

namespace Samples.GraphQL4.StarWars
{
    public class StarWarsMutation : ObjectGraphType<object>
    {
        public StarWarsMutation(StarWarsData data)
        {
            Name = "Mutation";
            var queryArgumentArray = new QueryArgument[1];
            var queryArgument = new QueryArgument<NonNullGraphType<HumanInputType>> { Name = "human" };
            queryArgumentArray[0] = queryArgument;
            Field<HumanType>("createHuman", null, new QueryArguments(queryArgumentArray), context => data.AddHuman(context.GetArgument<Human>("human")));
        }
    }
}
