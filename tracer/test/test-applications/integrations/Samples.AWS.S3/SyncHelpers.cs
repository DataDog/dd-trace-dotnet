#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.S3;
using Amazon.S3.Model;

namespace Samples.AWS.S3
{
    static class SyncHelpers
    {
        public static void StartS3Tasks(AmazonS3Client s3Client)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {

                // Allow time for the resource to be deleted
                Thread.Sleep(1000);
            }
        }
    }
}

#endif
