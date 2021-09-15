using System;
using GraphQL;
using GraphQL.Types;
using Samples.GraphQL4.StarWars.Types;

namespace Samples.GraphQL4.StarWars
{
    public class StarWarsQuery : ObjectGraphType<object>
    {
        public StarWarsQuery(StarWarsData data)
        {
            Name = "Query";
            Field<CharacterInterface>("hero", description: null, null, context => data.GetDroidByIdAsync("3"));
            var queryArgumentArray1 = new QueryArgument[1];
            var queryArgument1 = new QueryArgument<NonNullGraphType<StringGraphType>>();
            queryArgument1.Name = "id";
            queryArgument1.Description = "id of the human";
            queryArgumentArray1[0] = queryArgument1;
            Field<HumanType>("human", description: null, new QueryArguments(queryArgumentArray1), context => data.GetHumanByIdAsync(context.GetArgument<string>("id")));
            Func<ResolveFieldContext<object>, string, object> func = (context, id) => data.GetDroidByIdAsync(id);
            var queryArgumentArray2 = new QueryArgument[1];
            var queryArgument2 = new QueryArgument<NonNullGraphType<StringGraphType>>();
            queryArgument2.Name = "id";
            queryArgument2.Description = "id of the droid";
            queryArgumentArray2[0] = queryArgument2;
            FieldDelegate<DroidType>("droid", arguments: new QueryArguments(queryArgumentArray2), resolve: func);
        }
    }
}
