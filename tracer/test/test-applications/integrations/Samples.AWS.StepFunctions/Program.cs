using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.StepFunctions;

namespace Samples.AWS.StepFunctions
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Set up AmazonStepFunctionsConfig and redirect to the local message queue instance
            var stepFunctionsClient = GetAmazonStepFunctionsClient();

#if NETFRAMEWORK
            SyncHelpers.StartStepFunctionsTasks(stepFunctionsClient);
#endif
            await AsyncHelpers.StartStepFunctionsTasks(stepFunctionsClient);
        }

        private static AmazonStepFunctionsClient GetAmazonStepFunctionsClient()
        {
            if (Environment.GetEnvironmentVariable("AWS_ACCESSKEY") is string accessKey &&
                Environment.GetEnvironmentVariable("AWS_SECRETKEY") is string secretKey &&
                Environment.GetEnvironmentVariable("AWS_REGION") is string region)
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonStepFunctionsClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
            }
            else
            {
                var awsCredentials = new BasicAWSCredentials("x", "x");

#if STEPFUNCTIONS_3_7_0
                // DisableHostPrefixInjection = true is required to avoid the SDK from adding the service name to the host (sync-localhost) which breaks everything
                // Note: only saw this on .NET Framework
                var stepFunctionsConfig = new AmazonStepFunctionsConfig { ServiceURL = "http://" + Host(), DisableHostPrefixInjection = true };
#else
                var stepFunctionsConfig = new AmazonStepFunctionsConfig { ServiceURL = "http://" + Host() };
#endif

                return new AmazonStepFunctionsClient(awsCredentials, stepFunctionsConfig);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        }
    }
}
