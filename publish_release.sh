#!/bin/bash

PROJECT=ChebsValheimLibrary
RELEASEDIR=$PROJECT/bin/Release
DLL=$RELEASEDIR/ChebsValheimLibrary.dll
README=README.md

VERSION=$1

# Check that files exist and are readable
if [ ! -f "$DLL" ]; then
    echo "Error: $DLL does not exist or is not readable."
    exit 1
fi

if [ ! -f "$README" ]; then
    echo "Error: $README does not exist or is not readable."
    exit 1
fi

mkdir Package

cp -f "$DLL" Package || { echo "Error: Failed to copy $DLL"; exit 1; }
cp -f "$README" Package || { echo "Error: Failed to copy $README"; exit 1; }

cd Package

zip -r "../$RELEASEDIR/$PROJECT.$VERSION.zip" .
