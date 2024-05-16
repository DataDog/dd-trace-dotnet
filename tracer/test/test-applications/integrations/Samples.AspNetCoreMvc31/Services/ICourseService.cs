using WebService.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebService.Services
{
    public interface ICourseService
    {
        Task<Course> GetCourseById(string id);

        Task<Course> UnassignRoomFromCourse(string id,string room);
    }
}
