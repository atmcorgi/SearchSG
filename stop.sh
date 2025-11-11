#!/bin/bash

# Script to stop SearchSG Test App
# Usage: ./stop.sh

echo "Stopping SearchSG Test App..."
echo ""

STOPPED_COUNT=0
FAILED_COUNT=0

# Find and stop process on port 65109
PORT_65109=$(lsof -ti:65109 2>/dev/null)
if [ ! -z "$PORT_65109" ]; then
    echo "[INFO] Found process on port 65109 (PID: $PORT_65109)"
    kill $PORT_65109 2>/dev/null
    if [ $? -eq 0 ]; then
        echo "        Process stopped successfully"
        STOPPED_COUNT=$((STOPPED_COUNT + 1))
    else
        echo "        ERROR: Failed to stop process (may require admin privileges)"
        FAILED_COUNT=$((FAILED_COUNT + 1))
    fi
else
    echo "[INFO] No process found on port 65109"
fi

# Find and stop process on port 65110
PORT_65110=$(lsof -ti:65110 2>/dev/null)
if [ ! -z "$PORT_65110" ]; then
    echo "[INFO] Found process on port 65110 (PID: $PORT_65110)"
    kill $PORT_65110 2>/dev/null
    if [ $? -eq 0 ]; then
        echo "        Process stopped successfully"
        STOPPED_COUNT=$((STOPPED_COUNT + 1))
    else
        echo "        ERROR: Failed to stop process (may require admin privileges)"
        FAILED_COUNT=$((FAILED_COUNT + 1))
    fi
else
    echo "[INFO] No process found on port 65110"
fi

# Find and stop dotnet process running SearchSGTestApp
DOTNET_PROCESS=$(ps aux | grep "SearchSGTestApp" | grep -v grep | awk '{print $2}')
if [ ! -z "$DOTNET_PROCESS" ]; then
    echo "[INFO] Found dotnet process (PID: $DOTNET_PROCESS)"
    kill $DOTNET_PROCESS 2>/dev/null
    if [ $? -eq 0 ]; then
        echo "        Process stopped successfully"
        STOPPED_COUNT=$((STOPPED_COUNT + 1))
    else
        echo "        ERROR: Failed to stop process"
        FAILED_COUNT=$((FAILED_COUNT + 1))
    fi
else
    echo "[INFO] No dotnet process found"
fi

echo ""
if [ $STOPPED_COUNT -gt 0 ]; then
    echo "SUCCESS: Stopped $STOPPED_COUNT process(es)"
fi

if [ $FAILED_COUNT -gt 0 ]; then
    echo "WARNING: Failed to stop $FAILED_COUNT process(es)"
    echo ""
    echo "If the application is still running, try:"
    echo "  1. Use Activity Monitor to Force Quit"
    echo "  2. Run: kill -9 \$(lsof -ti:65109,65110)"
elif [ $STOPPED_COUNT -eq 0 ]; then
    echo "INFO: No running processes found"
fi

echo ""


