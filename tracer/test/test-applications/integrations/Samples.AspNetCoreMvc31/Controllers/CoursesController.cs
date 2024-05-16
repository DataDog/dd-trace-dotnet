using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebService.Models;
using WebService.Repositories;
using WebService.Services;

namespace WebService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {

        private readonly ILogger<CoursesController> _logger;
        private readonly IRoomService _roomService;

        public CoursesController(ILogger<CoursesController> logger, IRoomService roomService)
        {
            _logger = logger;
            _roomService = roomService;
        }

        [HttpGet]
        public IEnumerable<Course> Get()
        {
            return CourseRepository.GetCourses();
        }

        [HttpGet("{id}")]
        public Course GetCourse([FromRoute] string id)
        {
            return CourseRepository.GetCourseById(id);
        }

         [HttpGet("{id}/daily-code")]
        public string GetDailyCode([FromRoute] string id, string role)
        {
            var course = CourseRepository.GetCourseById(id);

            var service = new DailyCodeService(course);

            return service.GenerateCodeForDate(DateTime.Today,role);
        }

        [HttpGet("{id}/unassign")]
        public async Task<CourseScheduleWithRooms> UnassignRoom([FromRoute] string id, [FromQuery] string room)
        {
            if (string.IsNullOrEmpty(room))
            {
                throw new Exception("room id can't be null");
            }

            var course = CourseRepository.GetCourseById(id);

            var schedule = course.Schedule;

            if (schedule == null)
            {
                throw new Exception("course have no schedule, can't unassign room");
            }

            if (schedule.LectureRooms != null && schedule.LectureRooms.Contains(room))
            {
                schedule.LectureRooms.Remove(room);
            }

            if (schedule.PracticeRooms != null && schedule.PracticeRooms.Contains(room))
            {
                schedule.PracticeRooms.Remove(room);
            }

            return await GetCourseSchedule(id);
        }

        [HttpGet("{id}/assign")]
        public async Task<CourseScheduleWithRooms> AssignRoom([FromRoute] string id, [FromQuery] string room, [FromQuery] string purpose)
        {
            if (string.IsNullOrEmpty(room))
            {
                throw new Exception("room id can't be null");
            }

            var course = CourseRepository.GetCourseById(id);

            var schedule = course.Schedule;

            if (schedule == null)
            {
                schedule = course.Schedule = new CourseSchedule()
                {
                    LectureRooms = new List<string>(),
                    PracticeRooms = new List<string>(),
                    ScheduledCapacity = 0
                };
            }

            if (purpose == "lecture" && !schedule.LectureRooms.Contains(room))
            {
                schedule.LectureRooms.Add(room);
                await _roomService.BookRoom(course, room);
            }

            if (purpose == "practice" && !schedule.PracticeRooms.Contains(room))
            {
                schedule.PracticeRooms.Add(room);
                await _roomService.BookRoom(course, room);
            }

            return await GetCourseSchedule(id);
        }

        [HttpGet("{id}/schedule")]
        public async Task<CourseScheduleWithRooms> GetCourseSchedule([FromRoute] string id)
        {
            var course = CourseRepository.GetCourseById(id);

            var schedule = course.Schedule;

            if (schedule == null)
            {
                throw new Exception("Course has no schedule yet");
            }

            var results = new CourseScheduleWithRooms()
            {
                ScheduledCapacity = schedule.ScheduledCapacity
            };

            if (schedule.LectureRooms != null)
            {
                results.LecturesRooms = new List<Room>();
                foreach (var roomId in schedule.LectureRooms)
                {
                    results.LecturesRooms.Add(await _roomService.GetRoomById(roomId));
                }
            }

            if (schedule.PracticeRooms != null)
            {
                results.PracticeRooms = new List<Room>();
                foreach (var roomId in schedule.PracticeRooms)
                {
                    results.PracticeRooms.Add(await _roomService.GetRoomById(roomId));
                }
            }

            results.ScheduledCapacity = Math.Min(results.LecturesRooms.Sum(room => room.MaxCapacity), results.PracticeRooms.Sum(room => room.MaxCapacity));

            //it seems that our scheduled capactiy has changed
            if (results.ScheduledCapacity != schedule.ScheduledCapacity)
            {
                schedule.ScheduledCapacity = results.ScheduledCapacity;
            }

            return results;
        }

