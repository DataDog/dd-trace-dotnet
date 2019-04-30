using System;
using System.Collections.Generic;

namespace Datadog.FakeIntegration
{
    public class WoofCallback
    {
        public WoofCallback()
        {
            Callbacks = new List<Action<WoofRecord>>()
            {
                r => Console.WriteLine(r.Id),
            };
        }

        public List<Action<WoofRecord>> Callbacks { get; set; }
    }
}
