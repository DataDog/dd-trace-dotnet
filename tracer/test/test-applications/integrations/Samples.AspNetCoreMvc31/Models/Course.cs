using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebService.Models
{
    public class Course
    {
        public string Name { get; set; }

        public string Code { get; set; }

        public int StudentCount { get; set; }

        public int TeacherCount { get; set; }
        public int TeacherAssistantCount { get; set; }

        public HashSet<string> LectureRequiredFeatures { get; set; }

        public HashSet<string> PracticeRequiredFeatures { get; set; }

        public CourseSchedule Schedule { get; set; }
    }
}
