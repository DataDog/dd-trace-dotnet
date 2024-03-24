using System.Collections.Generic;
using System.Linq;

namespace WebService.Models
{
    public class CourseSchedule
    {
        public int ScheduledCapacity { get; set; }

        public List<string> LectureRooms { get; set; }
        public List<string> PracticeRooms { get; set; }
        public IEnumerable<string> AllRooms
        {
            get
            {
                return (LectureRooms ?? new List<string>()).Concat(PracticeRooms ?? new List<string>());
            }
        }
    }
}
