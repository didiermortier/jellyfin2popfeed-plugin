#!/bin/bash

# Jellyfin2 PopFeed Plugin Builder
# This script builds the plugin using Docker to ensure correct runtime environment

echo "=========================================="
echo "Building Jellyfin2 PopFeed Plugin"
echo "=========================================="

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "ERROR: Docker is not installed. Please install Docker first."
    echo "On Ubuntu/Debian: sudo apt-get install docker.io"
    echo "On CentOS/RHEL: sudo yum install docker"
    exit 1
fi

# Build the Docker image
echo "Building Docker image..."
docker build -t jellyfin2popfeed-builder .

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to build Docker image"
    exit 1
fi

# Run the build and extract the DLL
echo "Running build..."
docker create --name temp-builder jellyfin2popfeed-builder
docker cp temp-builder:/output/Jellyfin.Plugin.Jellyfin2PopFeed.dll .
docker rm temp-builder

echo ""
echo "=========================================="
echo "Build Complete!"
echo "=========================================="
echo "DLL created: Jellyfin.Plugin.Jellyfin2PopFeed.dll"
echo ""
echo "To install in Jellyfin:"
echo "1. mkdir -p /var/lib/jellyfin/plugins/Jellyfin2PopFeed/"
echo "2. cp Jellyfin.Plugin.Jellyfin2PopFeed.dll /var/lib/jellyfin/plugins/Jellyfin2PopFeed/"
echo "3. chown jellyfin:jellyfin /var/lib/jellyfin/plugins/Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.dll"
echo "4. sudo systemctl restart jellyfin"
echo ""
