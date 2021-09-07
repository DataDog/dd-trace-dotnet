// <copyright file="IntakeConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Security.IntegrationTests
{
    internal class IntakeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(AppSec.EventModel.Batch.Intake).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);
            var children = jo.Children();
            var events = (children.First(c => c.Path == "events") as JProperty).Children().FirstOrDefault();
            var deserializedEvents = new List<IEvent>();
            if (events != null)
            {
                foreach (var item in events)
                {
                    // todo other cases
                    switch (item["event_type"].Value<string>())
                    {
                        case "appsec.threat.attack":
                            var attack = item.ToObject<AppSec.EventModel.Attack>();
                            deserializedEvents.Add(attack);
                            break;
                        default: break;
                    }
                }
            }

            var intake = new AppSec.EventModel.Batch.Intake
            {
                Events = deserializedEvents,
                IdemPotencyKey = children.First(c => c.Path == "idempotency_key")?.First.Value<string>(),
                ProtocolVersion = children.First(c => c.Path == "protocol_version").First().Value<int>(),
            };

            return intake;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => serializer.Serialize(writer, value);
    }
}
