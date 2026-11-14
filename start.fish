#!/usr/bin/env fish
set SCRIPT_DIR (cd (dirname (status filename)); and pwd)
set -x AVALONIA_TELEMETRY_OPTOUT 1
set -x DOTNET_CLI_TELEMETRY_OPTOUT 1

if not type -q dotnet
    if test -x $HOME/.dotnet/dotnet
        set -x DOTNET_ROOT $HOME/.dotnet
        fish_add_path $HOME/.dotnet
    end
end
if not type -q dotnet
    echo "dotnet not found: sudo pacman -S dotnet-sdk-8.0 aspnet-runtime-8.0"
    exit 1
end
if not dotnet --list-runtimes 2>/dev/null | grep -q Microsoft.AspNetCore.App
    echo "ASP.NET runtime missing: sudo pacman -S aspnet-runtime-8.0"
    exit 1
end

dotnet build $SCRIPT_DIR/SmartX.sln --nologo -v q
or begin; echo "Build failed"; exit 1; end

function cleanup --on-event fish_exit
    if set -q api_pid
        pkill -P $api_pid 2>/dev/null
        kill $api_pid 2>/dev/null
    end
    echo "System shut down."
end

cd $SCRIPT_DIR/SmartX.Api
dotnet run --no-build --urls "http://*:5050" &
set -g api_pid $last_pid

echo "Waiting for API..."
for i in (seq 25)
    if curl -sf http://127.0.0.1:5050/api/commands/stream >/dev/null
        echo "API online"
        break
    end
    sleep 0.5
end

cd $SCRIPT_DIR/SmartX.Desktop
dotnet run --no-build
