using System.Collections.Generic;

namespace Samples.Security.AspNetCore5.Models
{
    public class ComplexModel
    {
        public string Name { get; set; } 
        public string LastName { get; set; } 
        public int Age { get; set; } 
        public IEnumerable<Dog> Dogs { get; set; }
        public Dog[] OtherDogs { get; set; }
        public Address Address { get; set; } 
        public Address Address2 { get; set; } 
        public string Gender { get; set; } 
        public double[] Windows { get; set; }
    }
    public class Dog
    {
        public string Name { get; set; }

        public IEnumerable<Dog> Dogs { get; set; }

    }

    public class Address
    {
        public string NameStreet { get; set; }
        public int Number { get; set; }
        public bool IsHouse { get; set; }

        public City City { get; set; }
    }

    public class City
    {
        public string Name { get; set; }
        public Country Country { get; set; }

        public Language Language { get; set; }
    }

    public class Language
    {
        public string Name { get; set; } = "Spanish";
    }

    public class Country
    {
        public string Name { get; set; }
        public Continent Continent { get; set; }
    }

    public class Continent
    {
        public string Name { get; set; }
        public Planet Planet { get; set; }

    }

    public class Planet
    {
        public string Name { get; set; } = "Earth";
    }
}
