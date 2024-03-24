using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebService.Models
{
    public enum RoomStatus
    {
        Ready,
        Maintenance,
        OutOfOrder,
        Destroyed
    }

    public class Room
    {
        public string Id { get; set; }

        public string Building { get; set; }

        public string Floor { get; set; }

        public int MaxCapacity { get; set; }

        public HashSet<string> Features { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public RoomStatus Status { get; set; } = RoomStatus.Ready;

        public string Course { get; set; }

        public bool Available
        {
            get
            {
                return string.IsNullOrEmpty(Course) && Status == RoomStatus.Ready;
            }
        }

        public bool Assigned
        {
            get
            {
                return !string.IsNullOrEmpty(Course);
            }
        }
    }
}
