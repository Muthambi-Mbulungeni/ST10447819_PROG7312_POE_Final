#!/bin/bash
echo "========================================================"
echo "  Smart-X Mesh Ecosystem — Part 2"
echo "========================================================"

# Trap Ctrl+C to kill the API when the desktop app closes
trap 'kill $API_PID 2>/dev/null; echo "System shut down."' EXIT

# Resolve script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Disable telemetry to avoid read-only ~/.local permissions issues
export AVALONIA_TELEMETRY_OPTOUT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

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

# ── Ensure ASP.NET Core runtime is available ───────────────────────────────
if ! dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App"; then
    echo ""
    echo "❌ Error: ASP.NET Core runtime ('Microsoft.AspNetCore.App') is missing."
    echo "   The Web API backend requires the ASP.NET Core shared framework."
    echo "   Install it on your system (Garuda / Arch Linux):"
    echo "      sudo pacman -S aspnet-runtime-8.0"
    echo ""
    exit 1
fi

# ── Build Solution ─────────────────────────────────────────────────────────
echo ""
echo "Building Smart-X Solution..."
dotnet build "$SCRIPT_DIR/SmartX.sln" --nologo -v q
if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the compilation errors above."
    exit 1
fi
echo "✔ Build succeeded."

# ── Step 1: Start the Backend API ──────────────────────────────────────────
echo ""
echo "[1/2] Starting Backend API..."
echo "      Swagger: http://localhost:5050/swagger"
echo "      Part 1 endpoints: /api/sensors, /api/telemetry/heatmap"
echo "      Part 2 endpoints: /api/commands/stream, /api/commands/send,"
echo "                        /api/commands/undo, /api/commands/history,"
echo "                        /api/alerts/active, /api/devices, /api/predictions"
echo ""

export ASPNETCORE_URLS="http://*:5050"
cd "$SCRIPT_DIR/SmartX.Api"
dotnet run --no-build --urls "http://*:5050" &
API_PID=$!

# Wait dynamically for the API to boot and respond
echo "      Waiting for API to be ready at http://127.0.0.1:5050..."
READY=0
for i in {1..25}; do
    if curl -s -f http://127.0.0.1:5050/api/commands/stream &>/dev/null; then
        READY=1
        echo "✔ API is online and responding."
        break
    fi
    sleep 0.5
done

if [ $READY -eq 0 ]; then
    echo "⚠️ Warning: API did not respond within 12 seconds. Attempting to start Desktop client anyway..."
fi

# ── Step 2: Start the Avalonia Desktop Client ──────────────────────────────
echo ""
echo "[2/2] Starting Avalonia Desktop Client..."
echo "      Navigate to 'Command Stream' in the nav bar for Part 2 features."
echo ""

cd "$SCRIPT_DIR/SmartX.Desktop"
dotnet run --no-build

echo ""
echo "Desktop client closed. Shutting down API..."
