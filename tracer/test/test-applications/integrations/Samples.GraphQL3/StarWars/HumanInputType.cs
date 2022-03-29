using GraphQL.Types;

namespace Samples.GraphQL3.StarWars
{
    public class HumanInputType : InputObjectGraphType
    {
        public HumanInputType()
        {
            Name = "HumanInput";
            Field<NonNullGraphType<StringGraphType>>("name");
            Field<StringGraphType>("homePlanet");
        }
    }
}
