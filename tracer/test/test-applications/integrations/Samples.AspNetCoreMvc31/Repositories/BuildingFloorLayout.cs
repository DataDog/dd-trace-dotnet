using System.Collections.Generic;

namespace WebService.Repositories
{
    public class BuildingFloorLayout
    {
        public string Name;

        public string Code;

        public List<BuildingFloorRoomLayout> RoomsLayout;

        public BuildingFloorLayout()
        {

        }

        public BuildingFloorLayout(string name, params BuildingFloorRoomLayout[] roomsLayout)
        {
            Name = name;
            Code = name.Substring(0, 1);
            RoomsLayout = new List<BuildingFloorRoomLayout>(roomsLayout);
        }
    }
}
