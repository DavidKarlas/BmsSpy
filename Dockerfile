# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app
# The diagnostics IPC socket would need a writable /tmp; disabling it lets the container run
# with a read-only root filesystem.
ENV DOTNET_EnableDiagnostics=0
# Prometheus metrics and health endpoint
EXPOSE 8080


# This stage is used to build the service project. It runs on the build machine's own platform:
# the output is architecture-neutral IL, so multi-arch images need no emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BmsSpy.csproj", "."]
RUN dotnet restore "./BmsSpy.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./BmsSpy.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BmsSpy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BmsSpy.dll"]
