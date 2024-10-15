#if NETFRAMEWORK

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace Samples.AWS.StepFunctions
{
    static class SyncHelpers
    {
        private const string StepFunctionName = "StepFunction";
        private const string Input = "{\"key1\":\"value1\",\"key2\":\"value2\"}";


        public static async Task StartStepFunctionsTasks(AmazonStepFunctionsClient stepFunctionsClient)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                var stepFunctionArn = await CreateStateMachineSync(stepFunctionsClient, StepFunctionName);

                // Needed in order to allow resource to be in
                // Ready status.
                Thread.Sleep(1000);
                
                var executionRequest = new StartSyncExecutionRequest
                {
                    Input = Input,
                    StateMachineArn = stepFunctionArn
                };

                await stepFunctionsClient.StartSyncExecution(executionRequest);
                await DeleteStateMachineSync(stepFunctionsClient, stepFunctionName);

                // Needed in order to allow resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static async Task DeleteStateMachineSync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionName)
        {
            var deleteStateMachineRequest = new DeleteStateMachineRequest { Name = stepFunctionName };

            var response = await stepFunctionsClient.DeleteStateMachine(deleteStateMachineRequest);

            Console.WriteLine($"DeleteStateMachine(DeleteStateMachineRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task<string> CreateStateMachineSync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionName)
        {
            var createStateMachineRequest = new CreateStateMachineRequest { Name = stepFunctionName };

            var response = await stepFunctionsClient.CreateStateMachine(createStateMachineRequest);

            Console.WriteLine($"CreateStateMachine(CreateStateMachineRequest) HTTP status code: {response.HttpStatusCode}");

            return response.StateMachineArn;
        }
    }
}
#endif
