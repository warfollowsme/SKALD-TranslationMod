# TranslationMod

A runtime localization plugin for SKALD: Against the Black Priory that enables automatic text extraction and translation without modifying the game files.

## Overview

TranslationMod is a BepInEx-based plugin that provides real-time localization capabilities for SKALD: Against the Black Priory. The plugin works entirely at runtime, meaning it doesn't require any modifications to the original game files. It automatically captures game text, manages translations, and applies them dynamically during gameplay.

## Features

- **Runtime Translation**: Translates game text on-the-fly without game modification
- **Language Pack System**: Supports customizable language packs with organized translations
- **Font Management**: Automatically handles font requirements for different character sets
- **Translation Management**: Loads and applies translations from organized CSV files
- **In-game Settings Integration**: Adds language selection to the game's settings menu

## Installation Requirements

### Prerequisites
1. **BepInEx 6 (Beta)** - BepInEx 5 doesn't work reliably with SKALD, use BepInEx 6 instead
2. **SKALD: Against the Black Priory** - The plugin is designed specifically for this game

### BepInEx Installation

#### Windows/Linux
1. Download BepInEx 6 (beta) from the [official BepInEx GitHub releases](https://github.com/BepInEx/BepInEx/releases)
2. Extract the BepInEx files to your SKALD game directory
3. Run the game once to generate BepInEx configuration files
4. The game will close after initial setup - this is normal

#### macOS (Intel)
Follow the same steps as Windows/Linux above.

#### macOS (Apple Silicon - M1/M2/M3)
**⚠️ Warning: These steps modify your game installation. Proceed at your own risk!**

BepInEx doesn't natively support ARM architecture on macOS. To make it work:

1. **Remove game signature** (required for code injection):
   ```bash
   codesign --remove-signature "SKALD: Against the Black Priory.app"
   ```

2. **Install BepInEx 6 (beta)** in your game directory

3. **Modify the launch script** - Edit `run_bepinex.sh` and replace the final execution line:

   **Replace this:**
   ```bash
   "${executable_path}" "$@"
   ```

   **With this:**
   ```bash
   current_path=$(pwd)
   exports="export LD_LIBRARY_PATH=\"$LD_LIBRARY_PATH\";export LD_PRELOAD=$LD_PRELOAD;export DYLD_LIBRARY_PATH=\"$DYLD_LIBRARY_PATH\";export DYLD_INSERT_LIBRARIES=\"$DYLD_INSERT_LIBRARIES\""
   cdir="cd \"$current_path\""
   exec="\"${executable_path}\""
   a="\"$@\""
   launch="arch;$cdir;pwd;$exports;$exec $a"
   echo "Launch Command: $launch"
   arch -x86_64 zsh -c "$launch"
   ```

4. **Make script executable:**
   ```bash
   chmod +x run_bepinex.sh
   ```

5. **Launch using the script:**
   ```bash
   ./run_bepinex.sh
   ```

*Solution adapted from [BepInEx GitHub issue #513](https://github.com/BepInEx/BepInEx/issues/513#issuecomment-1806574281)*

### Plugin Installation Steps
1. Complete BepInEx installation for your platform (see above)
2. Download the latest TranslationMod release
3. Copy `TranslationMod.dll` to `BepInEx/plugins/` folder
4. Create or download a language pack (see Language Pack Creation below)
5. Launch the game - the plugin will automatically initialize

## How It Works

The plugin operates transparently in the background:

1. **Text Interception**: Captures all text displayed in the game
2. **Translation Lookup**: Checks for available translations in loaded CSV files
3. **Smart Text Processing**: Handles complex text structures including HTML tags, numbers, and formatting
4. **Dynamic Replacement**: Replaces original text with translations in real-time
5. **Plugin Integration**: Seamlessly integrates with the game's text rendering system

## Language Pack Structure

```
LanguagePacks/
└── YourLanguage/
    ├── language_pack.json         # Language pack configuration
    ├── character_chart.json       # Font character mapping
    └── translations/              # Translation files folder
        ├── interface.csv          # UI translations
        ├── dialogues.csv          # Dialog translations
        └── items.csv              # Item translations
```

## Creating a Language Pack

Creating a new language pack consists of three main steps:

### 1. Create Character Map for Your Alphabet
Create `language_pack.json` configuration file with character mapping. **Important**: Start character indices from 90 (first free index in the game that begins a new line in font textures).

Example for Russian language:

```json
{
  "languageCode": "ru",
  "name": "Russian",
  "description": "Russian language pack for the game",
  "version": "1.0.0",
  "fontFilesPath": "fonts",
  "translationFilesPath": "translations",
  "characterChart": {
    "А": 90,
    "Б": 91,
    "В": 92,
    "Г": 93,
    "Д": 94,
    "Е": 95,
    "Ё": 96,
    "Ж": 97,
    "З": 98,
    "И": 99,
    "Й": 100,
    "К": 101,
    "Л": 102,
    "М": 103,
    "Н": 104,
    "О": 105,
    "П": 106,
    "Р": 107,
    "С": 108,
    "Т": 109,
    "У": 110,
    "Ф": 111,
    "Х": 112,
    "Ц": 113,
    "Ч": 114,
    "Ш": 115,
    "Щ": 116,
    "Ъ": 117,
    "Ы": 118,
    "Ь": 119,
    "Э": 120,
    "Ю": 121,
    "Я": 122,
    "а": 126,
    "б": 127,
    "в": 128,
    "г": 129,
    "д": 130,
    "е": 131,
    "ё": 132,
    "ж": 133,
    "з": 134,
    "и": 135,
    "й": 136,
    "к": 137,
    "л": 138,
    "м": 139,
    "н": 140,
    "о": 141,
    "п": 142,
    "р": 143,
    "с": 144,
    "т": 145,
    "у": 146,
    "ф": 147,
    "х": 148,
    "ц": 149,
    "ч": 150,
    "ш": 151,
    "щ": 152,
    "ъ": 153,
    "ы": 154,
    "ь": 155,
    "э": 156,
    "ю": 157,
    "я": 158
  }
}
```

### 2. Add Letter Images to Font Textures
Add images of alphabet letters to the font textures according to the indices specified in the character map from step 1. Each character must be placed at the correct position in the font texture atlas.

### 3. Create Translation Files
Create translations of the original text in CSV files. For convenience, all original text is already categorized. The complete text is also available in a Google Doc (link will be added) that you can duplicate and translate, then save in CSV format and transfer to the `translations` folder.

**CSV File Format** (use comma `,` as separator):
```csv
Original,Translate
New Game,Nuevo Juego
Load Game,Cargar Partida
Settings,Configuración
Health,Salud
Strength,Fuerza
```

**Organize translations by categories:**
- `interface.csv` - Menus, buttons, UI elements
- `dialogues.csv` - Character conversations
- `items.csv` - Items, spells, abilities
- `gameplay.csv` - Game mechanics, stats, descriptions

### Translation Tips
- **Exact matches**: Include complete phrases for better accuracy
- **Context matters**: Use descriptive filenames to organize translations

## Contributing

Language pack contributions are welcome! Please follow the language pack creation guide and test thoroughly before submitting.

## License

This plugin is provided as-is for the SKALD gaming community. Please respect the original game's terms of service when using this plugin. 