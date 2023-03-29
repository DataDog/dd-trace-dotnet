using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncTaskReturnTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Run(async () => { await Method("Foo"); });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        private async Task<RoomStatus> Method(string caller)
        {
            await Task.Yield();
            var room = GetRoomById(caller);
            await Task.Yield();
            return room.Status;
        }

        internal static Room GetRoomById(string id)
        {
            return Rooms.First(room => room.Id == id);
        }

        private static readonly List<Room> Rooms = new List<Room>
        {
            new Room
            {
                Id = "Foo",
                Status = RoomStatus.Maintenance
            },
            new Room
            {
                Id = "Bar"
            },
        };

        internal class Room
        {
            public string Id { get; set; }
            public RoomStatus Status { get; set; } = RoomStatus.Ready;

        }

        internal enum RoomStatus
        {
            Ready,
            Maintenance,
            OutOfOrder,
            Destroyed
        }
    }
}
