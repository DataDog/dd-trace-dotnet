using Microsoft.Extensions.Configuration;
using WebService.Models;
using WebService.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WebService.Repositories;

namespace WebService.Services
{
    public class RoomService : IRoomService
    {
        public RoomService(IConfiguration configuration)
        {
        }

        public async Task<Room> GetRoomById(string id)
        {
            return RoomRepository.GetRoomById(id);
        }

        public async Task<List<Room>> GetAvailableRooms(string[] features)
        {
            return RoomRepository.GetAvailableRooms(features).ToList();
        }

        public async Task BookRoom(Course course, string roomId)
        {
            var room = RoomRepository.GetRoomById(roomId);

            if (room.Available || room.Course == course.Code)
            {
                room.Course = course.Code;
            }
            else
            {
                throw new Exception("room is already booked by another course");
            }
        }

        public async Task UnbookRoom(Course course, string roomId)
        {
            var room = RoomRepository.GetRoomById(roomId);

            if (room.Available || room.Course == course.Code)
            {
                room.Course = null;
            }
            else
            {
                throw new Exception("room is already booked by another course");
            }
        }

        public async Task BookSchedule(Course course, CourseSchedule schedule)
        {
            foreach (var room in schedule.AllRooms)
            {
                await this.BookRoom(course, room);
            }
        }

        public async Task UnbookSchedule(Course course, CourseSchedule schedule)
        {
            foreach (var room in schedule.AllRooms)
            {
                await this.UnbookRoom(course, room);
            }
        }
    }
}
