using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SsmsSharedQueries.Configuration;
using Task = System.Threading.Tasks.Task;

namespace SsmsSharedQueries
{
    /// <summary>
    /// Entry point for the SSMS Shared Queries plugin. Registers the tool window,
    /// the "Tools > SSMS Shared Queries" command, and the options page.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("SSMS Shared Queries", "Team library of SQL queries, backed by a git repository.", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(SharedQueriesToolWindow),
        Style = VsDockStyle.Tabbed,
        Orientation = ToolWindowOrientation.Right,
        Window = "3AE79031-E1BC-11D0-8F78-00A0C9110057")] // dock with Solution Explorer (right), pinned
    [ProvideOptionPage(typeof(SharedQueriesOptionsPage), "SSMS Shared Queries", "General", 0, 0, supportsAutomation: true)]
    [Guid(PackageGuidString)]
    public sealed class SharedQueriesPackage : AsyncPackage
    {
        public const string PackageGuidString = "AECBE929-5676-41FE-933F-41D4ED068030";

        public static readonly Guid CommandSet = new Guid("652F8C92-EE87-45AF-A821-BA4705311483");
        public const int OpenPanelCommandId = 0x0100;

        /// <summary>Set once the package has initialized; used by the panel to reach services/options.</summary>
        public static SharedQueriesPackage Instance { get; private set; }

        /// <summary>The persisted options (repo URL, branch, queries folder, local cache).</summary>
        public SharedQueriesOptionsPage Options =>
            (SharedQueriesOptionsPage)GetDialogPage(typeof(SharedQueriesOptionsPage));

        /// <summary>Open Tools &gt; Options on the SSMS Shared Queries page.</summary>
        public void ShowOptions() => ShowOptionPage(typeof(SharedQueriesOptionsPage));

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            Diagnostics.Log.Write("Package.InitializeAsync: begin");
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Instance = this;
            Diagnostics.Log.Write("Package.InitializeAsync: Instance set");

            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                var openCmd = new MenuCommand(ShowToolWindow, new CommandID(CommandSet, OpenPanelCommandId));
                commandService.AddCommand(openCmd);
                Diagnostics.Log.Write("Package.InitializeAsync: command added");
            }
            else
            {
                Diagnostics.Log.Write("Package.InitializeAsync: WARN no OleMenuCommandService");
            }
        }

        private void ShowToolWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Diagnostics.Log.Write("ShowToolWindow invoked");
            ToolWindowPane window = FindToolWindow(typeof(SharedQueriesToolWindow), 0, create: true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create the SSMS Shared Queries tool window.");

            var frame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(frame.Show());
        }
    }
}
