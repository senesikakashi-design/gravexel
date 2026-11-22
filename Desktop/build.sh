#!/bin/sh
# Builds Gravexel.exe (.NET Framework 4.8, included in Windows 10/11) and the Windows installer.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT="$HERE/.."
CORE="$ROOT/Unity/Assets/Gravexel/Scripts/Core"
RES="$ROOT/Unity/Assets/Gravexel/Resources"
OUT="$ROOT/Build"
REF=/usr/lib/mono/4.8-api
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll "$(dirname "$(readlink -f "$(which dotnet)")")"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
VERSION=$(grep -o 'Version = "[0-9.]*"' "$CORE/GameApp.cs" | head -1 | grep -o '[0-9.]*')
echo "Gravexel version $VERSION"
mkdir -p "$OUT/obj"

compile() {
  dotnet "$CSC" -nologo -langversion:9 -optimize+ -unsafe+ -platform:anycpu -target:winexe \
    -out:"$OUT/Gravexel.exe" -nostdlib $1 \
    -r:$REF/mscorlib.dll -r:$REF/System.dll -r:$REF/System.Core.dll -r:$REF/System.Drawing.dll \
    -r:$REF/System.Windows.Forms.dll -r:$REF/System.Web.Extensions.dll -r:$REF/Facades/netstandard.dll -r:$REF/Facades/System.Threading.Tasks.Parallel.dll \
    -resource:"$RES/levels.txt",levels.txt -resource:"$OUT/obj/fonts.bin",fonts.bin $2 \
    $(find "$CORE" -name '*.cs') "$HERE"/*.cs
}

python3 "$ROOT/Tools/make_fonts.py" "$RES/Fonts/Audiowide-Regular.ttf" "$RES/Fonts/Rajdhani-Bold.ttf" "$OUT/obj/fonts.bin"

# pass 1 (no icon) -> render the icon with the game's own renderer -> pass 2 with the icon
compile "" ""
BRAND_ICON="$ROOT/Brand/out/icons/gravexel-icon-rounded-1024.png"
if [ -f "$BRAND_ICON" ]; then
  convert "$BRAND_ICON" -resize 256x256 "$OUT/obj/icon256.png"
else
  mono "$OUT/Gravexel.exe" --icon "$OUT/obj/icon256.png"
fi
convert "$OUT/obj/icon256.png" -define icon:auto-resize=256,128,64,48,32,16 "$OUT/obj/icon.ico"
compile "-win32icon:$OUT/obj/icon.ico" "-resource:$OUT/obj/icon.ico,icon.ico"
cp "$HERE/Gravexel.exe.config" "$OUT/"
echo "built $OUT/Gravexel.exe"

# installer + portable zip
makensis -V2 -DVERSION="$VERSION" -DSRC="$OUT" -DOUT="$OUT/Gravexel-Setup-$VERSION.exe" "$ROOT/Installer/Gravexel.nsi"
rm -f "$OUT"/Gravexel-*-portable.zip
(cd "$OUT" && zip -q Gravexel-$VERSION-portable.zip Gravexel.exe Gravexel.exe.config)
echo "built $OUT/Gravexel-Setup-$VERSION.exe"
