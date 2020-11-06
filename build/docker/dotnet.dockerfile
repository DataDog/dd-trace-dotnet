FROM mcr.microsoft.com/dotnet/sdk:5.0

# Instructions to install .NET Core runtimes from
# https://docs.microsoft.com/en-us/dotnet/core/install/linux-package-manager-debian10
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb 

RUN apt-get update && \
    apt-get install -y apt-transport-https && \
    apt-get update && \
    apt-get install -y aspnetcore-runtime-2.1 && \
    apt-get install -y aspnetcore-runtime-3.0 && \
	apt-get install -y aspnetcore-runtime-3.1

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it
