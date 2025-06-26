#!/bin/bash

if [ $# -eq 0 ]; then
    echo "Usage: $0 <version>"
    echo "Example: $0 1.0.0"
    exit 1
fi

VERSION=$1

# Check version format
if ! [[ $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Error: Version should be in X.Y.Z format (e.g., 1.0.0)"
    exit 1
fi

echo "Updating version to $VERSION..."

# Update version in csproj file
CSPROJ_PATH="src/TranslationMod.csproj"
if [ -f "$CSPROJ_PATH" ]; then
    sed -i.bak "s/<Version>[0-9]*\.[0-9]*\.[0-9]*<\/Version>/<Version>$VERSION<\/Version>/" "$CSPROJ_PATH"
    rm "${CSPROJ_PATH}.bak" 2>/dev/null || true
    echo "✓ Updated version in $CSPROJ_PATH"
else
    echo "⚠ File $CSPROJ_PATH not found"
fi

# Update version in language packs
for lang_dir in languages/*/; do
    lang_name=$(basename "$lang_dir")
    
    # Skip template
    if [ "$lang_name" = "template" ]; then
        continue
    fi
    
    lang_pack_path="${lang_dir}language_pack.json"
    if [ -f "$lang_pack_path" ]; then
        # Use jq if available, otherwise sed
        if command -v jq >/dev/null 2>&1; then
            jq ".version = \"$VERSION\"" "$lang_pack_path" > "${lang_pack_path}.tmp" && mv "${lang_pack_path}.tmp" "$lang_pack_path"
        else
            sed -i.bak "s/\"version\": \"[^\"]*\"/\"version\": \"$VERSION\"/" "$lang_pack_path"
            rm "${lang_pack_path}.bak" 2>/dev/null || true
        fi
        echo "✓ Updated version in $lang_pack_path"
    fi
done

echo "Version successfully updated to $VERSION"
echo "Now you can create tag and push:"
echo "  git add ."
echo "  git commit -m 'Release v$VERSION'"
echo "  git tag v$VERSION"
echo "  git push origin main --tags" 