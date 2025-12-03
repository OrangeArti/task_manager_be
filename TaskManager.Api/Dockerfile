# --- build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy csproj files and restore dependencies (cache)
COPY TaskManager.Api/*.csproj ./TaskManager.Api/
COPY TaskManager.Shared/*.csproj ./TaskManager.Shared/
RUN dotnet restore TaskManager.Api/TaskManager.Api.csproj

# copy the rest of the source code
COPY TaskManager.Api/ ./TaskManager.Api/
COPY TaskManager.Shared/ ./TaskManager.Shared/

# publish (self-contained optional)
RUN dotnet publish TaskManager.Api/TaskManager.Api.csproj -c Release -o /app

# --- runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Dev environment inside the container
ENV ASPNETCORE_ENVIRONMENT=Development
# listen for HTTP on 8080
ENV ASPNETCORE_URLS=http://+:8080

# copy published build
COPY --from=build /app ./

# start
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
