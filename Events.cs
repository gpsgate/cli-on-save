using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RunCliCommandOnSave {
  internal class Events : IVsRunningDocTableEvents3 {
    private readonly DTE _dte;
    private readonly RunningDocumentTable _runningDocumentTable;
    private OutputWindowPane _outputPane;
    private const string kAppName = "CLI On Save";

    public Events(DTE dte, RunningDocumentTable runningDocumentTable) {
      _runningDocumentTable = runningDocumentTable;
      _dte = dte;
    }

    public int OnBeforeSave(uint docCookie) {
      ThreadHelper.ThrowIfNotOnUIThread();
      Process(docCookie, "PreSave");
      return VSConstants.S_OK;
    }

    public int OnAfterSave(uint docCookie) {
      ThreadHelper.ThrowIfNotOnUIThread();
      Process(docCookie, "PostSave");
      return VSConstants.S_OK;
    }

    public int OnAfterFirstDocumentLock(uint DocCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) { return VSConstants.S_OK; }
    public int OnBeforeLastDocumentUnlock(uint DocCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) { return VSConstants.S_OK; }
    public int OnAfterAttributeChange(uint DocCookie, uint grfAttribs) { return VSConstants.S_OK; }
    public int OnBeforeDocumentWindowShow(uint DocCookie, int fFirstShow, IVsWindowFrame pFrame) { return VSConstants.S_OK; }
    public int OnAfterDocumentWindowHide(uint DocCookie, IVsWindowFrame pFrame) { return VSConstants.S_OK; }
    int IVsRunningDocTableEvents3.OnAfterAttributeChangeEx(uint DocCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) { return VSConstants.S_OK; }
    int IVsRunningDocTableEvents2.OnAfterAttributeChangeEx(uint DocCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) { return VSConstants.S_OK; }

    private void Process(uint docCookie, string section) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var documents = _dte.Documents.Cast<Document>();
      var documentToFormat = CookieToDoc(docCookie, documents);
      if (documentToFormat != null) {
        if (_dte.ActiveWindow.Kind != "Document") return;
        var ActiveDocument = _dte.ActiveDocument;
        var FileSettings = new Settings(documentToFormat.FullName);
        if (FileSettings == null) return;
        var commands = FileSettings.GetCommand(ActiveDocument.FullName, section);
        var config = FileSettings.GetConfig(_dte);

        List<string> errors = new List<string>();
        if (commands != null) {
          var tokens = FileSettings.GetTokens(_dte);
          documentToFormat.Activate();
          foreach ((string _cmd, string _args) in commands) {
            var cmd = _cmd.Replace(tokens.file.key, tokens.file.value).
              Replace(tokens.fileName.key, tokens.fileName.value).
              Replace(tokens.filePath.key, tokens.filePath.value).
              Replace(tokens.solutionDir.key, tokens.solutionDir.value);
            var args = _args.Replace(tokens.file.key, tokens.file.value).
              Replace(tokens.fileName.key, tokens.fileName.value).
              Replace(tokens.filePath.key, tokens.filePath.value).
              Replace(tokens.solutionDir.key, tokens.solutionDir.value);
            try {
              var process = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo {
                  FileName = cmd,
                  Arguments = args,
                  WorkingDirectory = config.workingDir,
                }
              };
              process.Start();
            } catch (Exception e) {
              errors.Add($"Command: {_cmd} Args {_args} Exception: {e.Message}");
            }
          }
          ActiveDocument.Activate();
        }

        if (config.debug) {
          if (commands == null || commands.Count == 0 || errors.Count > 0) {
            Log($"{documentToFormat.FullName} Error At {section}\n");
            foreach (var error in errors) {
              Log($"\tError:{error}\n");
            }
          } else {
            Log(String.Format($"Processed {documentToFormat.FullName} section {section}\n"));
          }
        }
      }
    }

    private void Log(string message) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var dte2 = _dte as DTE2;
      if (dte2 != null) {
        var panes = dte2.ToolWindows.OutputWindow.OutputWindowPanes;
        try {
          _outputPane = panes.Item(kAppName);
        } catch (ArgumentException) {
        }

        if (_outputPane == null) {
          _outputPane = panes.Add(kAppName);
        }

        if (_outputPane != null) {
          _outputPane.OutputString(message);
        }
      }
    }

    private Document CookieToDoc(uint docCookie, IEnumerable<Document> documents) {
      ThreadHelper.ThrowIfNotOnUIThread();
      foreach (var Doc in documents) {
        if (Doc.FullName == _runningDocumentTable.GetDocumentInfo(docCookie).Moniker) {
          return Doc;
        }
      }
      return null;
    }

  }
}