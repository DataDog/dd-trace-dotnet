using System;
using System.IO;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Reflection;

using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Samples.AWS.Lambda
{
    public class Function
    {
        private static async Task Main(string[] args)
        {
            // no-op
        }

        public object HandlerOne(CustomInput request)
        {
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerTwo(CustomInput request, ILambdaContext context)
        {
            return new { statusCode = 200, body = "ok!" };
        }
    }

    public class CustomInput
    {
        public string Field1 { get; set; }
        public int Field2 { get; set; }
    }
}
