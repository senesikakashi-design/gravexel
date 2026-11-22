#!/bin/sh
# Compiles the shared core + LevelTool for .NET Framework 4.8 (runs with mono or on Windows).
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
CORE="$HERE/../Unity/Assets/Gravexel/Scripts/Core"
REF=/usr/lib/mono/4.8-api
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll "$(dirname "$(readlink -f "$(which dotnet)")")"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
dotnet "$CSC" -nologo -langversion:9 -optimize+ -target:exe -out:"$HERE/LevelTool.exe" -nostdlib \
  -r:$REF/mscorlib.dll -r:$REF/System.dll -r:$REF/System.Core.dll -r:$REF/Facades/netstandard.dll \
  $(find "$CORE" -name '*.cs') "$HERE/LevelTool.cs"
