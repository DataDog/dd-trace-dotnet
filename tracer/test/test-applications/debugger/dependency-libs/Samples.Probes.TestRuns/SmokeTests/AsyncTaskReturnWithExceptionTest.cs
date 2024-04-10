using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncTaskReturnWithExceptionTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Run(async () => { await Method(nameof(RunAsync)); });
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
            return First(Rooms, room => room.Id == id);
        }

        internal static TSource First<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (TSource source1 in source)
            {
                if (predicate(source1))
                    return source1;
            }

            throw new InvalidOperationException("Sequence contains no matching element");
        }

        private static readonly List<Room> Rooms = new List<Room>
        {
            new Room
            {
                Id = "Foo"
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
