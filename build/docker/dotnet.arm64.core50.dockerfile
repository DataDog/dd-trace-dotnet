FROM mcr.microsoft.com/dotnet/sdk:5.0.102-ca-patch-buster-slim

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it