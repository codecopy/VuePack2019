using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace VuePack
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(VueContentTypeDefinition.VueContentType)]
    [ContentType("javascript")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal class VueCreationListener : IVsTextViewCreationListener
    {
        [Import]
        public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }

        [Import]
        public ITextDocumentFactoryService DocumentService { get; set; }

        private ITextDocument _document;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);

            // Both "Web Compiler" and "Bundler & Minifier" extensions add this property on their
            // generated output files. Generated output should be ignored from linting
            if (textView.Properties.TryGetProperty("generated", out bool generated) && generated)
            {
                return;
            }

            if (DocumentService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out _document))
            {
                if (_document.FilePath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))
                {
                    _document.FileActionOccurred += DocumentSaved;
                }
            }
        }

        private void DocumentSaved(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await TaskScheduler.Default;
                    DirectivesCache.ProcessFile(e.FilePath);
                });
            }
        }

        private static List<string> GetFiles(string path, string pattern)
        {
            var files = new List<string>();

            if (path.Contains("node_modules"))
            {
                return files;
            }

            try
            {
                files.AddRange(Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly));
                foreach (string directory in Directory.GetDirectories(path))
                {
                    files.AddRange(GetFiles(directory, pattern));
                }
            }
            catch { }

            return files;
        }
    }
}
