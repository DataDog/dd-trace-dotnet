using WebService.Models;
using System.Collections.Generic;

namespace WebService.Models
{
    public class CourseScheduleWithRooms
    {
        public int ScheduledCapacity { get; set; }

        public List<Room> LecturesRooms { get; set; }

        public List<Room> PracticeRooms { get; set; }
    }
}
