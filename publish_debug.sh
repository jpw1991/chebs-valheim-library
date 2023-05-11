#!/bin/bash

RELEASEDIR=ChebsValheimLibrary/ChebsValheimLibrary/bin/Release
LIB=$RELEASEDIR/ChebsValheimLibrary.dll
PLUGINS=/home/joshua/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins

# Check that source files exist and are readable
if [ ! -f "$LIB" ]; then
    echo "Error: $LIB does not exist or is not readable."
    exit 1
fi

# Check that target directory exists and is writable
if [ ! -d "$PLUGINS" ]; then
    echo "Error: $PLUGINS directory does not exist."
    exit 1
fi

if [ ! -w "$PLUGINS" ]; then
    echo "Error: $PLUGINS directory is not writable."
    exit 1
fi

cp -f "$LIB" "$PLUGINS" || { echo "Error: Failed to copy $LIB"; exit 1; }

