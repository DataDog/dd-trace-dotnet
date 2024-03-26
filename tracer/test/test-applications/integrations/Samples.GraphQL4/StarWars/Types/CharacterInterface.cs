using GraphQL.Types;

namespace Samples.GraphQL4.StarWars.Types
{
    public class CharacterInterface : InterfaceGraphType<StarWarsCharacter>
    {
        public CharacterInterface()
        {
            Name = "Character";
            Field(d => d.Id).Description("The id of the character.");
            Field(d => d.Name, nullable: true).Description("The name of the character.");
#if GRAPHQL_7_0
            Field<ListGraphType<EpisodeEnum>>("appearsIn").Description("Which movie they appear in.");
#else
            Field<ListGraphType<EpisodeEnum>>("appearsIn", "Which movie they appear in.");
#endif
        }
    }
}
