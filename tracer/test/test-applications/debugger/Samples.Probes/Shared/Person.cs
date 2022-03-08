using System;
using System.Collections.Generic;

namespace Samples.Probes.Shared;

internal class Person
{
    private int _shouldCloned;
    internal Person(string name, double age, Address address, Guid id, List<Person> children)
    {
        _shouldCloned = (int)age;
        Name = name;
        Age = age;
        Address = address;
        Id = id;
        Children = children;
    }

    public string Name { get; }
    public double Age { get; }
    public Address Address { get; }
    public Guid Id { get; }
    public List<Person> Children { get; }

    public int ShouldNotCloned
    {
        get
        {
            var hash = GetHashCode();
            if (hash % 2 == 0)
            {
                return hash;
            }
            else
            {
                return _shouldCloned;
            }
        }
        set
        {
            _shouldCloned = value;
        }
    }

    public override string ToString()
    {
        return $"{Name} ({Age})";
    }
}
