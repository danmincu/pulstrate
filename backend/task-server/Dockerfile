FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files
COPY ["TaskServer.sln", "./"]
COPY ["src/TaskServer.Core/TaskServer.Core.csproj", "src/TaskServer.Core/"]
COPY ["src/TaskServer.Infrastructure/TaskServer.Infrastructure.csproj", "src/TaskServer.Infrastructure/"]
COPY ["src/TaskServer.Api/TaskServer.Api.csproj", "src/TaskServer.Api/"]

# Restore dependencies
RUN dotnet restore "TaskServer.sln"

# Copy source code
COPY . .

# Build
WORKDIR "/src"
RUN dotnet build "TaskServer.sln" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/TaskServer.Api/TaskServer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskServer.Api.dll"]
