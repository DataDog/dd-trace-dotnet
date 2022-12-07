namespace Samples.Probes.TestRuns.Shared;

internal record struct Address
{
    public string Street { get; set; }
    public int Number { get; set; }
    public Place City { get; set; }
    public BuildingType HomeType { get; set; }
}

internal struct Place
{
    public PlaceType Type { get; set; }
    public string Name { get; set; }
}

internal enum PlaceType
{
    City,
    Country
}

internal enum BuildingType
{
    Cottage,
    Duplex,
    House,
    Hotel,
    Resort
}

