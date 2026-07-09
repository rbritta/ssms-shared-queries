using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using SsmsSharedQueries.UI;

namespace SsmsSharedQueries
{
    /// <summary>
    /// Dockable panel that hosts the shared-queries WPF control.
    /// </summary>
    [Guid(ToolWindowGuidString)]
    public sealed class SharedQueriesToolWindow : ToolWindowPane
    {
        public const string ToolWindowGuidString = "565B5321-2C09-4672-8253-5C70D4DA6CFA";

        public SharedQueriesToolWindow() : base(null)
        {
            Diagnostics.Log.Write("SharedQueriesToolWindow ctor");
            // Show the version in the panel caption (e.g. "SSMS Shared Queries 1.2.0") so an update is visible at a glance.
            var v = typeof(SharedQueriesToolWindow).Assembly.GetName().Version;
            Caption = $"SSMS Shared Queries {v.Major}.{v.Minor}.{v.Build}";
            // WPF control built entirely in code (no XAML) so the classic VSPackage
            // project doesn't need the WPF XAML build machinery.
            Content = new QueryPanelControl();
        }
    }
}
