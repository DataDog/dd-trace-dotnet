// <copyright file="Address.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.Probes.Contracts.Shared
{
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
}
