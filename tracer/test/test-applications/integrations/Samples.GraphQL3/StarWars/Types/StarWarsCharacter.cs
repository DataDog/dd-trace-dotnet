namespace Samples.GraphQL3.StarWars.Types
{
    public abstract class StarWarsCharacter
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string[] Friends { get; set; }

        public int[] AppearsIn { get; set; }
    }
}
