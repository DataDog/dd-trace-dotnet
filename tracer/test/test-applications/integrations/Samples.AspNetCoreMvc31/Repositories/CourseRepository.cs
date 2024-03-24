using WebService.Models;
using WebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebService.Repositories
{
    public class CourseRepository
    {
        private static readonly List<Course> Courses = new List<Course>();

        static CourseRepository()
        {
            
            Courses.Add(new Course()
            {
                Name = "Intoduction to superheroes",
                Code = "INTRO-001",
                StudentCount = 300,
                LectureRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.Projector,
                    LectureFeatures.Wifi
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.Projector,
                    LectureFeatures.Wifi
                },
                TeacherCount = 2,
                TeacherAssistantCount = 10,
            });

            Courses.Add(new Course()
            {
                Name = "Legalities for vigilantes",
                Code = "LEGAL-001",
                StudentCount = 100,
                LectureRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.Projector,
                    LectureFeatures.Wifi
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.Projector,
                    LectureFeatures.Wifi
                },
                TeacherCount = 2,
                TeacherAssistantCount = 4,
            });

            Courses.Add(new Course()
            {
                Name = "Hand combat for beginners",
                Code = "HAND-001",
                StudentCount = 100,
                LectureRequiredFeatures = new HashSet<string>()
                {
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.SoundProof,
                },
                TeacherCount = 2,
                TeacherAssistantCount = 4,
            });

            Courses.Add(new Course()
            {
                Name = "Hand combat for level 2",
                Code = "HAND-002",
                StudentCount = 50,
                LectureRequiredFeatures = new HashSet<string>()
                {
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.SoundProof,
                },
                TeacherCount = 1,
                TeacherAssistantCount = 3,
            });

            Courses.Add(new Course()
            {
                Name = "Powered Flight",
                Code = "FLIGHT-001",
                StudentCount = 20,
                LectureRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.VR,
                    LectureFeatures.StaticShield
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.SoundProof,
                    LectureFeatures.StaticShield
                },
                TeacherCount = 1,
                TeacherAssistantCount = 2,
            });

            Courses.Add(new Course()
            {
                Name = "Advance electrical attacks",
                Code = "AEA-400",
                StudentCount = 20,
                LectureRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.HighPower,
                    LectureFeatures.Projector,
                },
                PracticeRequiredFeatures = new HashSet<string>()
                {
                    LectureFeatures.HighPower,
                    LectureFeatures.Wifi,
                },
                TeacherCount = 1,
                TeacherAssistantCount = 3,
            });
        }

        public static IEnumerable<Course> GetCourses()
        {
            return Courses;
        }

        internal static Course GetCourseById(string id)
        {
            return GetCourses().First(course => course.Code == id);
        }
    }
}
