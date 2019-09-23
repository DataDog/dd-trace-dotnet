FROM mcr.microsoft.com/dotnet/core/sdk:2.1

# Instructions to install .NET Core 3.0 SDK from
# https://dotnet.microsoft.com/download/linux-package-manager/debian9/sdk-current
RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.asc.gpg && \
    sudo mv microsoft.asc.gpg /etc/apt/trusted.gpg.d/ && \
    wget -q https://packages.microsoft.com/config/debian/9/prod.list && \
    sudo mv prod.list /etc/apt/sources.list.d/microsoft-prod.list && \
    sudo chown root:root /etc/apt/trusted.gpg.d/microsoft.asc.gpg && \
    sudo chown root:root /etc/apt/sources.list.d/microsoft-prod.list

RUN sudo apt-get install -y apt-transport-https && \
    sudo apt-get update && \
    sudo apt-get install -y dotnet-sdk-3.0

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it
