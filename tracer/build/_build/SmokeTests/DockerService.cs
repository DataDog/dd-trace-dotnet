using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Nuke.Common.IO;
using Serilog;
using Logger = Serilog.Log;
using static SmokeTests.Helpers;

namespace SmokeTests;

public class DockerService
{
    public static async Task BuildImageFromDockerfileAsync(
        AbsolutePath contextDir,
        string dockerfilePath,
        string tag,
        Dictionary<string, string> buildArgs,
        AbsolutePath artifactsDir,
        string target = null)
    {
        // Build the context tar once — MemoryStream is re-seekable for retries
        using var contextStream = CreateBuildContextTar(contextDir, dockerfilePath, artifactsDir);

        var buildParams = new ImageBuildParameters
        {
            Dockerfile = dockerfilePath,
            Tags = new List<string> {tag},
            BuildArgs = buildArgs,
            Target = target,
            Remove = true,
            ForceRemove = true,
        };

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // Reset the context stream for each attempt
                contextStream.Position = 0;

                // Create a fresh client for each attempt — the previous connection may be in a bad state
                using var client = CreateDockerClient();

                string lastError = null;
                var progress = new InlineProgress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Stream))
                    {
                        var line = msg.Stream.TrimEnd('\n', '\r');
                        if (!string.IsNullOrEmpty(line))
                        {
                            Log.Debug("{DockerBuild}", line);
                        }
                    }

                    if (!string.IsNullOrEmpty(msg.Status))
                    {
                        var progressMsg = string.IsNullOrEmpty(msg.ProgressMessage) ? "" : " " + msg.ProgressMessage;
                        if (string.IsNullOrEmpty(msg.ID))
                            Log.Debug("{Status}{Progress}", msg.Status, progressMsg);
                        else
                            Log.Debug("[{Id}] {Status}{Progress}", msg.ID, msg.Status, progressMsg);
                    }

                    if (!string.IsNullOrEmpty(msg.ErrorMessage))
                    {
                        lastError = msg.ErrorMessage;
                        Log.Error("Docker build error: {Error}", msg.ErrorMessage);
                    }
                });

                Log.Information("Building image {Tag} using Docker API (attempt {Attempt}/{MaxRetries})...", tag, attempt, MaxRetries);

                await client.Images.BuildImageFromDockerfileAsync(
                    buildParams,
                    contextStream,
                    authConfigs: null,
                    headers: null,
                    progress: progress);

                if (lastError is not null)
                {
                    throw new InvalidOperationException($"Docker build failed: {lastError}");
                }

                Log.Information("Successfully built image {Tag}", tag);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = RetryDelays[attempt - 1];
                Log.Warning(ex, "Docker build attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s...", attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
    }

    public static async Task PullImageAsync(string image)
    {
        var parts = image.Split(':');
        var repo = parts[0];
        var tag = parts.Length > 1 ? parts[1] : "latest";

        await RetryAsync(
            $"Pull image {image}",
            async () =>
            {
                Logger.Information("Pulling image {Image}...", image);

                // Create a fresh client for each attempt — the previous connection may be in a bad state
                using var client = CreateDockerClient();

                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = repo, Tag = tag },
                    authConfig: null,
                    progress: new InlineProgress<JSONMessage>(msg =>
                    {
                        if (!string.IsNullOrEmpty(msg.Status))
                        {
                            var progressMsg = string.IsNullOrEmpty(msg.ProgressMessage) ? "" : " " + msg.ProgressMessage;
                            if (string.IsNullOrEmpty(msg.ID))
                                Logger.Debug("[Pull] {Status}{Progress}", msg.Status, progressMsg);
                            else
                                Logger.Debug("[Pull] [{Id}] {Status}{Progress}", msg.ID, msg.Status, progressMsg);
                        }
                    }));
                Logger.Information("Pulled image {Image}", image);
            },
            RetryDelays);
    }

    public static async Task CreateNetwork(string networkName)
    {
        await RetryAsync(
            "Create Docker network",
            async () =>
            {
                using var client = CreateDockerClient();
                Logger.Information("Creating Docker network {Network}...", networkName);
                await client.Networks.CreateNetworkAsync(new NetworksCreateParameters {Name = networkName});
            },
            RetryDelays);
    }

    /// <summary>
    /// Detects whether we're running inside a Docker container. If so, joins the smoke-test
    /// network (so we can reach containers by DNS alias) and inspects our own mounts to build
    /// a path translator for bind mounts. On the host, returns an identity translator.
    /// Must be called before creating any containers that need bind mounts.
    /// </summary>
    public static async Task<DockerEnvironment> DetectEnvironmentAsync(string networkName)
    {
        return await RetryAsync(
            $"Detect environment",
            async () =>
            {
                try
                {
                    using var client = CreateDockerClient();
                    var buildContainerId = Environment.MachineName;
                    await client.Networks.ConnectNetworkAsync(
                        networkName,
                        new NetworkConnectParameters {Container = buildContainerId});
                    Logger.Information("Joined network {Network} as {Id} — running in container", networkName, buildContainerId);

                    // Inspect our own container to discover mount mappings (container path → host path)
                    var inspection = await client.Containers.InspectContainerAsync(buildContainerId);
                    var mounts = inspection.Mounts
                        .Where(m => !string.IsNullOrEmpty(m.Destination) && !string.IsNullOrEmpty(m.Source))
                        .OrderByDescending(m => m.Destination.Length) // longest prefix first
                        .ToList();

                    foreach (var m in mounts)
                    {
                        Logger.Debug("Mount: {Source} -> {Destination} (Type: {Type})", m.Source, m.Destination, m.Type);
                    }

                    return new DockerEnvironment(buildContainerId, ToHostPath);

                    string ToHostPath(string containerPath)
                    {
                        foreach (var mount in mounts)
                        {
                            var dest = mount.Destination.TrimEnd('/');
                            if (containerPath == dest || containerPath.StartsWith(dest + "/", StringComparison.Ordinal))
                            {
                                var relativePart = containerPath.Substring(dest.Length);
                                var hostPath = mount.Source.TrimEnd('/') + relativePart;
                                Logger.Debug("Translated path {Container} -> {Host}", containerPath, hostPath);
                                return hostPath;
                            }
                        }

                        Logger.Warning("No mount found for path {Path}, using as-is", containerPath);
                        return containerPath;
                    }
                }
                catch (DockerNetworkNotFoundException ex)
                {
                    // ConnectNetworkAsync should catch a 404 and throw a DockerNetworkNotFoundException when it's not running in a container
                    Logger.Debug(ex, "Could not join network {Network} (This is expected when running on the host)", networkName);
                    return new DockerEnvironment(null, path => path);
                }
            },
            RetryDelays);
    }

    /// <summary>
    /// Creates and starts a container with retry logic. On failure, any partially-created
    /// container is cleaned up before the next attempt.
    /// </summary>
    public static async Task<string> CreateAndStartContainerWithRetryAsync(
        string description,
        CreateContainerParameters createParams,
        CancellationToken ct = default)
    {
        string containerId = null;
        return await RetryAsync(
            $"Create/start {description}",
            async () =>
            {
                using  var client = CreateDockerClient();

                // Clean up partially-created container from a previous attempt
                if (containerId is not null)
                {
                    await CleanupContainerAsync(client, containerId);
                    containerId = null;
                }

                Logger.Information("Creating {Description} container...", description);
                var response = await client.Containers.CreateContainerAsync(createParams, ct);
                containerId = response.ID;

                Logger.Information("Starting {Description} container {Id}...", description, containerId[..12]);
                await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);

                return containerId;
            },
            RetryDelays,
            ct);
    }

    /// <summary>
    /// Returns the base URL to reach a container's HTTP API.
    /// In a container we use DNS; on the host we use the mapped port.
    /// </summary>
    public static async Task<string> GetContainerUrlAsync(
        DockerEnvironment environment, string containerId, string containerAlias, int containerPort, CancellationToken ct = default)
    {
        if (environment.BuildContainerId is not null)
        {
            return  $"http://{containerAlias}:{containerPort}";
        }

        return await RetryAsync(
            $"Get container Url for {containerAlias}:{containerPort} ({containerId})",
            async () =>
            {
                using var client = CreateDockerClient();
                var inspection = await client.Containers.InspectContainerAsync(containerId, ct);
                var hostPort = inspection.NetworkSettings.Ports[$"{containerPort}/tcp"][0].HostPort;
                Logger.Information("{Alias} listening on host port {Port}", containerAlias, hostPort);
                return $"http://localhost:{hostPort}";
            },
            RetryDelays,
            ct);
    }

    /// <summary>
    /// Waits for the given <paramref name="containerId" /> to exit, and returns the status code
    /// </summary>
    /// <param name="description"></param>
    /// <param name="containerId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<long> WaitForContainerAsync(string description, string containerId, CancellationToken ct = default)
    {
        Logger.Information("Waiting for {Container} container to exit...", description);
        using var client = CreateDockerClient();

        using var appTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        appTimeoutCts.CancelAfter(TimeSpan.FromMinutes(15));

        var waitResponse = await client.Containers.WaitContainerAsync(containerId, appTimeoutCts.Token);
        Logger.Information("{Container} container exited with code {Code}", description, waitResponse.StatusCode);

        return waitResponse.StatusCode;
    }

    public static async Task<string> ReadContainerLogsAsync(string containerId, CancellationToken ct = default)
    {
        using var client = CreateDockerClient();
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true },
            ct);

        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        await logStream.CopyOutputToAsync(Stream.Null, stdout, stderr, ct);

        stdout.Position = 0;
        stderr.Position = 0;

        var sb = new StringBuilder();
        sb.Append(new StreamReader(stdout).ReadToEnd());
        sb.Append(new StreamReader(stderr).ReadToEnd());
        return sb.ToString();
    }

    public static async Task Cleanup(DockerEnvironment environment, string networkName, params string[] containerIds)
    {
        using var client =  CreateDockerClient();
        foreach (var containerId in containerIds)
        {
            await CleanupContainerAsync(client, containerId);

        }

        await DisconnectFromNetworkAsync(client, environment, networkName);
        await CleanupNetworkAsync(client, networkName);

        static async Task DisconnectFromNetworkAsync(DockerClient client, DockerEnvironment environment, string networkName)
        {
            if (environment?.BuildContainerId is not { } containerId)
            {
                return;
            }

            try
            {
                await client.Networks.DisconnectNetworkAsync(
                    networkName,
                    new NetworkDisconnectParameters {Container = containerId});
                Logger.Debug("Disconnected {Id} from network {Network}", containerId, networkName);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to disconnect {Id} from network {Network}", containerId, networkName);
            }
        }

        static async Task CleanupNetworkAsync(DockerClient client, string networkName)
        {
            try
            {
                await client.Networks.DeleteNetworkAsync(networkName);
                Logger.Debug("Removed network {Network}", networkName);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to remove network {Network}", networkName);
            }
        }
    }

    static async Task CleanupContainerAsync(DockerClient client, string containerId)
    {
        if (containerId is null)
        {
            return;
        }

        try
        {
            await client.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters {Force = true, RemoveVolumes = true});
            Logger.Debug("Removed container {Id}", containerId[..12]);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to remove container {Id}", containerId[..12]);
        }
    }

    /// <summary>
    /// Creates a tar archive containing only the files needed for the Docker build context.
    /// This avoids sending the entire tracer directory (which would be huge).
    /// The Dockerfile COPYs the test app directory into the builder stage, including an
    /// "artifacts" subdirectory containing installer packages (.deb/.rpm). In CI, these
    /// are downloaded separately, so we inject them into the tar at the expected path.
    /// </summary>
    static MemoryStream CreateBuildContextTar(AbsolutePath contextDir, string dockerfilePath, AbsolutePath artifactsDir)
    {
        var memoryStream = new MemoryStream();
        using (var tarWriter = new TarWriter(memoryStream, leaveOpen: true))
        {
            // Add the Dockerfile
            var fullDockerfilePath = contextDir / dockerfilePath;
            tarWriter.WriteEntry(fullDockerfilePath, dockerfilePath.Replace('\\', '/'));

            // Add the test application directory (referenced by COPY in the Dockerfile)
            var testAppRelPath = "test/test-applications/regression/AspNetCoreSmokeTest";
            var testAppDir = contextDir / testAppRelPath;
            if (Directory.Exists(testAppDir))
            {
                AddDirectoryToTar(tarWriter, testAppDir, testAppRelPath);
            }

            // Inject installer artifacts into the test app's artifacts/ subdirectory.
            // The Dockerfile does: COPY --from=builder /src/artifacts /app/install
            // Since the test app is COPYd to /src, the artifacts must be at /src/artifacts,
            // i.e. inside the test app directory as "artifacts/".
            if (artifactsDir is not null && Directory.Exists(artifactsDir))
            {
                var artifactsTarPath = $"{testAppRelPath}/artifacts";
                var artifactFiles = Directory.GetFiles(artifactsDir, "*", SearchOption.AllDirectories);
                Log.Information("Injecting {Count} artifact files from {ArtifactsDir} into tar at {TarPath}", artifactFiles.Length, artifactsDir, artifactsTarPath);
                foreach (var f in artifactFiles)
                {
                    Log.Information("  Artifact: {File}", Path.GetRelativePath(artifactsDir, f));
                }

                AddDirectoryToTar(tarWriter, artifactsDir, artifactsTarPath);
            }
            else if (artifactsDir is not null)
            {
                Log.Warning("Artifacts directory {ArtifactsDir} does not exist — image build will likely fail at COPY --from=builder /src/artifacts", artifactsDir);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;

        static void AddDirectoryToTar(TarWriter tarWriter, string sourceDir, string tarBasePath)
        {
            foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                // Compute relative path within tar, always using forward slashes
                var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                var entryName = $"{tarBasePath}/{relativePath}";
                tarWriter.WriteEntry(filePath, entryName);
            }
        }
    }

    static DockerClient CreateDockerClient() => new DockerClientConfiguration().CreateClient();

    /// <summary>
    /// <see cref="IProgress{T}"/> implementation that invokes the handler synchronously.
    /// Unlike <see cref="Progress{T}"/>, which posts callbacks to the thread pool,
    /// this ensures the handler runs inline during <c>Report()</c> so that any
    /// side effects (e.g. setting <c>lastError</c>) are visible immediately after
    /// the awaited API call returns.
    /// </summary>
    sealed class InlineProgress<T> : IProgress<T>
    {
        readonly Action<T> Handler;

        public InlineProgress(Action<T> handler) => Handler = handler;
        public void Report(T value) => Handler(value);
    }


    /// <summary>
    /// Captures whether we're running inside a container (CI) or on the host (local dev).
    /// When in a container, <see cref="BuildContainerId"/> is set and <see cref="ToHostPath"/>
    /// translates container-local paths to Docker-host paths for use in bind mounts.
    /// </summary>
    public record DockerEnvironment(string BuildContainerId, Func<string, string> ToHostPath);
}
