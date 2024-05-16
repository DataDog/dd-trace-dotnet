using WebService.Models;
using WebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WebService.Repositories
{
    public static class RoomRepository
    {
        //private static List<Room> Rooms = new List<Room>();

        private static List<Room> Rooms = new List<Room>
        {
            new Room
            {
                Id = "MB-M01",
                Building = "Main Building",
                Floor = "Main Floor",
                MaxCapacity = 250,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-M02",
                Building = "Main Building",
                Floor = "Main Floor",
                MaxCapacity = 250,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-201",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-202",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-203",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-204",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-205",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-206",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>
                {
                    "Projector"
                }
            },
            new Room
            {
                Id = "MB-207",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-208",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "MB-209",
                Building = "Main Building",
                Floor = "2",
                MaxCapacity = 25,
                Features = new HashSet<string>()
            },
            new Room
            {
                Id = "PP-M01",
                Building = "Parker Building",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "PP-M02",
                Building = "Parker Building",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "PP-201",
                Building = "Parker Building",
                Floor = "2",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi"
                }
            },
            new Room
            {
                Id = "PP-202",
                Building = "Parker Building",
                Floor = "2",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi",
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-203",
                Building = "Parker Building",
                Floor = "2",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi",
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-204",
                Building = "Parker Building",
                Floor = "2",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi"
                }
            },
            new Room
            {
                Id = "PP-301",
                Building = "Parker Building",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>()
            },
            new Room
            {
                Id = "PP-302",
                Building = "Parker Building",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-303",
                Building = "Parker Building",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-304",
                Building = "Parker Building",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "PP-401",
                Building = "Parker Building",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-402",
                Building = "Parker Building",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "PP-403",
                Building = "Parker Building",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi"
                }
            },
            new Room
            {
                Id = "PP-404",
                Building = "Parker Building",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>()
            },
            new Room
            {
                Id = "PP-501",
                Building = "Parker Building",
                Floor = "5",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Wifi",
                    "High Power"
                }
            },
            new Room
            {
                Id = "PP-502",
                Building = "Parker Building",
                Floor = "5",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector"
                }
            },
            new Room
            {
                Id = "PP-503",
                Building = "Parker Building",
                Floor = "5",
                MaxCapacity = 10,
                Features = new HashSet<string>()
            },
            new Room
            {
                Id = "PP-504",
                Building = "Parker Building",
                Floor = "5",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-B01",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B02",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B03",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B04",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B05",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B06",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B07",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-B08",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B09",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B10",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B11",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B12",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B13",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B14",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B15",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B16",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B17",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B18",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B19",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B20",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B21",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B22",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B23",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B24",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-B25",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B26",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B27",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B28",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B29",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B30",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B31",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B32",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B33",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-B34",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B35",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B36",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B37",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-B38",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-B39",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-B40",
                Building = "Stark Hall",
                Floor = "Basement",
                MaxCapacity = 5,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-M01",
                Building = "Stark Hall",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-M02",
                Building = "Stark Hall",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "High Power",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-M03",
                Building = "Stark Hall",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-M04",
                Building = "Stark Hall",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-201",
                Building = "Stark Hall",
                Floor = "2",
                MaxCapacity = 20,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-202",
                Building = "Stark Hall",
                Floor = "2",
                MaxCapacity = 20,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-203",
                Building = "Stark Hall",
                Floor = "2",
                MaxCapacity = 20,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-204",
                Building = "Stark Hall",
                Floor = "2",
                MaxCapacity = 20,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-301",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-302",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-303",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-304",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-305",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-306",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "SH-307",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-308",
                Building = "Stark Hall",
                Floor = "3",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-401",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-402",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "High Power"
                }
            },
            new Room
            {
                Id = "SH-403",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "High Power",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-404",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-405",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-406",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "Static Shield"
                }
            },
            new Room
            {
                Id = "SH-407",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi",
                    "VR"
                }
            },
            new Room
            {
                Id = "SH-408",
                Building = "Stark Hall",
                Floor = "4",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Projector",
                    "Wifi"
                }
            },
            new Room
            {
                Id = "BW-B01",
                Building = "Banner Warehouse",
                Floor = "Basement",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Reinforced Steel"
                }
            },
            new Room
            {
                Id = "BW-B02",
                Building = "Banner Warehouse",
                Floor = "Basement",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "Reinforced Steel",
                    "Sound Proof"
                }
            },
            new Room
            {
                Id = "BW-B03",
                Building = "Banner Warehouse",
                Floor = "Basement",
                MaxCapacity = 10,
                Features = new HashSet<string>()
            },
            new Room
            {
                Id = "BW-B04",
                Building = "Banner Warehouse",
                Floor = "Basement",
                MaxCapacity = 10,
                Features = new HashSet<string>
                {
                    "Reinforced Steel"
                }
            },
            new Room
            {
                Id = "BW-M01",
                Building = "Banner Warehouse",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "High Power",
                    "Reinforced Steel",
                    "Sound Proof"
                }
            },
            new Room
            {
                Id = "BW-M02",
                Building = "Banner Warehouse",
                Floor = "Main Floor",
                MaxCapacity = 50,
                Features = new HashSet<string>
                {
                    "Static Shield",
                    "High Power",
                    "Reinforced Steel",
                    "Sound Proof"
                }
            }
        };

        internal static Room GetRoomById(string id)
        {
            return Rooms.First(room => room.Id == id);
        }

        static RoomRepository()
        {
            //Rooms.Clear();
            //PopuplateRooms();
        }


        private static void PopuplateRooms()
        {
            PopuplateBuilding(new BuildingLayout
            {
                Name = "Main Building",
                Code = "MB",
                Features = new List<RoomFeature>()
                {
                    new RoomFeature { Feature = RoomFeatures.Projector, Probability = 0.9 },
                    new RoomFeature { Feature = RoomFeatures.Wifi, Probability = 0.75 }
                },
                Floors = new List<BuildingFloorLayout>()
                {
                    new BuildingFloorLayout("Main Floor",
                        new BuildingFloorRoomLayout() { Count = 2, Capcitiy = 250 }),
                    new BuildingFloorLayout("2",
                        new BuildingFloorRoomLayout() { Count = 5, Capcitiy = 10 },
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 6 }
                    )
                }
            });

            PopuplateBuilding(new BuildingLayout
            {
                Name = "Parker Building",
                Code = "PP",
                Features = new List<RoomFeature>()
                {
                    new RoomFeature { Feature = RoomFeatures.Projector, Probability = 0.2 },
                    new RoomFeature { Feature = RoomFeatures.Wifi, Probability = 0.5 },
                    new RoomFeature { Feature = RoomFeatures.StaticShield, Probability = 0.5 },
                    new RoomFeature { Feature = RoomFeatures.HighPower, Probability = 0.2 },
                },
                Floors = new List<BuildingFloorLayout>()
                {
                    new BuildingFloorLayout("Main Floor",
                        new BuildingFloorRoomLayout() { Count = 2, Capcitiy = 50 }),
                    new BuildingFloorLayout("2",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 10 }),
                    new BuildingFloorLayout("3",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 10 }),
                    new BuildingFloorLayout("4",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 10 }),
                    new BuildingFloorLayout("5",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 10 })
                }
            });

            PopuplateBuilding(new BuildingLayout
            {
                Name = "Stark Hall",
                Code = "SH",
                Features = new List<RoomFeature>()
                {
                    new RoomFeature { Feature = RoomFeatures.Projector, Probability = 1.0 },
                    new RoomFeature { Feature = RoomFeatures.Wifi, Probability = 1.0 },
                    new RoomFeature { Feature = RoomFeatures.StaticShield, Probability = 0.5 },
                    new RoomFeature { Feature = RoomFeatures.HighPower, Probability = 0.2 },
                    new RoomFeature { Feature = RoomFeatures.VR, Probability = 0.5 },
                },
                Floors = new List<BuildingFloorLayout>()
                {
                     new BuildingFloorLayout("Basement",
                        new BuildingFloorRoomLayout() { Count = 40, Capcitiy = 5 }),
                    new BuildingFloorLayout("Main Floor",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 50 }),
                    new BuildingFloorLayout("2",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 20 }),
                    new BuildingFloorLayout("3",
                        new BuildingFloorRoomLayout() { Count = 8, Capcitiy = 10 }),
                    new BuildingFloorLayout("4",
                        new BuildingFloorRoomLayout() { Count = 8, Capcitiy = 10 })
                }
            });

            PopuplateBuilding(new BuildingLayout
            {
                Name = "Banner Warehouse",
                Code = "BW",
                Features = new List<RoomFeature>()
                {
                    new RoomFeature { Feature = RoomFeatures.StaticShield, Probability = 0.5 },
                    new RoomFeature { Feature = RoomFeatures.HighPower, Probability = 0.2 },
                    new RoomFeature { Feature = RoomFeatures.ReinforcedSteel, Probability = 0.9 },
                    new RoomFeature { Feature = RoomFeatures.SoundProof, Probability = 0.5 },
                },
                Floors = new List<BuildingFloorLayout>()
                {
                     new BuildingFloorLayout("Basement",
                        new BuildingFloorRoomLayout() { Count = 4, Capcitiy = 10 }),
                    new BuildingFloorLayout("Main Floor",
                        new BuildingFloorRoomLayout() { Count = 2, Capcitiy = 50 }),
                }
            });

            //foreach(var building in new string[]
            //{
            // "Main Building",
            // "Parker Building",
            // "Stark Hall",
            // "Banner Warehouse",
            // "Kent House",
            //})
            // {
            //     P
            // }
        }

        private static void PopuplateBuilding(BuildingLayout buildingLayout)
        {
            var seed = buildingLayout.Name.GetHashCode();
            var random = new Random(seed);

            foreach (var floor in buildingLayout.Floors)
            {
                var roomIndex = 1;
                foreach (var roomLayout in floor.RoomsLayout)
                {
                    for (var i = 0; i < roomLayout.Count; i++)
                    {
                        var room = new Room()
                        {
                            Id = $"{buildingLayout.Code}-{floor.Code}{roomIndex++:00}",
                            Building = buildingLayout.Name,
                            Floor = floor.Name,
                            MaxCapacity = roomLayout.Capcitiy,
                            Features = RandomizeFeatures(random, buildingLayout.Features, roomLayout.Features)
                        };
                        Rooms.Add(room);
                    }
                }
            }
        }

        private static HashSet<string> RandomizeFeatures(Random random, List<RoomFeature> featuresProbs, string[] features)
        {
            var results = new HashSet<string>();

            if (features != null)
            {
                foreach (var feature in features)
                {
                    results.Add(feature);
                }
            }

            if (featuresProbs != null)
            {
                foreach (var feature in featuresProbs)
                {
                    if (random.NextDouble() < feature.Probability)
                    {
                        results.Add(feature.Feature);
                    }
                }
            }

            return results;
        }



        public static IEnumerable<Room> GetRooms()
        {
            return Rooms;
        }

        public static IEnumerable<Room> GetRooms(string[] requiredFeatures)
        {
            return GetRooms().Where(room => requiredFeatures.All(feature => room.Features.Contains(feature)));
        }

        public static IEnumerable<Room> GetAvailableRooms()
        {
            return GetRooms().Where(room => room.Available);
        }

        public static IEnumerable<Room> GetAvailableRooms(string[] requiredFeatures)
        {
            return GetAvailableRooms().Where(room => requiredFeatures.All(feature => room.Features.Contains(feature)));
        }
    }
}
