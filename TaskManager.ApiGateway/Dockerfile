FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskManager.Shared/*.csproj ./TaskManager.Shared/
COPY TaskManager.ApiGateway/*.csproj ./TaskManager.ApiGateway/

RUN dotnet restore TaskManager.ApiGateway/TaskManager.ApiGateway.csproj

COPY TaskManager.Shared/ ./TaskManager.Shared/
COPY TaskManager.ApiGateway/ ./TaskManager.ApiGateway/

RUN dotnet publish TaskManager.ApiGateway/TaskManager.ApiGateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TaskManager.ApiGateway.dll"]
