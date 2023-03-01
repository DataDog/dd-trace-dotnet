using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Samples.GraphQL7.StarWars.Types;

namespace Samples.GraphQL7.StarWars
{
    public class StarWarsData
    {
        private readonly List<Droid> _droids = new();
        private readonly List<Human> _humans = new();

        public StarWarsData()
        {
            var humans1 = _humans;
            var human1 = new Human();
            human1.Id = "1";
            human1.Name = "Luke";
            human1.Friends = new string[2] { "3", "4" };
            human1.AppearsIn = new int[3] { 4, 5, 6 };
            human1.HomePlanet = "Tatooine";
            var human2 = human1;
            humans1.Add(human2);
            var humans2 = _humans;
            var human3 = new Human();
            human3.Id = "2";
            human3.Name = "Vader";
            human3.AppearsIn = new int[3] { 4, 5, 6 };
            human3.HomePlanet = "Tatooine";
            var human4 = human3;
            humans2.Add(human4);
            var droids1 = _droids;
            var droid1 = new Droid();
            droid1.Id = "3";
            droid1.Name = "R2-D2";
            droid1.Friends = new string[2] { "1", "4" };
            droid1.AppearsIn = new int[3] { 4, 5, 6 };
            droid1.PrimaryFunction = "Astromech";
            var droid2 = droid1;
            droids1.Add(droid2);
            var droids2 = _droids;
            var droid3 = new Droid();
            droid3.Id = "4";
            droid3.Name = "C-3PO";
            droid3.AppearsIn = new int[3] { 4, 5, 6 };
            droid3.PrimaryFunction = "Protocol";
            var droid4 = droid3;
            droids2.Add(droid4);
        }

        public IEnumerable<StarWarsCharacter> GetFriends(StarWarsCharacter character)
        {
            if (character == null)
            {
                return null;
            }

            var starWarsCharacterList = new List<StarWarsCharacter>();
            var lookup = character.Friends;
            if (lookup != null)
            {
                starWarsCharacterList.AddRange(_humans.Where(h => lookup.Contains(h.Id)));
                starWarsCharacterList.AddRange(_droids.Where(h => lookup.Contains(h.Id)));
            }

            return starWarsCharacterList;
        }

        public Task<Human> GetHumanByIdAsync(string id)
        {
            return Task.FromResult(_humans.FirstOrDefault(h => h.Id == id));
        }

        public Task<Droid> GetDroidByIdAsync(string id)
        {
            return Task.FromResult(_droids.FirstOrDefault(h => h.Id == id));
        }

        public Human AddHuman(Human human)
        {
            human.Id = Guid.NewGuid().ToString();
            _humans.Add(human);
            return human;
        }
    }
}
