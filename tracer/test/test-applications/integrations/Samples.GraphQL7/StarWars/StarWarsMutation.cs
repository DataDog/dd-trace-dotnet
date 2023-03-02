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
            Field<HumanType>("createHuman")
                .Argument<NonNullGraphType<HumanInputType>>("human")
                .Resolve(context => data.AddHuman(context.GetArgument<Human>("human")));
        }
    }
}
