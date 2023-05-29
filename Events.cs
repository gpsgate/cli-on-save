using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RunCliCommandOnSave {
  internal class Events : IVsRunningDocTableEvents3 {
    private readonly DTE _dte;
    private OutputWindowPane _outputPane;
    private readonly RunningDocumentTable _runningDocumentTable;
    private const string kAppName = "CLI On Save";
    private const string kPreSave = "PreSave";
    private const string kPostSave = "PostSave";
    Dictionary<uint, Document> _documentDict = new Dictionary<uint, Document>();

    public Events(DTE dte, RunningDocumentTable runningDocumentTable) {
      _runningDocumentTable = runningDocumentTable;
      _dte = dte;
    }

    public int OnBeforeSave(uint docCookie) {
      ThreadHelper.ThrowIfNotOnUIThread();
      Process(docCookie, kPreSave);
      return VSConstants.S_OK;
    }

    public int OnAfterSave(uint docCookie) {
      ThreadHelper.ThrowIfNotOnUIThread();
      Process(docCookie, kPostSave);
      return VSConstants.S_OK;
    }

#region default - handlers
    public int OnAfterFirstDocumentLock(uint DocCookie, uint dwRdtLockType,
                                        uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
      return VSConstants.S_OK;
    }
    public int OnBeforeLastDocumentUnlock(uint DocCookie, uint dwRdtLockType,
                                          uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
      return VSConstants.S_OK;
    }
    public int OnAfterAttributeChange(uint DocCookie, uint grfAttribs) {
      return VSConstants.S_OK;
    }
    public int OnBeforeDocumentWindowShow(uint DocCookie, int fFirstShow, IVsWindowFrame pFrame) {
      return VSConstants.S_OK;
    }
    public int OnAfterDocumentWindowHide(uint DocCookie, IVsWindowFrame pFrame) {
      return VSConstants.S_OK;
    }
    int IVsRunningDocTableEvents3.OnAfterAttributeChangeEx(uint DocCookie, uint grfAttribs,
                                                           IVsHierarchy pHierOld, uint itemidOld,
                                                           string pszMkDocumentOld,
                                                           IVsHierarchy pHierNew, uint itemidNew,
                                                           string pszMkDocumentNew) {
      return VSConstants.S_OK;
    }
    int IVsRunningDocTableEvents2.OnAfterAttributeChangeEx(uint DocCookie, uint grfAttribs,
                                                           IVsHierarchy pHierOld, uint itemidOld,
                                                           string pszMkDocumentOld,
                                                           IVsHierarchy pHierNew, uint itemidNew,
                                                           string pszMkDocumentNew) {
      return VSConstants.S_OK;
    }
#endregion

    private Document DocFromCookie(uint docCookie, IEnumerable<Document> documents) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var moniker = _runningDocumentTable.GetDocumentInfo(docCookie).Moniker;
      try {
        return documents.First(doc => { return doc.FullName == moniker; });
      } catch (Exception) {
      }
      return null;
    }

    private void LogToConsole(string message) {
      ThreadHelper.ThrowIfNotOnUIThread();
      // Get the output window
      IVsOutputWindow output = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));

      // Ensure that you have an Output pane
      IVsOutputWindowPane pane;
      Guid generalPaneGuid =
          VSConstants.GUID_OutWindowDebugPane;  // P.S. There's also the GUID_OutWindowOutputPane
      if (output.GetPane(ref generalPaneGuid, out pane) != VSConstants.S_OK) {
        output.CreatePane(ref generalPaneGuid, kAppName, 1, 1);
        output.GetPane(ref generalPaneGuid, out pane);
      }
      // Print your output
      pane.OutputString(message + "\n");
    }

    private void Process(uint docCookie, string section) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var documents = _dte.Documents.Cast<Document>();
      var documentToFormat = DocFromCookie(docCookie, documents);
      if (documentToFormat != null) {
        if (_dte.ActiveWindow.Kind != "Document")
          return;
        var ActiveDocument = _dte.ActiveDocument;
        var FileSettings = new Settings(documentToFormat.FullName);
        if (FileSettings == null)
          return;
        var commands = FileSettings.GetCommand(ActiveDocument.FullName, section);
        var config = FileSettings.GetConfig(_dte);

        List<string> errors = new List<string>();
        if (commands != null) {
          var tokens = FileSettings.GetTokens(_dte);
          documentToFormat.Activate();
          foreach ((string _cmd, string _args) in commands) {
            var cmd = _cmd.Replace(tokens.file.key, tokens.file.value)
                          .Replace(tokens.fileName.key, tokens.fileName.value)
                          .Replace(tokens.filePath.key, tokens.filePath.value)
                          .Replace(tokens.solutionDir.key, tokens.solutionDir.value);
            var args = _args.Replace(tokens.file.key, tokens.file.value)
                           .Replace(tokens.fileName.key, tokens.fileName.value)
                           .Replace(tokens.filePath.key, tokens.filePath.value)
                           .Replace(tokens.solutionDir.key, tokens.solutionDir.value);
            try {
              var process =
                  new System.Diagnostics.Process { StartInfo =
                                                       new System.Diagnostics.ProcessStartInfo {
                                                         FileName = cmd,
                                                         Arguments = args,
                                                         WorkingDirectory = config.workingDir,
                                                       } };
              process.Start();
            } catch (Exception e) {
              errors.Add($"Command: {_cmd} Args {_args} Exception: {e.Message}");
            }
          }
          ActiveDocument.Activate();
        }

        if (config.debug) {
          if (commands == null || commands.Count == 0 || errors.Count > 0) {
            LogToConsole($"{documentToFormat.FullName} Error At {section}");
            foreach (var error in errors) {
              LogToConsole($"\tError:{error}");
            }
          } else {
            LogToConsole(String.Format($"Processed {documentToFormat.FullName} section {section}"));
          }
        }
      }
    }
  }
}