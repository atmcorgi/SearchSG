#!/bin/bash

# Script to build and run SearchSG Test App
# Usage: ./build-and-run.sh

echo "==================================================================="
echo "  SearchSG Test App - Build and Run"
echo "==================================================================="
echo ""

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK is not installed"
    echo "Install: brew install --cask dotnet-sdk"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "[INFO] .NET SDK version: $DOTNET_VERSION"
echo ""

# Check if ports are already in use
if lsof -ti:65109 > /dev/null 2>&1 || lsof -ti:65110 > /dev/null 2>&1; then
    echo "WARNING: Port 65109 or 65110 is already in use"
    echo "Stopping existing processes..."
    
    if [ -f "./stop.sh" ]; then
        ./stop.sh
        sleep 2
    else
        lsof -ti:65109 | xargs kill -9 2>/dev/null
        lsof -ti:65110 | xargs kill -9 2>/dev/null
        sleep 2
    fi
fi

# Check configuration file
if [ ! -f "appconfigs.json" ]; then
    echo "WARNING: Configuration file appconfigs.json not found"
    echo "The application may not work correctly without proper configuration"
    echo ""
fi

# Clean previous build
echo "[INFO] Cleaning previous build..."
dotnet clean --no-restore > /dev/null 2>&1
echo ""

# Restore dependencies
echo "[INFO] Restoring dependencies..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to restore dependencies"
    exit 1
fi
echo ""

# Build project
echo "[INFO] Building project..."
dotnet build --no-restore
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo ""

# Check build result
if [ ! -f "bin/Debug/net9.0/SearchSGTestApp.dll" ]; then
    echo "ERROR: Build output not found"
    exit 1
fi

echo "==================================================================="
echo "  Build completed successfully"
echo "==================================================================="
echo ""

# Run application
echo "==================================================================="
echo "  Starting application..."
echo "  Application will be available at:"
echo "  HTTP:  http://localhost:65110"
echo "  HTTPS: https://localhost:65109"
echo "==================================================================="
echo ""
echo "Press Ctrl + C to stop the application"
echo ""

# Run in foreground
dotnet run --no-build

