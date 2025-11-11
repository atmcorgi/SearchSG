#!/bin/bash

# Script to start SearchSG Test App in foreground
# Usage: ./start.sh
# To stop: Press Ctrl + C in this terminal

echo "Starting SearchSG Test App..."
echo ""

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK is not installed"
    echo "Install: brew install --cask dotnet-sdk"
    exit 1
fi

# Check if ports are already in use
if lsof -ti:65109 > /dev/null 2>&1 || lsof -ti:65110 > /dev/null 2>&1; then
    echo "WARNING: Port 65109 or 65110 is already in use"
    echo "Stopping existing processes..."
    ./stop.sh
    sleep 2
fi

# Check configuration file
if [ ! -f "appconfigs.json" ]; then
    echo "WARNING: Configuration file appconfigs.json not found"
    echo "The application may not work correctly without proper configuration"
    echo ""
fi

echo "==================================================================="
echo "Application will be available at:"
echo "  HTTP:  http://localhost:65110"
echo "  HTTPS: https://localhost:65109"
echo "==================================================================="
echo ""
echo "Press Ctrl + C to stop the application"
echo ""

# Run application in foreground
dotnet run

