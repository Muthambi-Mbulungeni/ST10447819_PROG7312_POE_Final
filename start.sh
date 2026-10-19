#!/bin/bash
echo "========================================================"
echo "  Smart-X Mesh Ecosystem — Part 2"
echo "========================================================"

# Trap Ctrl+C to kill the API when the desktop app closes
trap 'kill $API_PID 2>/dev/null; echo "System shut down."' EXIT

# Resolve the directory this script lives in (works regardless of where you call it from)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ── Step 1: Start the Backend API ──────────────────────────────────────────
echo ""
echo "[1/2] Starting Backend API..."
echo "      Swagger: http://localhost:5050/swagger"
echo "      Part 1 endpoints: /api/sensors, /api/telemetry/heatmap"
echo "      Part 2 endpoints: /api/commands/stream, /api/commands/send,"
echo "                        /api/commands/undo, /api/commands/history,"
echo "                        /api/alerts/active, /api/devices, /api/predictions"
echo ""

cd "$SCRIPT_DIR/SmartX.Api"
dotnet run &
API_PID=$!

# Wait for the API to fully start
sleep 4

# ── Step 2: Start the Avalonia Desktop Client ──────────────────────────────
echo "[2/2] Starting Avalonia Desktop Client..."
echo "      Navigate to 'Command Stream' in the nav bar for Part 2 features."
echo ""

cd "$SCRIPT_DIR/SmartX.Desktop"
dotnet run

echo ""
echo "Desktop client closed. Shutting down API..."
