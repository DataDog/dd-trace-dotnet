using WebService.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebService.Services
{
    public interface IRoomService
    {
        Task<Room> GetRoomById(string id);

        Task<List<Room>> GetAvailableRooms(string[] features);

        Task BookRoom(Course course, string room);

        Task BookSchedule(Course course,CourseSchedule schedule);

        Task UnbookSchedule(Course course, CourseSchedule schedule);
    }
}
