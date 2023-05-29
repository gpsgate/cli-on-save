# cli-on-save
VSIX tool to run a CLI command (such as clang-format) on save.

# Targets
Supports VS2022.

# Setup
A .run-cli-on-save file is a directory-level configuration file that follows the INI format. Add .run-cli-on-save in a subfolder to override behaviour of that directory tree.

Contain 3 possible sections: [PreSave], [PostSave] and [Config] as in the following example.

Create a .run-cli-on-save and place these contents in the file.
```
; ReadMe:
; {File} = full file name and path of the file being saved.
; {SolutionDir} = solution directory path
; {FileName} = just the file name and extension of the file being saved.
; {FilePath} = just the file path no name of the file being saved.
; Commands, ExcludePaths, and Includepaths can contain multiple entries seperated by "|".
; .run-cli-on-save files can be placed in subfolder to change the behavior for that sub-folder.

[Config]
; Sets the working directory that commands will be run from.
WorkingDir={SolutionDir}
; Set to 'true' to provide feedback under Debug->Windows->Output on your commands such as the command name, args, and internal error info.
Debug=false

[PreSave]
; Pre-save Example calls echo to generate some data, and then echo with an escaped pipe character.
; Commands=cmd=cmd.exe args=/C echo {File} >> {File}_backup.bak|cmd=cmd.exe arg=/C echo =ON^|OFF
; IncludeExtensions=.cs|.ts

[PostSave]
; Runs clang-format (must have clang-format installed and in your path)
Commands=cmd=clang-format args=-style=Google -i "{File}"

; Example: calls echo to update a file, and then calls clang-format to format a file.
; Commands=cmd=cmd.exe args=/C echo "processed {FileName}" >> processed.txt|cmd=clang-format args=-style=Google -i "{File}"

; Example: to point at a clang-format file (.clang-format) just use the typical CLI syntax. Note that the file should be in your working directory
; Commands=cmd=clang-format args=-style=file -i "{File}"

IncludeExtensions=.cs|.ts
ExcludePaths=third-party|bin
```
