namespace GeneratePackageVersions
{
    public class PackageVersionEntry
    {
        public string IntegrationName { get; set; }

        public string SampleProjectName { get; set; }

        public string NugetPackageSearchName { get; set; }

        public string MinVersion { get; set; }

        public string MaxVersionExclusive { get; set; }

        public string SampleTargetFramework { get; set; }
    }
}
