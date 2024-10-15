using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace Samples.AWS.StepFunctions
{
    static class AsyncHelpers
    {
        private const string StepFunctionName = "StepFunction";
        private const string Input = "{\"key1\":\"value1\",\"key2\":\"value2\"}";


        public static async Task StartStepFunctionsTasks(AmazonStepFunctionsClient stepFunctionsClient)
        {
            Console.WriteLine("Beginning Async methods");
            using (var scope = SampleHelpers.CreateScope("async-methods"))
            {
                var stepFunctionArn = await CreateStateMachineAsync(stepFunctionsClient, StepFunctionName);

                // Needed in order to allow resource to be in
                // Ready status.
                Thread.Sleep(1000);
                
                var executionRequest = new StartExecutionRequest
                {
                    Input = Input,
                    StateMachineArn = stepFunctionArn
                };

                await stepFunctionsClient.StartExecutionAsync(executionRequest);
                await DeleteStateMachineAsync(stepFunctionsClient, stepFunctionArn);

                // Needed in order to allow resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static async Task DeleteStateMachineAsync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionArn)
        {
            var deleteStateMachineRequest = new DeleteStateMachineRequest { StateMachineArn = stepFunctionArn };

            var response = await stepFunctionsClient.DeleteStateMachineAsync(deleteStateMachineRequest);

            Console.WriteLine($"DeleteStateMachineAsync(DeleteStateMachineRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static async Task<string> CreateStateMachineAsync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionName)
        {
            var createStateMachineRequest = new CreateStateMachineRequest { Name = stepFunctionName };

            var response = await stepFunctionsClient.CreateStateMachineAsync(createStateMachineRequest);

            Console.WriteLine($"CreateStateMachineAsync(CreateStateMachineRequest) HTTP status code: {response.HttpStatusCode}");

            return response.StateMachineArn;
        }
    }
}
