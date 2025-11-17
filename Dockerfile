# syntax=docker/dockerfile:1

ARG PROJECT
ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
ARG BUILD_CONFIGURATION
WORKDIR /src

# Copy the entire repo so project references resolve correctly
COPY . .

# Restore and publish the requested project
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG APP_DLL
WORKDIR /app

COPY --from=build /app/publish .

# Services listen on port 8080 inside the container by default
ENV ASPNETCORE_URLS=http://+:8080

ENV APP_DLL=${APP_DLL}

# Use bash to resolve the dll name at runtime
ENTRYPOINT ["bash", "-c", "dotnet /app/${APP_DLL}"]
