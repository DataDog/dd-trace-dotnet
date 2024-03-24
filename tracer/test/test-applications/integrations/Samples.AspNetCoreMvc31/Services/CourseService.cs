using Microsoft.Extensions.Configuration;
using WebService.Models;
using WebService.Extensions;
using WebService.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebService.Services
{
    public class CourseService : ICourseService
    {
        public CourseService(IConfiguration configuration)
        {
        }
        
        public async Task<Course> GetCourseById(string id)
        {
            return CourseRepository.GetCourseById(id);
        }

        public async Task<List<Course>> GetCourses()
        {
            return CourseRepository.GetCourses().ToList();
        }

        public async Task<Course> UnassignRoomFromCourse(string id, string room)
        {
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

            return course;
        }
    }
}
