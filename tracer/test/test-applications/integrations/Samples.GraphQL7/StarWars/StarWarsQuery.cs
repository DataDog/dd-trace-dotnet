using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Samples.GraphQL7.StarWars.Types;

namespace Samples.GraphQL7.StarWars
{
    public class StarWarsQuery : ObjectGraphType<object>
    {
        public StarWarsQuery(StarWarsData data)
        {
            Name = "Query";
            
            Func<ResolveFieldContext<object>, string, Task<Droid>> func = (context, id) => data.GetDroidByIdAsync(id);
            
            Field<CharacterInterface>("hero").ResolveAsync(async context => await data.GetDroidByIdAsync("3"));
            Field<HumanType>("human").Argument<NonNullGraphType<StringGraphType>>("id", "id of the human")
                .ResolveAsync(async context => await data.GetHumanByIdAsync(context.GetArgument<string>("id")));
            Field<DroidType>("droid").Argument<NonNullGraphType<StringGraphType>>("id", "id of the droid")
                .ResolveDelegate(func);
        }
    }
}
