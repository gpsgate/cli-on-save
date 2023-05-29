# cli-on-save
VSIX tool to run a CLI command (such as clang-format) on save.

# Targets
Supports VS2022.

# Setup
A .run-cli-on-save file is a directory-level configuration file that follows the INI format. Add .run-cli-on-save in a subfolder to override behaviour of that directory tree.

Contain 3 possible sections: [PreSave], [PostSave] and [Config] as in the following example.

Create
```
; ReadMe:
; {File} = full file name and path
; {SolutionDir} = solution directory path
; {FileName} = just the file name and extension
; {FilePath} = just the file path no name.
; Commands, ExcludePaths, and Includepaths can contain multiple entries seperated by "|".
; .run-cli-on-save files can be placed in subfolder to change the behavior for that sub-folder.

[Config]
; Sets the working directory to the solution directory.
WorkingDir={SolutionDir}
; Set to 'true' to provide feedback under Debug->Windows->Output on your commands such as the command name, args, and internal error info.
Debug=false

[PreSave]
; Pre-save Example calls echo to generate some data, and then echo with an escaped pipe character.
Commands=cmd=cmd.exe args=/C echo {File} >> {File}_backup.bak|cmd=cmd.exe arg=/C echo =ON^|OFF
IncludeExtensions=.cs|.ts

[PostSave]
; Post-Save Example calls echo to update a file, and then calls clang-format to format a file.
Commands=cmd=cmd.exe args=/C echo "processed {FileName}" >> processed.txt|cmd=clang-format args=-style=Google -i "{File}"
IncludeExtensions=.cs|.ts
ExcludePaths=third-party|bin
```
