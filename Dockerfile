# syntax=docker/dockerfile:1

ARG PROJECT
ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
ARG BUILD_CONFIGURATION
WORKDIR /src

# Copy solution file first for better caching
COPY *.sln ./

# Copy all project files - .dockerignore will exclude unnecessary files
# This layer will be cached unless .csproj files change
COPY src/ ./src/

# Restore dependencies - Docker layer caching will cache this step when project files don't change
# Using standard NuGet cache location to avoid conflicts during parallel builds
RUN dotnet restore "$PROJECT" --verbosity quiet

# Build and publish - using --no-restore since we already restored above
RUN dotnet publish "$PROJECT" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG APP_DLL
WORKDIR /app

# Copy only published files
COPY --from=build /app/publish .

# Services listen on port 8080 inside the container by default
ENV ASPNETCORE_URLS=http://+:8080

ENV APP_DLL=${APP_DLL}

# Use bash to resolve the dll name at runtime
ENTRYPOINT ["bash", "-c", "dotnet /app/${APP_DLL}"]
