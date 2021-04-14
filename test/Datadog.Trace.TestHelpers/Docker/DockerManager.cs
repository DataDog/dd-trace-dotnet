using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
// using Docker.DotNet;
// using Docker.DotNet.Models;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers.Docker
{
    public class DockerManager : IDisposable
    {
        // private readonly DockerClient _dockerClient;

        private readonly string _dockerFile;
        private readonly string _msiDirectory;
        private readonly string _imageTag;
        private readonly string _imageName;
        private readonly string _tracePipe;
        private readonly string _solutionDir;
        private readonly int _containerPort;

        private readonly EnvironmentHelper _environmentHelper;
        private readonly ITestOutputHelper _output;

        // private string _containerId;

        public DockerManager(
            EnvironmentHelper environmentHelper,
            int containerPort)
        {
            _environmentHelper = environmentHelper;
            _solutionDir = EnvironmentHelper.GetSolutionDirectory();
            _dockerFile = "DockerfileIIS";
            _msiDirectory = @"./src/WindowsInstaller/bin/Release/x64/en-us";
            _imageTag = _imageName = $"sample-{_environmentHelper.TestId.ToString().Replace("-", string.Empty)}";
            _containerPort = containerPort;
            _tracePipe = _environmentHelper.TracePipeName;
            _output = environmentHelper.OutputHelper;
            // _dockerClient = new DockerClientConfiguration(new Uri(DockerApiUri())).CreateClient();
        }

        public void Dispose()
        {
            // if (_containerId != null)
            // {
            //     _dockerClient.Containers.KillContainerAsync(_containerId, new ContainerKillParameters()).Wait(10_000);
            // }
            // else
            // {
            StopContainer();
            // }
        }

        public void BuildImage()
        {
            // docker build -f DockerfileIIS
            // --build-arg SITE_DIR=./test/test-applications/aspnet/Samples.AspNetMvc4
            // --build-arg MSI_DIR=./src/WindowsInstaller/bin/Release/x64/en-us
            // -t local-iis-test C:\Github\dd-trace-dotnet --no-cache

            var sampleProjectDir = _environmentHelper.GetSampleProjectDirectory();
            var publishDir = Path.Combine(sampleProjectDir, "bin", "app.publish");
            var dockerizedProjectDir = publishDir.Replace(_solutionDir, string.Empty).Replace(@"\", "/");
            dockerizedProjectDir = $".{publishDir}";
            var cmd = $"docker build -f {_dockerFile} --build-arg SITE_DIR={dockerizedProjectDir} --build-arg MSI_DIR={_msiDirectory} -t {_imageTag} {_solutionDir} --no-cache";
            // _output.WriteLine("Docker build cmd: {0}", cmd);
            RunCmd(cmd);
        }

        public void StartContainer()
        {
            var cmd = $"docker run --name {_imageName} -p {_containerPort}:{_containerPort} -v //./pipe/{_tracePipe}://./pipe/{_tracePipe} {_imageTag}";
            RunCmd(cmd);
            // _output.WriteLine("Docker run cmd: {0}", cmd);

            // try
            // {
            //     var queryTask = _dockerClient.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
            //     queryTask.Wait(10_000);
            //     var containers = queryTask.Result;
            //     foreach (var container in containers)
            //     {
            //         if (container.Names.Any(n => n.Equals(_imageName, StringComparison.OrdinalIgnoreCase)))
            //         {
            //             _containerId = container.ID;
            //             break;
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     _output.WriteLine("Unable to verify container start: {0}", ex);
            // }
        }

        public void StopContainer()
        {
            // docker stop local-iis-test; docker rm local-iis-test
            var cmd = $"docker stop {_imageName}; docker rm {_imageName}";
            RunCmd(cmd);
            // _output.WriteLine("Docker stop cmd: {0}", cmd);
        }

        private void RunCmd(string cmd)
        {
            try
            {
                var process = new System.Diagnostics.Process();
                var startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/C {cmd}";
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                _output.WriteLine("Unable to run cmd {0}: {1}", cmd, ex);
            }
        }

        private string DockerApiUri()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
            {
                return "npipe://./pipe/docker_engine";
            }

            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (isLinux)
            {
                return "unix:/var/run/docker.sock";
            }

            throw new Exception("Was unable to determine what OS this is running on, does not appear to be Windows or Linux!?");
        }

        // private async Task PullImage()
        // {
        //     await _dockerClient.Images
        //         .CreateImageAsync(new ImagesCreateParameters
        //         {
        //             FromImage = ContainerImageUri,
        //             Tag = "latest"
        //         },
        //             new AuthConfig(),
        //             new Progress<JSONMessage>());
        // }

        // private async Task StartContainer()
        // {
        //     var response = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        //     {
        //         Image = ContainerImageUri,
        //         ExposedPorts = new Dictionary<string, EmptyStruct>
        //         {
        //             {
        //                 "8000", default(EmptyStruct)
        //             }
        //         },
        //         HostConfig = new HostConfig
        //         {
        //             PortBindings = new Dictionary<string, IList<PortBinding>>
        //             {
        //                 {"8000", new List<PortBinding> {new PortBinding {HostPort = "8000"}}}
        //             },
        //             PublishAllPorts = true
        //         }
        //     });
        //     _containerId = response.ID;
        //     await _dockerClient.Containers.StartContainerAsync(_containerId, null);
        // }
    }
}
