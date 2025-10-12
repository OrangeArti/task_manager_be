FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskManager.Shared/*.csproj ./TaskManager.Shared/
COPY TaskManager.ServiceStub/*.csproj ./TaskManager.ServiceStub/

RUN dotnet restore TaskManager.ServiceStub/TaskManager.ServiceStub.csproj

COPY TaskManager.Shared/ ./TaskManager.Shared/
COPY TaskManager.ServiceStub/ ./TaskManager.ServiceStub/

RUN dotnet publish TaskManager.ServiceStub/TaskManager.ServiceStub.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TaskManager.ServiceStub.dll"]
