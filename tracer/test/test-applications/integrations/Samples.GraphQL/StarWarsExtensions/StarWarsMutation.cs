using System.Collections.Generic;
using System.Linq;
using GraphQL.StarWars;
using GraphQL.StarWars.Types;
using GraphQL.Types;

namespace Samples.GraphQL.StarWarsExtensions
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

            Field<ListGraphType<HumanType>>(
                "createHumans",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>> { Name = "names", Description = "The names of the humans to create" }
                ),
                resolve: context =>
                {
                    var names = context.GetArgument<List<string>>("names");
                    return names.Select(name =>
                    {
                        var human = new Human();
                        human.Name = name;
                        return data.AddHuman(human);
                    });
                }
            );
        }
    }
}
