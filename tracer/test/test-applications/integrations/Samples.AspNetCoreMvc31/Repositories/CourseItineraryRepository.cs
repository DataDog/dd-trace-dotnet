using WebService.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebService.Repositories
{
    public static class CourseItineraryRepository
    {
        private static readonly List<CourseItinerary> Courses = new List<CourseItinerary>();

        private static Dictionary<int, string> _lectureDescriptions = new Dictionary<int, string>
        {
            {1, "Introduction" },
            {2, "Basics of {Name}" },
            {3, "History of {Name}" },
            {4, "Limitations of {Name}" },
            {5, "Advance techniques and methods" },
            {6, "Current trends" },
            {7, "Counter measures " },
            {8, "Looking to the future" }
        };

        private static Dictionary<int, string> _practiceDescriptions = new Dictionary<int, string>
        {
            {1, "Introduction" },
            {2, "Basic terms" },
            {3, "Workshop #1" },
            {4, "Workshop #2" },
            {5, "Revview of advance techniques and methods" },
            {6, "Workshop #3" },
            {7, "Workshop #4" },
            {8, "Final exam" }
        };


        static CourseItineraryRepository()
        {

        }

        private static void AddWeeklyItinerary(this CourseItinerary itinerary, int weekNumber, DateTime weekDate, Course course)
        {
            var random = new Random(course.Code.GetHashCode());
            var lectureDate = weekDate.AddDays(random.Next(0, 2)).AddHours(random.Next(17, 22));
            var practiceDate = weekDate.AddDays(random.Next(3, 4)).AddHours(random.Next(17, 22));

            itinerary.Lectures.Add(new SessionDescription()
            {
                SessionNumber = weekNumber,
                Date = lectureDate,
                Title = _lectureDescriptions[weekNumber].Replace("{Name}",course.Name),
                Description = "To see detailed description please log-in"
            });

            itinerary.Practices.Add(new SessionDescription()
            {
                SessionNumber = weekNumber,
                Date = practiceDate,
                Title = _practiceDescriptions[weekNumber].Replace("{Name}", course.Name),
                Description = "To see detailed description please log-in"
            });

            itinerary.Assignments.Add(new AssignmentDescription()
            {
                AssignmnetNumber = weekNumber,
                PublishDate = practiceDate.AddHours(1),
                DueDate = weekDate.AddDays(7),
                Title = (random.NextDouble() < 0.2 ? "Group Assignment" : "Personal Assignment") + " #" + weekNumber ,
                Description = "To see detailed description please log-in"
            });
        }

        public static async Task<CourseItinerary> GetCourseItinerary(Course course)
        {
            var today = DateTime.Today;
            var lastMonth = today.AddMonths(-1);

            var startOfWeek = lastMonth.AddDays(1 - (int)lastMonth.DayOfWeek);

            var itinerary = new CourseItinerary()
            {
                Lectures = new List<SessionDescription>(),
                Practices = new List<SessionDescription>(),
                Assignments = new List<AssignmentDescription>()
            };

            for (var week = 1; week <= 8; week += 1)
            {
                itinerary.AddWeeklyItinerary(week, startOfWeek, course);
                startOfWeek = startOfWeek.AddDays(7);
            }

            return itinerary;
        }
    }
}
