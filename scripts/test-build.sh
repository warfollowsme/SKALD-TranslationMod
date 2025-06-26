#!/bin/bash

echo "🔨 Test build TranslationMod..."

# Clean previous builds
rm -rf build release

# Compile project
echo "📦 Compiling project..."
dotnet build src/TranslationMod.csproj --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Compilation error"
    exit 1
fi

# Publish
echo "🚀 Publishing project..."
dotnet publish src/TranslationMod.csproj --configuration Release --output ./build

if [ $? -ne 0 ]; then
    echo "❌ Publishing error"
    exit 1
fi

# Create release structure
echo "📁 Creating release structure..."
mkdir -p release/TranslationMod/LanguagePacks

# Copy files
echo "📋 Copying files..."
cp build/TranslationMod.dll release/TranslationMod/
echo "✓ TranslationMod.dll copied"

cp build/Newtonsoft.Json.dll release/TranslationMod/ 2>/dev/null && echo "✓ Newtonsoft.Json.dll copied" || echo "⚠ Newtonsoft.Json.dll not found"

cp config.cfg.example release/TranslationMod/TranslationMod.cfg
echo "✓ TranslationMod.cfg created"

# Copy language packs
echo "🌍 Copying language packs..."
for lang_dir in languages/*/; do
    lang_name=$(basename "$lang_dir")
    
    # Skip template
    if [ "$lang_name" = "template" ]; then
        echo "⏭ Skipping $lang_name"
        continue
    fi
    
    echo "📦 Copying $lang_name..."
    # Remove trailing slash to copy the folder itself, not its contents
    lang_dir_clean="${lang_dir%/}"
    cp -r "$lang_dir_clean" "release/TranslationMod/LanguagePacks/"
done

# Create README
cat > release/TranslationMod/README.md << 'EOF'
# TranslationMod Test Build

## Installation
1. Extract all files to your game's BepInEx/plugins/ folder
2. The language packs will be available in the LanguagePacks/ subfolder
3. Configure plugin settings in TranslationMod.cfg if needed

## Contents
- TranslationMod.dll - Main plugin file
- Newtonsoft.Json.dll - Required dependency
- TranslationMod.cfg - Plugin configuration file
- LanguagePacks/ - Language packs directory

## Supported Languages
EOF

# Add language list
ls release/TranslationMod/LanguagePacks/ | sed 's/^/- /' >> release/TranslationMod/README.md

# Create archive
echo "🗜 Creating archive..."
cd release
zip -r TranslationMod-test.zip TranslationMod/
cd ..

echo ""
echo "✅ Test build completed!"
echo "📦 Archive: release/TranslationMod-test.zip"
echo ""
echo "📊 Release contents:"
echo "$(cd release && find TranslationMod -type f | head -20)"
if [ $(cd release && find TranslationMod -type f | wc -l) -gt 20 ]; then
    echo "... and $(( $(cd release && find TranslationMod -type f | wc -l) - 20 )) more files"
fi
echo ""
echo "🎯 Archive size: $(du -h release/TranslationMod-test.zip | cut -f1)" 