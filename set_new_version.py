# There's too many places where the version has to be set, so I've made a little script to do it for me each time.
#
# Locations (why):
# - AssemblyInfo.cs (for assembly versioning):
#   + AssemblyVersion("2.0.0.0")
#   + AssemblyFileVersion("2.0.0.0")
#
# - Base.cs (for DLL checking):
#   + CurrentVersion = new("2.0.0");
#
# - ChebsValheimLibrary.csproj (for nuget package)
#   + <Version>2.0.0</Version>

import argparse
import os.path
import re


class FilePatternReplacement:
    def __init__(self, file, pattern, replacement):
        self.file = file
        self.pattern = pattern
        self.replacement = replacement

    def replace(self):
        if not os.path.exists(self.file):
            return f"{self.file} does not exist"

        with open(self.file, 'r', encoding='utf-8') as f:
            content = f.read()
            modified_content = re.sub(self.pattern, self.replacement, content)

        with open(self.file, 'w', encoding='utf-8') as f:
            f.write(modified_content)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(prog='Set new version')
    parser.add_argument('version', nargs='?', type=str)
    args = parser.parse_args()

    version = args.version

    if version is None or version == "":
        parser.print_help()
        sys.exit(1)

    file_pattern_replacements = [
        FilePatternReplacement('ChebsValheimLibrary/ChebsValheimLibrary/Properties/AssemblyInfo.cs',
                               'AssemblyVersion\\([".0-9]+\\)',
                               f'AssemblyVersion("{version}.0")'),
        FilePatternReplacement('ChebsValheimLibrary/ChebsValheimLibrary/Properties/AssemblyInfo.cs',
                               'AssemblyFileVersion\\([".0-9]+\\)',
                               f'AssemblyFileVersion("{version}.0")'),
        FilePatternReplacement('ChebsValheimLibrary/ChebsValheimLibrary/Base.cs',
                               'CurrentVersion = new\\([".0-9]+\\)',
                               f'CurrentVersion = new("{version}")'),
        FilePatternReplacement('ChebsValheimLibrary/ChebsValheimLibrary/ChebsValheimLibrary.csproj',
                               '<Version>[".0-9]+',
                               f"<Version>{version}"),
    ]

    errors = [x.replace() for x in file_pattern_replacements]
    for error in errors:
        if error is not None:
            print(error)
