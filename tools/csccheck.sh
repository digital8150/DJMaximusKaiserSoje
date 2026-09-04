#!/usr/bin/env bash
# Compiles the front-end assemblies with Roslyn directly, without starting Unity.
# It is a fast syntax and binding check while the editor is busy or unavailable;
# Unity remains the authority on what actually ships.
set -u

UNITY_DATA="/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data"
CSC="/c/Program Files/dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll"
OUT=".agent-logs/csccheck"
mkdir -p "$OUT"

REFS=()
add_ref() { [ -f "$1" ] && REFS+=("-r:$1"); }

add_ref "$UNITY_DATA/NetStandard/ref/2.1.0/netstandard.dll"
# Some editor packages are compiled against the mscorlib facade; without it their public
# signatures fail to bind here even though Unity compiles them fine.
add_ref "$UNITY_DATA/DotNetSdk/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/mscorlib.dll"
# The folder already carries the modular UnityEditor.*Module assemblies, so the monolithic
# UnityEditor.dll is deliberately left out: referencing both makes every editor type ambiguous.
for dll in "$UNITY_DATA/Managed/UnityEngine"/*.dll; do add_ref "$dll"; done
for name in UnityEngine.UI Unity.TextMeshPro Unity.TextMeshPro.Editor Unity.InputSystem \
            Unity.InputSystem.ForUI Unity.Addressables Unity.Addressables.Editor Unity.ResourceManager; do
  add_ref "Library/ScriptAssemblies/$name.dll"
done

compile() {
  local name="$1"; shift
  local extra=()
  while [ "${1:-}" != "--" ]; do extra+=("-r:$OUT/$1.dll"); shift; done
  shift
  local sources=()
  for dir in "$@"; do
    while IFS= read -r file; do sources+=("$file"); done < <(find "$dir" -name '*.cs')
  done

  echo "=== $name (${#sources[@]} files) ==="
  dotnet "$CSC" -nologo -target:library -langversion:9.0 -nostdlib+ -noconfig \
    -define:UNITY_2023_1_OR_NEWER -define:UNITY_EDITOR -define:UNITY_STANDALONE_WIN \
    -out:"$OUT/$name.dll" "${REFS[@]}" "${extra[@]}" "${sources[@]}" 2>&1 |
    grep -E "error|warning CS0114|warning CS0108" | head -30
}

compile DJMaximusKaiserSoje.Core -- Assets/Game/Runtime/Core
compile DJMaximusKaiserSoje.Content DJMaximusKaiserSoje.Core -- Assets/Game/Runtime/Content
compile DJMaximusKaiserSoje.Gameplay DJMaximusKaiserSoje.Core DJMaximusKaiserSoje.Content -- Assets/Game/Runtime/Gameplay
compile DJMaximusKaiserSoje.App DJMaximusKaiserSoje.Core DJMaximusKaiserSoje.Content DJMaximusKaiserSoje.Gameplay -- Assets/Game/Runtime/App
compile DJMaximusKaiserSoje.Presentation DJMaximusKaiserSoje.Core -- Assets/Game/Runtime/Presentation
compile DJMaximusKaiserSoje.Editor DJMaximusKaiserSoje.Core DJMaximusKaiserSoje.Presentation -- Assets/Game/Editor
echo "done"
