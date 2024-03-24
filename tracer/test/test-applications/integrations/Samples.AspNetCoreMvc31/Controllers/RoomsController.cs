using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebService.Repositories;
using WebService.Services;

namespace WebService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly ILogger<RoomsController> _logger;
        private readonly ICourseService _courseService;

        public RoomsController(ILogger<RoomsController> logger, ICourseService courseService)
        {
            _logger = logger;
            _courseService = courseService;
        }

        [HttpGet]
        public IEnumerable<Room> Get([FromQuery] string features = null)
        {
            var featuresParsed = (features ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries);
            if (featuresParsed.Length > 0)
            {
                return RoomRepository.GetRooms(featuresParsed);
            }
            else
            {
                return RoomRepository.GetRooms();
            }
        }

        [HttpGet("available")]
        public IEnumerable<Room> GetAvailableRooms([FromQuery] string features = "")
        {
            var featuresParsed = features.Split(",", StringSplitOptions.RemoveEmptyEntries);

            var rooms = (featuresParsed.Length > 0 ? RoomRepository.GetAvailableRooms(featuresParsed) : RoomRepository.GetAvailableRooms()).ToList();

            if (rooms.Any(room => !room.Available))
            {
                throw new Exception("not all room where available, there might be a bug");
            }

            return rooms;
        }

        [HttpGet("{id}")]
        public Room GetRoom([FromRoute] string id)
        {
            return RoomRepository.GetRoomById(id);
        }

        [HttpGet("{id}/book")]
        public Room BookRoom([FromRoute] string id, [FromQuery] string course)
        {
            if (string.IsNullOrWhiteSpace(course))
            {
                throw new Exception("can't book a room without course ID");
            }

            var room = RoomRepository.GetRoomById(id);

            if (room.Available || room.Course == course)
            {
                room.Course = course;
            }
            else
            {
                throw new Exception("room is already booked by another course");
            }

            return room;
        }

        [HttpGet("{id}/unbook")]
        public Room UnbookRoom([FromRoute] string id, [FromQuery] string course)
        {
            if (string.IsNullOrWhiteSpace(course))
            {
                throw new Exception("can't book a room without course ID");
            }

            var room = RoomRepository.GetRoomById(id);

            if (room.Available || room.Course == course)
            {
                room.Course = null;
            }
            else
            {
                throw new Exception("room is already booked by another course");
            }

            return room;
        }

        [HttpGet("{id}/unassign")]
        public async Task<Room> UnassignRoom([FromRoute] string id)
        {
            var room = RoomRepository.GetRoomById(id);

            if (room.Assigned)
            {
                await _courseService.UnassignRoomFromCourse(room.Course, room.Id);
                room.Course = null;
            }
            return room;
        }

        [HttpGet("{id}/status")]
        public async Task<RoomStatus> GetRoomStatus([FromRoute] string id)
        {
            var room = RoomRepository.GetRoomById(id);
            return room.Status;
        }

        [HttpPost("{id}/status")]
        public async Task<RoomStatus> SetRoomStatus([FromRoute] string id, [FromBody] RoomStatus status)
        {
            var room = RoomRepository.GetRoomById(id);

            room.Status = status;
            if (room.Assigned && room.Status != RoomStatus.Ready)
            {
                await _courseService.UnassignRoomFromCourse(room.Course, room.Id);
                room.Course = null;
            }
            return room.Status;
        }

        [HttpGet("stats")]
        public Dictionary<string, int> GetStats()
        {
            var rooms = RoomRepository.GetRooms();

            var total = rooms.Count();
            var assignedRooms = rooms.Count(room => !room.Available);
            var roomsReady = rooms.Count(room => room.Status == RoomStatus.Ready);
            var outOfOrderRooms = rooms.Count(room => room.Status == RoomStatus.OutOfOrder);
            var roomsBeenMaintained = rooms.Count(room => room.Status == RoomStatus.Maintenance);
            var destroyedRooms = rooms.Count(room => room.Status == RoomStatus.Destroyed);
            var totalCapacity = rooms.Sum(room => room.Status == RoomStatus.Ready ? room.MaxCapacity : 0);
            var totalAssignedCapacity = rooms.Sum(room => room.Available ? 0 : room.MaxCapacity);

            return new Dictionary<string, int>
            {
                { "TotalRooms", total },
                { "AssignedRooms", assignedRooms },
                { "RoomsReady", roomsReady  },
                { "OutOfOrderRooms", outOfOrderRooms },
                { "RoomsBeenMaintained ", roomsBeenMaintained  },
                { "DestroyedRooms", destroyedRooms },
                { "TotalCapacity", totalCapacity },
                { "TotalAssignedCapacity", totalAssignedCapacity },
            };
        }
    }
}
