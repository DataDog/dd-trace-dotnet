using System;

namespace Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAD.Asm1
{
    internal class DummyWorker
    {
        private const string AdDataKeyName = "Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefaultAD.Assembly1";
        private const string AdDataValue = "Dummy Work Performed";

        public void PerformDummyWork()
        {
            Type thisType = this.GetType();
            ConsoleWrite.Line($"Invoked code in \"{thisType.FullName}\" from assembly \"{thisType.Assembly.FullName}\" located at \"{thisType.Assembly.Location}\".");

            AppDomain.CurrentDomain.SetData(AdDataKeyName, AdDataValue);
        }
    }
}
