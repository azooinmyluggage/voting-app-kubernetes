FROM microsoft/dotnet:1.1-sdk-msbuild

WORKDIR /app

ADD . /app

RUN dotnet restore \
    && dotnet publish -c Release -o out 

ENV AZURE_STORAGE_ACCOUNT account
ENV AZURE_STORAGE_ACCESS_KEY key
ENV SQL_HOSTNAME sql_hostname
ENV SQL_USERNAME sql_username
ENV SQL_PASSWORD sql_password

ENTRYPOINT ["dotnet", "out/worker.dll"]
