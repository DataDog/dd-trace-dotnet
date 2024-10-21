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

        public static void StartStepFunctionsTasks(AmazonStepFunctionsClient stepFunctionsClient)
        {
            Console.WriteLine("Beginning Sync methods");
            using (var scope = SampleHelpers.CreateScope("sync-methods"))
            {
                var stepFunctionArn = CreateStateMachineSync(stepFunctionsClient, StepFunctionName);

                // Needed in order to allow resource to be in
                // Ready status.
                Thread.Sleep(1000);

                var executionRequest = new StartExecutionRequest
                {
                    Input = Input,
                    StateMachineArn = stepFunctionArn
                };

                var response = stepFunctionsClient.StartExecution(executionRequest);
                Console.WriteLine($"StartSyncExecution(StartSyncExecution) HTTP status code: {response.HttpStatusCode}");

                DeleteStateMachineSync(stepFunctionsClient, stepFunctionArn);

                // Needed in order to allow resource to be deleted
                Thread.Sleep(1000);
            }
        }

        private static void DeleteStateMachineSync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionArn)
        {
            var deleteStateMachineRequest = new DeleteStateMachineRequest { StateMachineArn = stepFunctionArn };

            var response = stepFunctionsClient.DeleteStateMachine(deleteStateMachineRequest);

            Console.WriteLine($"DeleteStateMachine(DeleteStateMachineRequest) HTTP status code: {response.HttpStatusCode}");
        }

        private static string CreateStateMachineSync(AmazonStepFunctionsClient stepFunctionsClient, string stepFunctionName)
        {
            var def = "{\"Comment\":\"Hello.\",\"StartAt\":\"FirstState\",\"States\":{\"FirstState\":{\"Type\":\"Task\",\"Resource\":\"arn:aws:lambda:us-east-1:123456789012:function:FUNCTION_NAME\",\"Next\":\"ChoiceState\"},\"ChoiceState\":{\"Type\":\"Choice\",\"Choices\":[{\"Variable\":\"$.foo\",\"NumericEquals\":1,\"Next\":\"FirstMatchState\"},{\"Variable\":\"$.foo\",\"NumericEquals\":2,\"Next\":\"SecondMatchState\"}],\"Default\":\"DefaultState\"},\"FirstMatchState\":{\"Type\":\"Task\",\"Resource\":\"arn:aws:lambda:us-east-1:123456789012:function:OnFirstMatch\",\"Next\":\"NextState\"},\"SecondMatchState\":{\"Type\":\"Task\",\"Resource\":\"arn:aws:lambda:us-east-1:123456789012:function:OnSecondMatch\",\"Next\":\"NextState\"},\"DefaultState\":{\"Type\":\"Fail\",\"Error\":\"DefaultStateError\",\"Cause\":\"No Matches!\"},\"NextState\":{\"Type\":\"Task\",\"Resource\":\"arn:aws:lambda:us-east-1:123456789012:function:FUNCTION_NAME\",\"End\":true}}}";
            var createStateMachineRequest = new CreateStateMachineRequest { Name = stepFunctionName, Definition = def };

            var response = stepFunctionsClient.CreateStateMachine(createStateMachineRequest);

            Console.WriteLine($"CreateStateMachine(CreateStateMachineRequest) HTTP status code: {response.HttpStatusCode}");

            return response.StateMachineArn;
        }
    }
}
#endif
