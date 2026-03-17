# Build using the following commands:
#
# cd tracer\build\_build
# # Build our base debian image, used in CI
# docker build --build-arg DOTNETSDK_VERSION=10.0.100 --tag dd-trace-dotnet/debian-tester --file .\docker\debian.dockerfile .
# # Build the sandbox layers
# docker build --build-arg DOTNETSDK_VERSION=10.0.100 --tag dd-trace-dotnet/sandbox --file .\docker\claude-sandbox.dockerfile --no-cache-filter claude .
#
# Run a sandbox in the current directory, use:
#
# docker sandbox run -t dd-trace-dotnet/sandbox claude .

FROM dd-trace-dotnet/debian-tester AS base

# Grab stuff from the original sandbox
ENV NPM_CONFIG_PREFIX=/usr/local/share/npm-global
ENV PATH=/home/agent/.local/bin:/usr/local/share/npm-global/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
ENV NO_PROXY=localhost,127.0.0.1,::1,172.17.0.0/16
ENV no_proxy=localhost,127.0.0.1,::1,172.17.0.0/16

WORKDIR /home/agent/workspace
RUN apt-get update \
    && apt-get install -yy --no-install-recommends \
    ca-certificates \
    curl \
    gnupg \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg | \
    gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
    $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | \
    tee /etc/apt/sources.list.d/docker.list > /dev/null

# Remove base image user
# Create non-root user
# Configure sudoers
# Create sandbox config
# Set up npm global package folder under /usr/local/share
RUN userdel ubuntu || true \
    && useradd --create-home --uid 1000 --shell /bin/bash agent \
    && groupadd -f docker \
    && usermod -aG sudo agent \
    && usermod -aG docker agent \
    && mkdir /etc/sudoers.d \
    && chmod 0755 /etc/sudoers.d \
    && echo "agent ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/agent \
    && echo "Defaults:%sudo env_keep += \"http_proxy https_proxy no_proxy HTTP_PROXY HTTPS_PROXY NO_PROXY SSL_CERT_FILE NODE_EXTRA_CA_CERTS REQUESTS_CA_BUNDLE JAVA_TOOL_OPTIONS\"" > /etc/sudoers.d/proxyconfig \
    && mkdir -p /home/agent/.docker/sandbox/locks \
    && chown -R agent:agent /home/agent \
    && mkdir -p /usr/local/share/npm-global \
    && chown -R agent:agent /usr/local/share/npm-global

RUN touch /etc/sandbox-persistent.sh && chmod 644 /etc/sandbox-persistent.sh && chown agent:agent /etc/sandbox-persistent.sh
ENV BASH_ENV=/etc/sandbox-persistent.sh
RUN cat <<'PROFILEEOF' > /etc/profile.d/sandbox-persistent.sh
# Source the sandbox persistent environment file
if [ -f /etc/sandbox-persistent.sh ]; then
    . /etc/sandbox-persistent.sh
fi

# Export BASH_ENV so non-interactive child shells also source the persistent env
export BASH_ENV=/etc/sandbox-persistent.sh
PROFILEEOF

RUN chmod 644 /etc/profile.d/sandbox-persistent.sh

RUN cat <<'PREPEND' > /tmp/sandbox-bashrc-prepend
# Docker Sandbox: Source persistent environment for interactive shells
if [ -f /etc/sandbox-persistent.sh ]; then
    . /etc/sandbox-persistent.sh
fi

# Export BASH_ENV so non-interactive child shells also source the persistent env
export BASH_ENV=/etc/sandbox-persistent.sh

PREPEND

RUN cat /tmp/sandbox-bashrc-prepend /etc/bash.bashrc > /tmp/new-bashrc \
   && mv /tmp/new-bashrc /etc/bash.bashrc \
   && chmod 644 /etc/bash.bashrc \
   && rm /tmp/sandbox-bashrc-prepend

RUN cat <<'BASHRCEOF' > /home/agent/.bashrc
# Source the sandbox persistent environment file
if [ -f /etc/sandbox-persistent.sh ]; then
    . /etc/sandbox-persistent.sh
fi

# Export BASH_ENV for child non-interactive shells
export BASH_ENV=/etc/sandbox-persistent.sh
BASHRCEOF

RUN chmod 644 /home/agent/.bashrc \
    && chown agent:agent /home/agent/.bashrc

USER root

# Setup Github keys
RUN curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
    | tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null \
    && chmod a+r /etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
    | tee /etc/apt/sources.list.d/github-cli.list > /dev/null

RUN apt-get update \
    && apt-get install -yy --no-install-recommends \
    dnsutils \
    docker-buildx-plugin \
    docker-ce-cli \
    docker-compose-plugin \
    git \
    jq \
    less \
    lsof \
    make \
    procps \
    psmisc \
    ripgrep \
    rsync \
    socat \
    sudo \
    unzip \
    gh \
    bc \
    default-jdk-headless \
    golang \
    man-db \
    nodejs \
    npm \
    python3 \
    python3-pip \
    containerd.io docker-ce \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

LABEL com.docker.sandboxes.start-docker=true

USER agent

FROM base AS claude

# Install Claude Code
RUN curl -fsSL https://claude.ai/install.sh | bash

ENV CLAUDE_ENV_FILE=/etc/sandbox-persistent.sh
CMD ["claude", "--dangerously-skip-permissions"]

