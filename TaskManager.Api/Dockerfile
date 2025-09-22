# --- build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# копируем csproj и восстанавливаем зависимости (кэш)
COPY TaskManager.Api/*.csproj ./TaskManager.Api/
RUN dotnet restore TaskManager.Api/TaskManager.Api.csproj

# копируем остальной исходный код
COPY TaskManager.Api/ ./TaskManager.Api/

# публикуем в self-contained зависимости (необязательно)
RUN dotnet publish TaskManager.Api/TaskManager.Api.csproj -c Release -o /app

# --- runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# среда Dev внутри контейнера
ENV ASPNETCORE_ENVIRONMENT=Development
# слушаем HTTP на 8080
ENV ASPNETCORE_URLS=http://+:8080

# копируем опубликованный билд
COPY --from=build /app ./

# старт
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]