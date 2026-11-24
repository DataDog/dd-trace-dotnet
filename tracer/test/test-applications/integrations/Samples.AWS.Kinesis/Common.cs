using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Samples.AWS.Kinesis;

public class Common
{
    // Set by AwsKinesisTests only, so that the Data Streams test, which runs the same sample, keeps its checkpoints
    public static bool UseStreamArn => Environment.GetEnvironmentVariable("KINESIS_SAMPLE_USE_STREAM_ARN") == "true";

    // The stream's ARN with LocalStack's default region and account
    public const string StreamArn = "arn:aws:kinesis:us-east-1:000000000000:stream/MyStreamName";

    public static MemoryStream DictionaryToMemoryStream(Dictionary<string, object> dictionary)
    {
        var jsonString = JsonConvert.SerializeObject(dictionary);
        var bytes = Encoding.UTF8.GetBytes(jsonString);
        return new MemoryStream(bytes);
    }
}
