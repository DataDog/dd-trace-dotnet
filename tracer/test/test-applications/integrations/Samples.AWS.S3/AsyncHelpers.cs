using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class AsyncHelpers
    {
        public static async Task StartS3Tasks(AmazonS3Client eventBridgeClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                // Allow time for the resource to be deleted
                Thread.Sleep(1000);
            }
        }
    }
}
