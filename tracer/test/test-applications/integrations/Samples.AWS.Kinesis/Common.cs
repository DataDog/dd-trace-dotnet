using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Samples.AWS.Kinesis;

public class Common
{
    public static readonly string StreamName = $"MyStreamName-{Guid.NewGuid():N}";

    public static MemoryStream DictionaryToMemoryStream(Dictionary<string, object> dictionary)
    {
        var jsonString = JsonConvert.SerializeObject(dictionary);
        var bytes = Encoding.UTF8.GetBytes(jsonString);
        return new MemoryStream(bytes);
    }
}
