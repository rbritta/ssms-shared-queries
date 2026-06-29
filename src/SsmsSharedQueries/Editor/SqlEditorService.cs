using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;

namespace SsmsSharedQueries.Editor
{
    /// <summary>
    /// Reads from and writes to the active SQL query editor in SSMS via the low-level
    /// IVsTextManager / IVsTextView path (the reliable path in SSMS).
    /// All methods MUST be called on the UI thread.
    /// </summary>
    internal sealed class SqlEditorService
    {
        private readonly IVsTextManager _textManager;

        public SqlEditorService(IVsTextManager textManager)
        {
            _textManager = textManager ?? throw new ArgumentNullException(nameof(textManager));
        }

        private IVsTextView GetActiveView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int hr = _textManager.GetActiveView(1, null, out IVsTextView view);
            return ErrorHandler.Succeeded(hr) ? view : null;
        }

        public bool HasActiveQuery()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetActiveView() != null;
        }

        /// <summary>Insert text at the caret position of the active query editor.</summary>
        public bool InsertAtCaret(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = GetActiveView();
            if (view == null) return false;
            if (ErrorHandler.Failed(view.GetBuffer(out IVsTextLines lines)) || lines == null) return false;

            view.GetCaretPos(out int line, out int col);
            return ReplaceSpan(lines, line, col, line, col, text);
        }

        private static bool ReplaceSpan(IVsTextLines lines, int startLine, int startCol, int endLine, int endCol, string text)
        {
            text = text ?? string.Empty;
            IntPtr pText = Marshal.StringToCoTaskMemUni(text);
            try
            {
                var changed = new TextSpan[1];
                int hr = lines.ReplaceLines(startLine, startCol, endLine, endCol, pText, text.Length, changed);
                return ErrorHandler.Succeeded(hr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pText);
            }
        }
    }
}
