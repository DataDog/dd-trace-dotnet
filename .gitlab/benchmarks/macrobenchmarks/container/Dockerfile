FROM mcr.microsoft.com/dotnet/sdk:9.0

ARG CI_JOB_TOKEN
ENV PYTHONDONTWRITEBYTECODE 1
ENV PYTHON_VERSION 3.9.15
ENV PYENV_ROOT "/pyenv"
ENV PATH "$PYENV_ROOT/shims:$PYENV_ROOT/bin:/app:/app/bp-runner/:/app/benchmark-github-tools/:/app/benchmark-analyzer/:$PATH"

# Needed for pyenv to enable using source in the RUN install-base-deps.sh command
SHELL ["bash", "-c"]

# Install .NET SDK
RUN curl -SL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --version 11.0.100 --install-dir /usr/share/dotnet

# confirm install
RUN dotnet --list-sdks

COPY ./install-base-deps.sh ./install-base-deps.sh
RUN --mount=type=ssh source ./install-base-deps.sh
# Use bp-install to install Benchmarking Platform utilities
# @see https://datadoghq.atlassian.net/wiki/spaces/APMINT/pages/3652092200/How+to+install+packages+with+bp-install
RUN --mount=type=ssh bp-install benchmark-analyzer github-tools datadog-agent k6
