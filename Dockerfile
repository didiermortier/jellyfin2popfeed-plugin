# Jellyfin Plugin Builder
# Uses Jellyfin's official Docker image which has the correct .NET runtime

FROM ghcr.io/linuxserver/jellyfin:latest

# Install build tools
RUN apt-get update && apt-get install -y \
    dotnet-sdk-9.0 \
    git \
    && rm -rf /var/lib/apt/lists/*

# Copy plugin source
WORKDIR /build
COPY Jellyfin.Plugin.Jellyfin2PopFeed /build/Jellyfin.Plugin.Jellyfin2PopFeed

# Restore NuGet packages (using Jellyfin's feed)
WORKDIR /build/Jellyfin.Plugin.Jellyfin2PopFeed
RUN dotnet nuget add source https://repo.jellyfin.org/nuget/ --name jellyfin
RUN dotnet restore Jellyfin.Plugin.Jellyfin2PopFeed.csproj

# Build the plugin
RUN dotnet build Jellyfin.Plugin.Jellyfin2PopFeed.csproj --configuration Release --no-restore

# Copy the output DLL
RUN mkdir -p /output
RUN cp bin/Release/net9.0/Jellyfin.Plugin.Jellyfin2PopFeed.dll /output/

# The DLL will be at /output/Jellyfin.Plugin.Jellyfin2PopFeed.dll
