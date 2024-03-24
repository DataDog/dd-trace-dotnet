using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebService.Models
{
    public class Course
    {
        public virtual string Id { get; set; }
        public virtual string Name { get; set; }

        public virtual string Code { get; set; }

        public virtual int StudentCount { get; set; }

        public virtual int TeacherCount { get; set; }
        public virtual int TeacherAssistantCount { get; set; }

        public virtual HashSet<string> LectureRequiredFeatures { get; set; }

        public virtual HashSet<string> PracticeRequiredFeatures { get; set; }

        public virtual CourseSchedule Schedule { get; set; }
    }
}
