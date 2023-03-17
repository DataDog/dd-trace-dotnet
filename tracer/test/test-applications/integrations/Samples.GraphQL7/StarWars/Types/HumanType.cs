using GraphQL.Types;

namespace Samples.GraphQL7.StarWars.Types
{
    public class HumanType : ObjectGraphType<Human>
    {
        public HumanType(StarWarsData data)
        {
            Name = "Human";
            Field(h => h.Id).Description("The id of the human.");
            Field(h => h.Name, nullable: true).Description("The name of the human.");
            Field<ListGraphType<CharacterInterface>>("friends").Resolve(context => data.GetFriends(context.Source));
            Field<ListGraphType<EpisodeEnum>>("appearsIn").Description("Which movie they appear in.");
            Field(h => h.HomePlanet, nullable: true).Description("The home planet of the human.");
            Interface<CharacterInterface>();
        }
    }
}