        [HttpGet("{id}/resolve-schedule")]
        public async Task<CourseScheduleWithRooms> ResolveSchedule([FromRoute] string id)
        {
            var activity = System.Diagnostics.Activity.Current;

            _logger.LogInformation("Activity {@Activity} {id}", activity, activity.Id);

            var course = CourseRepository.GetCourseById(id);

            var oldSchedule = course.Schedule;

            if (oldSchedule != null)
            {
                await _roomService.UnbookSchedule(course, oldSchedule);
            }

            try
            {
                var newSchedule = new CourseSchedule();

                var lectureRooms = await FindAvailableRooms(course.LectureRequiredFeatures.ToArray(), course.StudentCount, course.TeacherCount);
                var practiceRooms = await FindAvailableRooms(course.PracticeRequiredFeatures.ToArray(), course.StudentCount, course.TeacherAssistantCount, lectureRooms.Select(room => room.Id).ToList());

                newSchedule.LectureRooms = lectureRooms.Select(room => room.Id).ToList();
                newSchedule.PracticeRooms = practiceRooms.Select(room => room.Id).ToList();

                newSchedule.ScheduledCapacity = Math.Min(lectureRooms.Sum(room => room.MaxCapacity), practiceRooms.Sum(room => room.MaxCapacity));


                await _roomService.BookSchedule(course, newSchedule);

                course.Schedule = newSchedule;
            }
            catch (Exception)
            {
                if (oldSchedule != null)
                {
                    await _roomService.BookSchedule(course, oldSchedule);
                }
                throw;
            }

            return await GetCourseSchedule(id);
        }

        private async Task<List<Room>> FindAvailableRooms(string[] features, int numberOfStudents, int numberOfTeachers, List<String> excludedRooms = null)
        {
            List<Room> availableLectureRooms = await GetAllAvailableRoomsWithRequiredFeatures(features, excludedRooms);

            //order rooms by size
            availableLectureRooms = availableLectureRooms.OrderByDescending(room => room.MaxCapacity).ToList();

            //schedule rooms until we fill the required capacity
            var scheduledStudents = 0;

            var foundRooms = new List<Room>();

            while (scheduledStudents < numberOfStudents && foundRooms.Count < numberOfTeachers)
            {
                var desiredRoomCapacity = (numberOfStudents - scheduledStudents) / (numberOfTeachers - foundRooms.Count);
                var room = FindOptimalRoom(availableLectureRooms, desiredRoomCapacity);

                if (room == null)
                {
                    _logger.LogWarning("Failed to find available room");
                    break;
                }

                availableLectureRooms.Remove(room);
                foundRooms.Add(room);
                scheduledStudents += room.MaxCapacity;
            }

            return foundRooms;
        }

        private async Task<List<Room>> GetAllAvailableRoomsWithRequiredFeatures(string[] features, List<string> excludedRooms)
        {
            //get all availalbe rooms.
            var availableLectureRooms = await _roomService.GetAvailableRooms(features);

            //filterout rooms from the exculded list
            if (excludedRooms != null)
            {
                availableLectureRooms = availableLectureRooms.Where(room => !excludedRooms.Any(id => id == room.Id)).ToList();
            }

            if (availableLectureRooms == null || availableLectureRooms.Count == 0)
            {
                throw new Exception("No available rooms with required features");
            }

            return availableLectureRooms;
        }

        private Room FindOptimalRoom(IEnumerable<Room> rooms, int desiredRoomCapacity)
        {
            return rooms.LastOrDefault(room => room.MaxCapacity >= desiredRoomCapacity);
        }

        [HttpGet("{id}/security-profile")]
        public async Task<SecurityProfile> GetCourseSecurityProfile(string id)
        {
            var course = CourseRepository.GetCourseById(id);
            return await CourseSecurityProfileRepository.GetSecurityProfile(course);
        }


        [HttpGet("{id}/itinerary")]
        public async Task<CourseItinerary> GetCourseItinerary(string id)
        {
            var course = CourseRepository.GetCourseById(id);
            return await CourseItineraryRepository.GetCourseItinerary(course);
        }

        [HttpGet("stats")]
        public Dictionary<string, int> GetStats()
        {
            var courses = CourseRepository.GetCourses();

            var total = courses.Count();
            var scheduledCourses = courses.Count(course => course.Schedule != null ? course.Schedule.ScheduledCapacity > 0 : false);
            var totalStudents = courses.Sum(course => course.StudentCount);
            var totalScheduled = courses.Sum(course => course.Schedule != null ? course.Schedule.ScheduledCapacity : 0);

            var threads = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

            return new Dictionary<string, int>
            {
                { "TotalCourses", total },
                { "ScheduledCourses", scheduledCourses },
                { "TotalStudents", totalStudents },
                { "TotalScheduled", totalScheduled },
                { "Threads", threads }
            };
        }
    }
}
