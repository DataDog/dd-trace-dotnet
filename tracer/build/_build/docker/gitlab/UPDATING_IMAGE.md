# Updating the GitLab CI Windows Docker Image

This guide outlines the process for updating and deploying the Windows Docker image used in GitLab CI.

## Prerequisites

- Windows PC/Virtual Machine
- Docker Desktop with Windows containers enabled
- Push access to `datadog` DockerHub organization

## Process

### 1. Update the Dockerfile

Edit `gitlab.windows.dockerfile` with required changes (version environment variables, SHA256/SHA512 hashes, download URLs).

### 2. Determine the Tag

Tag format: `dotnet<VERSION>-<STAGE>` (e.g., `dotnet10-rc2`)

- **Major .NET version updates:** Use the .NET version (e.g., `dotnet10-rc1`, `dotnet10-rc2`)
- **Minor tooling updates:** Append `.1`, `.2`, etc. (e.g., `dotnet10-rc2.1`)

### 3. Build and Push to DockerHub

```powershell
$tag="<TAG>"
echo "building datadog/dd-trace-dotnet-docker-build:$tag"
cd tracer\build\_build\docker\gitlab
docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:$tag .
docker push datadog/dd-trace-dotnet-docker-build:$tag
```

### 4. Test the Image in GitLab CI

Update `.gitlab-ci.yml` to use the DockerHub image: `datadog/dd-trace-dotnet-docker-build:<TAG>`

Create a PR and verify the GitLab CI build passes.

### 5. Get the Image Digest

This is displayed when running the `docker push` from step 3. Alternatively, you can find the hash for an image using:
```powershell
echo "Finding format for $tag"
docker pull datadog/dd-trace-dotnet-docker-build:$tag
docker inspect --format='{{index .RepoDigests 0}}' datadog/dd-trace-dotnet-docker-build:$tag
```

Extract the SHA256 hash from the output (format: `docker.io/datadog/dd-trace-dotnet-docker-build@sha256:<HASH>`).

### 6. Create Mirror PR

In the `DataDog/images` repository, add entries to two files:

**`mirror.yaml`:**
```yaml
- source: "docker.io/datadog/dd-trace-dotnet-docker-build:<TAG>"
  dest:
    repo: "datadog/dd-trace-dotnet-docker-build"
    tag: "<TAG>"
  replication_target: "build"
  platforms:
    - "windows/amd64"
```

**`mirror.lock.yaml`:**
```yaml
- source: docker.io/datadog/dd-trace-dotnet-docker-build:<TAG>
  digest: sha256:<DIGEST_FROM_STEP_5>
```

> [!TIP]
> You can clean up and delete "old" tags from these files, as long as they're no long in use in any pipelines.
Create PR and wait for merge. Mirror sync completes within a few minutes.

### 7. Update GitLab CI to Use Mirror

Update `.gitlab-ci.yml` to use the mirror URL: `registry.ddbuild.io/images/mirror/datadog/dd-trace-dotnet-docker-build:<TAG>`

Create final PR in `dd-trace-dotnet`.

## Reference

**Example PRs:**
- Image update: [dd-trace-dotnet#7492](https://github.com/DataDog/dd-trace-dotnet/pull/7492)
- Mirror: [images#7647](https://github.com/DataDog/images/pull/7647)

