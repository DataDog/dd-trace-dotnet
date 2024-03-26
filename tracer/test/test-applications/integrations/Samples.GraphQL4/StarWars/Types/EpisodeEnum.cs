using GraphQL.Types;

namespace Samples.GraphQL4.StarWars.Types
{
    public class EpisodeEnum : EnumerationGraphType
    {
        public EpisodeEnum()
        {
            Name = "Episode";
            Description = "One of the films in the Star Wars Trilogy.";
            AddValue("NEWHOPE", "Released in 1977.", 4);
            AddValue("EMPIRE", "Released in 1980.", 5);
            AddValue("JEDI", "Released in 1983.", 6);
        }
#if GRAPHQL_5_0 || GRAPHQL_7_0
        void AddValue(string name, string description, object value)
        {
            Add(name: name, value: value, description: description);
        }
#endif
    }
}
