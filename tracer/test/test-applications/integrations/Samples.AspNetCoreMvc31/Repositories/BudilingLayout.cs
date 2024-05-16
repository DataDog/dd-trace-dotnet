using System.Collections.Generic;

namespace WebService.Repositories
{
    class BuildingLayout
    {
        public string Name;
        public List<BuildingFloorLayout> Floors;
        public List<RoomFeature> Features;

        public string Code { get; internal set; }
    }
}
