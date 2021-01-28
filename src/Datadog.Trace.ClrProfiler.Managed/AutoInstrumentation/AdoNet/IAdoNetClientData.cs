namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal interface IAdoNetClientData
    {
        string IntegrationName { get; }

        string AssemblyName { get; }

        string SqlCommandType { get; }

        string MinimumVersion { get; }

        string MaximumVersion { get; }

        string DataReaderType { get; }

        string DataReaderTaskType { get; }
    }
}
