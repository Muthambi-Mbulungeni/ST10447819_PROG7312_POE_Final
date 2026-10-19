#!/bin/bash
echo "========================================================"
echo "  Smart-X Mesh Ecosystem — Part 2"
echo "========================================================"

# Trap Ctrl+C to kill the API when the desktop app closes
trap 'kill $API_PID 2>/dev/null; echo "System shut down."' EXIT

# Resolve script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ── Ensure dotnet is available ─────────────────────────────────────────────
if ! command -v dotnet &> /dev/null; then
    # Check common user & system install locations
    if [ -x "$HOME/.dotnet/dotnet" ]; then
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
    elif [ -x "/usr/share/dotnet/dotnet" ]; then
        export DOTNET_ROOT="/usr/share/dotnet"
        export PATH="/usr/share/dotnet:$PATH"
    elif [ -x "/snap/bin/dotnet" ]; then
        export PATH="/snap/bin:$PATH"
    fi
fi

if ! command -v dotnet &> /dev/null; then
    echo ""
    echo "❌ Error: .NET SDK ('dotnet') is not installed or not in your PATH."
    echo ""
    echo "To install .NET 8 on your system (Garuda / Arch Linux):"
    echo "   sudo pacman -S dotnet-sdk-8.0"
    echo ""
    echo "Or install locally into ~/.dotnet without sudo:"
    echo "   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0"
    echo "   export PATH=\"\$HOME/.dotnet:\$PATH\""
    echo ""
    exit 1
fi

DOTNET_VER=$(dotnet --version 2>/dev/null)
echo "✔ Using .NET SDK version: $DOTNET_VER"

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

# Wait for the API to boot and verify
sleep 4

# ── Step 2: Start the Avalonia Desktop Client ──────────────────────────────
echo "[2/2] Starting Avalonia Desktop Client..."
echo "      Navigate to 'Command Stream' in the nav bar for Part 2 features."
echo ""

cd "$SCRIPT_DIR/SmartX.Desktop"
dotnet run

echo ""
echo "Desktop client closed. Shutting down API..."
