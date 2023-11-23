﻿using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace RunCliCommandOnSave {
  public class Settings {
    public const string kSettingsFileName = ".run-cli-on-save";
    private string _settingsFile = null;

    public Settings(string docFullName) {
      _settingsFile = LocateSettings(Path.GetDirectoryName(docFullName));
    }
    internal string ReadSection(string section, string key) {
      var Temp = new StringBuilder(256);
      var NumberOfChars =
          GetPrivateProfileString(section, key, null, Temp, Temp.Capacity, _settingsFile);
      return NumberOfChars == 0 ? null : Temp.ToString();
    }

    internal ((string key, string value)file, (string key, string value)fileName,
              (string key, string value)filePath, (string key, string value)solutionDir)
        GetTokens(DTE dte) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var activeDocument = dte.ActiveDocument;
      return (
          file: ("{File}", activeDocument.FullName), fileName: ("{FileName}", activeDocument.Name),
          filePath: ("{FilePath}", activeDocument.Path),
          solutionDir: ("{SolutionDir}", System.IO.Path.GetDirectoryName(dte.Solution.FullName)));
    }

    internal string ResolveTokens(DTE dte, string input) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var tokens = GetTokens(dte);
      return input.Replace(tokens.file.key, tokens.file.value)
          .Replace(tokens.fileName.key, tokens.fileName.value)
          .Replace(tokens.filePath.key, tokens.filePath.value)
          .Replace(tokens.solutionDir.key, tokens.solutionDir.value);
    }

    internal (string workingDir, bool debug) GetConfig(DTE dte) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var WDRaw = ReadSection("Config", "WorkingDir");
      var debugRaw = ReadSection("Config", "Debug");
      string workingDir =
          WDRaw == null ? System.IO.Path.GetDirectoryName(dte.Solution.FullName) : WDRaw;
      workingDir = this.ResolveTokens(dte, workingDir);
      bool debug = false;
      if (debugRaw != null) {
        bool.TryParse(debugRaw.Trim().ToLower(), out debug);
      }
      return (workingDir: workingDir, debug: debug);
    }

    public List<(string cmd, string args)> GetCommand(string docFullName, string action) {
      var result = new List<(string cmd, string args)>();
      var CommandsRaw = ReadSection(action, "Commands");
      if (CommandsRaw == null) {
        return null;
      }
      // Handles escaped pipe
      const string kPipe = "~<PIPE>~";
      CommandsRaw = CommandsRaw.Replace("^|", kPipe);
      var Commands =
          CommandsRaw.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
      if (Commands.Length == 0) {
        return null;
      }
      foreach (var c in Commands) {
        var command = c.Replace(kPipe, "|");

        string cmdPattern = @"cmd=(?<cmd>[^\s]+)";
        string argsPattern = @"args=(?<args>.*)";

        Match cmdMatch = Regex.Match(command, cmdPattern);
        Match argsMatch = Regex.Match(command, argsPattern);

        if (cmdMatch.Success && argsMatch.Success) {
          string cmd = cmdMatch.Groups["cmd"].Value;
          string args = argsMatch.Groups["args"].Value;

          result.Add((cmd: cmd, args: args));
        }
      }

      var ExcludedPathsRaw = ReadSection(action, "ExcludePaths");
      if (ExcludedPathsRaw != null) {
        var BasePath = Path.GetDirectoryName(_settingsFile);
        var ExcludedPathsRawList = ExcludedPathsRaw.Split(
            new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        var ExcludedPaths = ExcludedPathsRawList.Select(
            s => new FileInfo(Path.Combine(BasePath, s.Replace("/", "\\").Replace("\"", "")))
                     .FullName);
        try {
          foreach (var Path in ExcludedPaths) {
            if (docFullName.StartsWith(Path)) {
              return null;
            }
          }
        } catch (Exception) {
        }
      }

      var IncludeExtensionsRaw = ReadSection(action, "IncludeExtensions");
      if (IncludeExtensionsRaw != null) {
        var IncludeExtensions =
            IncludeExtensionsRaw
                .Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.TrimSuffix("*"));
        try {
          if (!IncludeExtensions.Any(ext => { return docFullName.EndsWith(ext); })) {
            return null;
          }
        } catch (Exception) {
        }
      }

      return result;
    }

    private string LocateSettings(string startingPath) {
      var dir = new DirectoryInfo(startingPath);
      while (dir != null) {
        var cfgFile = Path.Combine(dir.FullName, kSettingsFileName);
        if (File.Exists(cfgFile)) {
          return cfgFile;
        }
        dir = dir.Parent;
      }
      return null;
    }

    [DllImport("kernel32")]
    private static extern int GetPrivateProfileString(string section, string key, string def,
                                                      StringBuilder retVal, int size,
                                                      string filePath);
  }
}
