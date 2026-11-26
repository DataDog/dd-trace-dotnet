# FROM mcr.microsoft.com/dotnet/sdk:8.0

# ARG CI_JOB_TOKEN
# ENV PYTHONDONTWRITEBYTECODE 1
# ENV PYTHON_VERSION 3.9.15
# ENV PYENV_ROOT "/pyenv"
# ENV PATH "$PYENV_ROOT/shims:$PYENV_ROOT/bin:/app:/app/bp-runner/:/app/benchmark-github-tools/:/app/benchmark-analyzer/:$PATH"

# # Needed for pyenv to enable using source in the RUN install-base-deps.sh command
# SHELL ["bash", "-c"]

# COPY ./install-base-deps.sh ./install-base-deps.sh
# RUN --mount=type=ssh source ./install-base-deps.sh

# # Install lightweight Java for Kafka
# RUN apt-get update && apt-get install -y --no-install-recommends \
#     openjdk-17-jre-headless && rm -rf /var/lib/apt/lists/*

# # Use bp-install to install Benchmarking Platform utilities
# # @see https://datadoghq.atlassian.net/wiki/spaces/APMINT/pages/3652092200/How+to+install+packages+with+bp-install
# RUN --mount=type=ssh bp-install benchmark-analyzer github-tools datadog-agent k6

FROM apache/kafka:3.9.2 AS kafka-stage

FROM registry.ddbuild.io/images/benchmarking-platform-tools-ubuntu:latest

ARG CI_JOB_TOKEN

SHELL ["/bin/bash", "-c"]

COPY ./install-base-deps.sh ./install-base-deps.sh
RUN ./install-base-deps.sh

COPY ./install-datadog-agent.sh ./install-datadog-agent.sh
RUN ./install-datadog-agent.sh

COPY --from=kafka-stage /opt/kafka /usr/local/kafka

# Download and install the tracer
ENV TRACER_VERSION=3.15.0
RUN if [ "$(uname -m)" = "arm64" ] || [ "$(uname -m)" = "aarch64" ]; then \
        ARCH="arm64"; \
    else \
        ARCH="amd64"; \
    fi && \
    echo "Installing Datadog .NET tracer for architecture: $ARCH" && \
    wget https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_${ARCH}.deb && \
    dpkg -i ./datadog-dotnet-apm_${TRACER_VERSION}_${ARCH}.deb && \
    /opt/datadog/createLogPath.sh