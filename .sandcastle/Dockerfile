FROM node:22-bookworm

# Install system dependencies
RUN apt-get update && apt-get install -y \
  git \
  curl \
  jq \
  libicu72 \
  && rm -rf /var/lib/apt/lists/*

# Install PowerShell: the Claude Code hook in .claude/settings.json (the verify gate) runs
# `powershell`, so alias it to pwsh. Without it the hook blocks every Bash call in the sandbox.
RUN curl -fsSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb \
  && dpkg -i /tmp/packages-microsoft-prod.deb \
  && rm /tmp/packages-microsoft-prod.deb \
  && apt-get update && apt-get install -y powershell \
  && rm -rf /var/lib/apt/lists/* \
  && ln -s "$(command -v pwsh)" /usr/local/bin/powershell

# The issue tracker is local Markdown in .scratch/ read by .sandcastle/issues.mjs (Node), so no CLI is needed.

# Build-args for UID/GID alignment: sandcastle docker build-image
# defaults these to the host user's UID/GID so image-built files
# and bind-mounted files share an owner without runtime chown.
ARG AGENT_UID=1000
ARG AGENT_GID=1000

# Rename the base image's "node" user to "agent" and align UID/GID.
RUN groupmod -o -g $AGENT_GID node && usermod -o -u $AGENT_UID -g $AGENT_GID -d /home/agent -m -l agent node
USER ${AGENT_UID}:${AGENT_GID}

# Install Claude Code CLI
RUN curl -fsSL https://claude.ai/install.sh | bash

# Install the .NET SDK. The channel matches global.json, which rolls forward to the latest 10.0 feature band.
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /home/agent/.dotnet \
  && mkdir -p /home/agent/.local/bin && ln -s /home/agent/.dotnet/dotnet /home/agent/.local/bin/dotnet

# Install the Aspire CLI: building the AppHost and the functional test projects fails without it (ASPIRE009).
RUN curl -sSL https://aspire.dev/install.sh | bash \
  && ln -sf /home/agent/.aspire/bin/aspire /home/agent/.local/bin/aspire

# Add Claude and .NET to PATH
ENV DOTNET_ROOT="/home/agent/.dotnet"
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
ENV PATH="/home/agent/.local/bin:/home/agent/.dotnet:$PATH"

WORKDIR /home/agent

# In worktree sandbox mode, Sandcastle bind-mounts the git worktree at /home/agent/workspace
# and overrides the working directory to /home/agent/workspace at container start.
# Structure your Dockerfile so that /home/agent/workspace can serve as the project root.
ENTRYPOINT ["sleep", "infinity"]
