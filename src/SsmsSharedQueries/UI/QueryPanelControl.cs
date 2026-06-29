using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using SsmsSharedQueries.Editor;
using SsmsSharedQueries.Git;
using ShapePath = System.Windows.Shapes.Path;

namespace SsmsSharedQueries.UI
{
    /// <summary>One shared query file shown in the tree.</summary>
    internal sealed class QueryItem
    {
        public string DisplayName { get; set; }
        public string RelativePath { get; set; }
        public string FullPath { get; set; }
        public override string ToString() => DisplayName;
    }

    /// <summary>A folder node in the tree.</summary>
    internal sealed class FolderNode
    {
        public string FullPath { get; set; }
    }

    /// <summary>
    /// The shared-queries panel: an Object-Explorer-style toolbar, a repo root node
    /// (name, branch, ahead/behind), a folder tree with +/- expanders, colors, vector
    /// icons, favorites, metadata, locks, drag-and-drop moves, Open/Insert, folder
    /// management, Submit, Info, and a local operation history.
    /// </summary>
    internal sealed class QueryPanelControl : UserControl
    {
        private readonly TreeView _tree = new TreeView { FontSize = 13 };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 6, 4), VerticalAlignment = VerticalAlignment.Center };
        private Button _syncBtn, _submitBtn, _settingsBtn;
        private TextBlock _submitCountTb;
        private TextBlock _repoStatusTb;
        private TextBox _searchBox;
        private string _searchText = string.Empty;
        private bool _suppressExpandMemory;

        private List<QueryItem> _items = new List<QueryItem>();
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _modified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, FileMeta> _historyMap = new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TextBlock>> _nameByRel = new Dictionary<string, List<TextBlock>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TextBlock>> _metaByRel = new Dictionary<string, List<TextBlock>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _baseLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // rel -> committed (HEAD) line count
        private readonly List<string> _history = new List<string>();
        private string _repoLocal;
        private string _baseFull;
        private string _userName;
        private int _ahead, _behind;
        private bool _refreshing;
        private Style _flatStyle;
        private Point _dragStart;
        private object _dragData;
        private bool _dragging;
        private DragAdorner _dragAdorner;
        private TreeViewItem _dropTargetItem;

        private static string DataDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SsmsSharedQueries");
        private static string FavoritesPath { get; } = Path.Combine(DataDir, "favorites.txt");
        private static string HistoryPath { get; } = Path.Combine(DataDir, "history.txt");
        private static string ExpandedPath { get; } = Path.Combine(DataDir, "expanded.txt");
        private const string RepoKey = ":root:";
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---- vector icons + brushes ----------------------------------------
        private static readonly Geometry GeoFolderMaterial = ParseGeo("M10,4 H4 C2.9,4 2.01,4.9 2.01,6 L2,18 C2,19.1 2.9,20 4,20 H20 C21.1,20 22,19.1 22,18 V8 C22,6.9 21.1,6 20,6 H12 Z");
        private static readonly Color DefaultFolderColor = Color.FromRgb(0xE8, 0xB7, 0x4E);
        private static readonly Brush DropHighlightBrush = Frozen(0xCC, 0xE8, 0xFF);
        private const double TreeIconSize = 16;
        private static readonly Geometry GeoFilePage = ParseGeo("M3,1 L9,1 L12,4 L12,13 L3,13 Z");
        private static readonly Geometry GeoFileLines = ParseGeo("M9,1 L9,4 L12,4 M5,7 H10 M5,9 H10 M5,11 H10");
        private static readonly Brush FileLineBrush = Frozen(0x80, 0x80, 0x80);
        private static readonly Geometry GeoLock = ParseGeo("F0 M5,8 L5,5 A3,3 0 0 1 11,5 L11,8 L12.5,8 L12.5,15 L3.5,15 L3.5,8 Z M6.5,8 L6.5,5 A1.5,1.5 0 0 1 9.5,5 L9.5,8 Z");
        private static readonly Geometry GeoStar = ParseGeo("M8,1 L9.8,5.8 L15,6 L11,9.5 L12.3,14.5 L8,11.8 L3.7,14.5 L5,9.5 L1,6 L6.2,5.8 Z");
        private static readonly Geometry GeoDb = ParseGeo("M2,4 C2,2.9 4.7,2 8,2 C11.3,2 14,2.9 14,4 L14,12 C14,13.1 11.3,14 8,14 C4.7,14 2,13.1 2,12 Z");
        private static readonly Geometry GeoUp = ParseGeo("M8,1 L13,7 L10,7 L10,14 L6,14 L6,7 L3,7 Z");
        private static readonly Geometry GeoGear = ParseGeo("F0 M7,1 L9,1 L9.3,2.7 L10.7,3.3 L12.2,2.4 L13.6,3.8 L12.7,5.3 L13.3,6.7 L15,7 L15,9 L13.3,9.3 L12.7,10.7 L13.6,12.2 L12.2,13.6 L10.7,12.7 L9.3,13.3 L9,15 L7,15 L6.7,13.3 L5.3,12.7 L3.8,13.6 L2.4,12.2 L3.3,10.7 L2.7,9.3 L1,9 L1,7 L2.7,6.7 L3.3,5.3 L2.4,3.8 L3.8,2.4 L5.3,3.3 L6.7,2.7 Z M8,5.6 A2.4,2.4 0 1 0 8,10.4 A2.4,2.4 0 1 0 8,5.6 Z");
        private static readonly Geometry GeoSyncArc = ParseGeo("M12.2,4.2 A4.8,4.8 0 1 1 7.8,2.2");
        private static readonly Geometry GeoSyncHead = ParseGeo("M11.4,1 L13.6,4.4 L9.6,4.6 Z");
        private static readonly Brush LockBrush = Frozen(0xC0, 0x5B, 0x4A);
        private static readonly Brush StarBrush = Frozen(0xF5, 0xC2, 0x42);
        private static readonly Brush DbBrush = Frozen(0x4F, 0x8A, 0xC0);
        private static readonly Brush UpBrush = Frozen(0x4C, 0xA5, 0x4C);
        private static readonly Brush GearBrush = Frozen(0x70, 0x70, 0x70);
        private static readonly Brush ModifiedBrush = Frozen(0xC0, 0x50, 0x4D); // soft red for modified/unsubmitted
        private static readonly Brush SearchHighlightBrush = Frozen(0x1E, 0x90, 0xFF); // blue for matched letters
        private static readonly Brush MetaBrush = Frozen(0x9A, 0x9A, 0x9A);
        private static readonly Brush RepoStatBrush = Frozen(0x4F, 0x8A, 0xC0);
        private static readonly Brush TextBrush = SystemColors.WindowTextBrush;

        public QueryPanelControl()
        {
            Diagnostics.Log.Write("QueryPanelControl ctor: begin");
            Background = SystemColors.WindowBrush;
            Foreground = SystemColors.WindowTextBrush;

            LoadStyles();

            // Object-Explorer-style toolbar: flat icon buttons.
            _syncBtn = FlatButton(SyncIcon(DbBrush), "Sync (get the latest from the server)", (s, e) => { Diagnostics.Log.Write("Click: Sync"); Run("Sync", SyncAsync); });
            _submitCountTb = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0), Foreground = TextBrush };
            var submitContent = new StackPanel { Orientation = Orientation.Horizontal };
            submitContent.Children.Add(Icon(GeoUp, UpBrush));
            submitContent.Children.Add(_submitCountTb);
            _submitBtn = FlatButton(submitContent, "Submit (commit + push)", (s, e) => { Diagnostics.Log.Write("Click: Submit"); Run("Submit", SubmitAsync); });
            _submitBtn.IsEnabled = false;
            _settingsBtn = FlatButton(Icon(GeoGear, GearBrush), "Repository settings", (s, e) => SharedQueriesPackage.Instance?.ShowOptions());

            var leftButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftButtons.Children.Add(_syncBtn);
            leftButtons.Children.Add(_submitBtn);
            leftButtons.Children.Add(new Border { Width = 1, Margin = new Thickness(3, 2, 3, 2), Background = Frozen(0xC8, 0xC8, 0xC8) });
            leftButtons.Children.Add(_settingsBtn);

            // search field (fills the rest), then a search button and a clear button on the right
            _searchBox = new TextBox
            {
                MinWidth = 80,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 0),
                Padding = new Thickness(3, 1, 3, 1),
            };
            var watermark = new TextBlock
            {
                Text = "Search by name...",
                Foreground = MetaBrush,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
            _searchBox.TextChanged += (s, e) => watermark.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _searchBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; DoSearch(); } };
            var searchGrid = new Grid();
            searchGrid.Children.Add(_searchBox);
            searchGrid.Children.Add(watermark);

            var searchBtn = FlatButton(SearchIcon(GearBrush), "Search by file name (Enter)", (s, e) => DoSearch());
            var clearBtn = FlatButton(ClearIcon(GearBrush), "Clear search (restore the tree)", (s, e) => ClearSearch());

            var toolbar = new DockPanel { LastChildFill = true, Margin = new Thickness(2, 2, 2, 3) };
            DockPanel.SetDock(leftButtons, Dock.Left);
            DockPanel.SetDock(clearBtn, Dock.Right);
            DockPanel.SetDock(searchBtn, Dock.Right);
            toolbar.Children.Add(leftButtons);
            toolbar.Children.Add(clearBtn);
            toolbar.Children.Add(searchBtn);
            toolbar.Children.Add(searchGrid);

            var historyBtn = FlatButton(new TextBlock { Text = "\U0001F570", VerticalAlignment = VerticalAlignment.Center }, "Show operation history", (s, e) => ShowHistory());
            var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 2) };
            DockPanel.SetDock(historyBtn, Dock.Left);
            footer.Children.Add(historyBtn);
            footer.Children.Add(_status);

            ScrollViewer.SetHorizontalScrollBarVisibility(_tree, ScrollBarVisibility.Disabled);
            _tree.PreviewMouseLeftButtonDown += Tree_PreviewMouseLeftButtonDown;
            _tree.PreviewMouseMove += Tree_PreviewMouseMove;
            _tree.PreviewMouseLeftButtonUp += Tree_PreviewMouseLeftButtonUp;
            _tree.LostMouseCapture += (s, e) => { _dragging = false; ClearDragVisuals(); };

            var root = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(toolbar, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(toolbar);
            root.Children.Add(footer);
            root.Children.Add(_tree);
            Content = root;

            MouseEnter += (s, e) => RefreshStateFireAndForget();
            _tree.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F2 && _tree.SelectedItem is TreeViewItem it) { e.Handled = true; BeginRename(it); }
            };

            LoadFavorites();
            LoadHistory();
            LoadExpanded();
            BuildTree();

            Diagnostics.Log.Write("QueryPanelControl constructed");
            SetStatus("Set the repo (gear icon), then Sync (circular arrow).");
        }

        private void LoadStyles()
        {
            try
            {
                var rd = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(StylesXaml);
                _tree.Resources.MergedDictionaries.Add(rd);
                _flatStyle = rd["FlatBtn"] as Style;
            }
            catch (Exception ex) { Diagnostics.Log.Write("LoadStyles failed", ex); }
        }

        // ---- actions -------------------------------------------------------

        private async Task SyncAsync()
        {
            Diagnostics.Log.Write("SyncAsync: begin");
            SetStatus("Syncing...", history: false);
            var git = CreateGit();
            await git.EnsureRepositoryAsync();

            _repoLocal = git.LocalPath;
            _baseFull = string.IsNullOrWhiteSpace(BaseDirectory) ? git.LocalPath : Path.Combine(git.LocalPath, BaseDirectory);

            var identity = await git.GetIdentityAsync();
            _userName = ParseName(identity);
            _historyMap = await git.GetHistoryMapAsync();
            _modified = await git.GetChangedRelPathsAsync();
            _ahead = await git.GetAheadCountAsync();
            _behind = await git.GetBehindCountAsync();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ReloadItems();
            BuildTree();
            SetStatus(_items.Count == 0
                ? $"No .sql files found under '{BaseDirectory}'. Signed in as {identity}."
                : $"{_items.Count} queries loaded. Signed in as {identity}.");
            await RefreshStateAsync();
        }

        private async Task InsertAsync()
        {
            if (SelectedQuery is null) { SetStatus("Select a query in the tree first."); return; }
            var item = SelectedQuery;
            var sql = File.ReadAllText(item.FullPath);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var editor = GetEditorService();
            if (!editor.HasActiveQuery())
            {
                SetStatus("Open a query window in SSMS first (New Query), then click Insert.");
                return;
            }
            editor.InsertAtCaret(sql);
            SetStatus($"Inserted '{item.DisplayName}' at the cursor.");
        }

        private async Task SubmitAsync()
        {
            var git = CreateGit();
            var status = await git.GetStatusAsync();
            var ahead = await git.GetAheadCountAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (status.Count == 0 && ahead == 0) { SetStatus("No changes to submit."); return; }

            GitResult result;
            if (status.Count > 0)
            {
                var dlg = new CommitDialog(status) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() != true) { SetStatus("Submit cancelled."); return; }
                SetStatus($"Committing {status.Count} change(s) and pushing...", history: false);
                result = await git.CommitAndPushAsync(dlg.Message);
            }
            else
            {
                SetStatus($"Pushing {ahead} pending commit(s)...", history: false);
                result = await git.PushAsync();
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (result.Success)
            {
                SetStatus(status.Count > 0 ? $"Submitted {status.Count} change(s) and pushed." : $"Pushed {ahead} pending commit(s).");
            }
            else if (IsConflict(result))
            {
                var choice = System.Windows.MessageBox.Show(
                    "A file changed on the server since your last sync and conflicts with your change.\n\n" +
                    "Yes = keep MY version (overwrite the server's)\n" +
                    "No = take the SERVER's version (discard my conflicting change)\n" +
                    "Cancel = do nothing (your work stays committed locally)",
                    "Submit conflict", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Cancel)
                {
                    SetStatus("Submit stopped: conflict not resolved. Your work is committed locally.");
                }
                else
                {
                    SetStatus("Resolving conflict and pushing...", history: false);
                    var res = await git.ResolvePushAsync(preferMine: choice == MessageBoxResult.Yes);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (res.Success)
                        SetStatus(choice == MessageBoxResult.Yes
                            ? "Conflict resolved (kept your version) and pushed."
                            : "Conflict resolved (took the server's version) and pushed.");
                    else
                    {
                        var e2 = (string.IsNullOrWhiteSpace(res.StdErr) ? res.StdOut : res.StdErr) ?? string.Empty;
                        var firstResolve = e2.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
                        SetStatus(string.IsNullOrEmpty(firstResolve) ? $"Resolve failed (exit {res.ExitCode})." : "Resolve failed: " + firstResolve);
                        System.Windows.MessageBox.Show(res.ToString(), "SSMS Shared Queries - resolve failed",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                var err = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
                var first = (err ?? "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
                SetStatus(string.IsNullOrEmpty(first) ? $"Submit failed (exit {result.ExitCode})." : "Submit failed: " + first);
                System.Windows.MessageBox.Show(result.ToString(), "SSMS Shared Queries - submit failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ReloadItems();
            BuildTree();
            await RefreshStateAsync();
        }

        // ---- folder / file management --------------------------------------

        private void NewFolder(string parentFull)
        {
            if (!EnsureSynced()) return;
            var path = UniquePath(parentFull, "New Folder", string.Empty);
            Directory.CreateDirectory(path);
            FolderMeta.EnsureFile(path, FolderMeta.ReadColor(parentFull));
            ReloadItems(); BuildTree();
            var node = FindNodeByPath(path);
            if (node != null) BeginRename(node, isNew: true);
            SetStatus("Type the folder name (Enter keeps, Esc discards).", history: false);
        }

        private void NewSqlFile(string parentFull)
        {
            if (!EnsureSynced()) return;
            var path = UniquePath(parentFull, "NewQuery", ".sql");
            File.WriteAllText(path, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ReloadItems(); BuildTree();
            var node = FindNodeByPath(path);
            if (node != null) BeginRename(node, isNew: true);
            SetStatus("Type the file name (Enter keeps, Esc discards).", history: false);
        }

        private void DeleteFolder(string folderFull)
        {
            if (!EnsureSynced()) return;
            bool hasContent = Directory.GetDirectories(folderFull).Length > 0
                              || Directory.GetFiles(folderFull, "*.sql").Length > 0;
            if (hasContent)
            {
                var r = System.Windows.MessageBox.Show(
                    $"'{Path.GetFileName(folderFull)}' is not empty. Delete the folder and ALL its contents?",
                    "Delete folder", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }
            var rel = MakeRelative(_baseFull, folderFull);
            Directory.Delete(folderFull, recursive: true);
            ReloadItems(); BuildTree(); RefreshStateFireAndForget();
            SetStatus($"Deleted folder '{rel}'.");
        }

        private void SetFolderColor(string folderFull)
        {
            if (!EnsureSynced()) return;
            using (var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true })
            {
                var current = FolderMeta.ReadColor(folderFull);
                if (current != null)
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(current);
                        dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
                    }
                    catch { }
                }
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                FolderMeta.WriteColor(folderFull, hex);

                if (Directory.GetDirectories(folderFull).Length > 0)
                {
                    var apply = System.Windows.MessageBox.Show("Apply this color to all subfolders too?",
                        "Folder color", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (apply == MessageBoxResult.Yes)
                        foreach (var sub in Directory.GetDirectories(folderFull, "*", SearchOption.AllDirectories))
                            FolderMeta.WriteColor(sub, hex);
                }
                ReloadItems(); BuildTree(); RefreshStateFireAndForget();
                SetStatus($"Set color {hex} on '{MakeRelative(_baseFull, folderFull)}'.");
            }
        }

        private void ResetFolderColor(string folderFull)
        {
            if (!EnsureSynced()) return;
            FolderMeta.WriteColor(folderFull, null);
            if (Directory.GetDirectories(folderFull).Length > 0)
            {
                var apply = System.Windows.MessageBox.Show("Reset the color on all subfolders too?",
                    "Reset folder color", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (apply == MessageBoxResult.Yes)
                    foreach (var sub in Directory.GetDirectories(folderFull, "*", SearchOption.AllDirectories))
                        FolderMeta.WriteColor(sub, null);
            }
            ReloadItems(); BuildTree(); RefreshStateFireAndForget();
            SetStatus($"Reset color on '{MakeRelative(_baseFull, folderFull)}'.");
        }

        private void OpenQuery(QueryItem it)
        {
            if (!File.Exists(it.FullPath))
            {
                SetStatus($"'{it.DisplayName}' no longer exists. Sync to refresh.");
                return;
            }
            try
            {
                OpenFileInEditor(it.FullPath);
                SetStatus($"Opened '{it.DisplayName}' for editing. Save (Ctrl+S), then Submit.");
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Write("OpenQuery failed", ex);
                SetStatus($"Could not open '{it.DisplayName}': {ex.Message}");
            }
        }

        private void ToggleLock(QueryItem it)
        {
            var folder = Path.GetDirectoryName(it.FullPath);
            var fileName = Path.GetFileName(it.FullPath);
            if (FolderMeta.GetLock(folder, fileName) != null)
            {
                FolderMeta.RemoveLock(folder, fileName);
                SetStatus($"Unlocked '{it.DisplayName}'. Submit to share.");
            }
            else
            {
                FolderMeta.SetLock(folder, fileName, CurrentUser());
                SetStatus($"Locked '{it.DisplayName}' as {CurrentUser()}. Submit to share.");
            }
            ReloadItems(); BuildTree(); RefreshStateFireAndForget();
        }

        private void ToggleDeprecated(QueryItem it)
        {
            var folder = Path.GetDirectoryName(it.FullPath);
            var fileName = Path.GetFileName(it.FullPath);
            if (FolderMeta.GetDeprecation(folder, fileName) != null)
            {
                FolderMeta.RemoveDeprecation(folder, fileName);
                SetStatus($"Removed deprecated mark from '{it.DisplayName}'. Submit to share.");
            }
            else
            {
                FolderMeta.SetDeprecation(folder, fileName, CurrentUser());
                SetStatus($"Marked '{it.DisplayName}' as deprecated. Submit to share.");
            }
            ReloadItems(); BuildTree(); RefreshStateFireAndForget();
        }

        private void ShowFileInfo(QueryItem it)
        {
            Run("Info", async () =>
            {
                var git = CreateGit();
                var log = await git.GetFileLogAsync(it.RelativePath, 20);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                FileMeta meta = null;
                _historyMap?.TryGetValue(it.RelativePath, out meta);
                var folder = Path.GetDirectoryName(it.FullPath);
                var locker = FolderMeta.GetLock(folder, Path.GetFileName(it.FullPath));
                var deprecatedBy = FolderMeta.GetDeprecation(folder, Path.GetFileName(it.FullPath));
                new FileInfoDialog(it.DisplayName, CountLines(it.FullPath), meta, locker, deprecatedBy, log)
                { Owner = Window.GetWindow(this) }.ShowDialog();
                SetStatus($"Viewed info: {it.DisplayName}", history: false);
            });
        }

        private void DiscardChanges(QueryItem it)
        {
            var locker = FolderMeta.GetLock(Path.GetDirectoryName(it.FullPath), Path.GetFileName(it.FullPath));
            if (locker != null) { SetStatus($"Cannot discard '{it.DisplayName}' - locked by {locker}."); return; }
            if (!_modified.Contains(it.RelativePath)) { SetStatus("No unsubmitted changes on this file."); return; }
            Run("Discard", async () =>
            {
                var git = CreateGit();
                bool tracked = await git.IsTrackedAsync(it.RelativePath);

                if (tracked)
                {
                    // submitted file with edits -> revert ONLY this file (never deletes)
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var rt = System.Windows.MessageBox.Show(
                        $"Discard your changes to '{it.DisplayName}'?\n\nReverts ONLY this file to its last submitted version.",
                        "Discard changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (rt != MessageBoxResult.Yes) { SetStatus("Discard cancelled."); return; }
                    await git.RevertPathAsync(it.RelativePath);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ReloadItems(); BuildTree();
                    SetStatus($"Reverted '{it.DisplayName}' to the last submitted version.");
                    return;
                }

                // untracked: is this a MOVED file? (a submitted file of the same name was deleted)
                var fileName = Path.GetFileName(it.FullPath);
                var movedFrom = (await git.GetDeletedRelPathsAsync())
                    .FirstOrDefault(d => string.Equals(Path.GetFileName(d), fileName, StringComparison.OrdinalIgnoreCase));
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (movedFrom != null)
                {
                    var oldDisplay = MakeRelative(_baseFull, Path.Combine(_repoLocal, movedFrom));
                    var rm = System.Windows.MessageBox.Show(
                        $"'{it.DisplayName}' looks like a moved file.\n\nDiscard the move and put it back at '{oldDisplay}'?",
                        "Discard move", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (rm != MessageBoxResult.Yes) { SetStatus("Discard cancelled."); return; }
                    await git.RevertPathAsync(movedFrom);          // restore the original location
                    var origFolder = Path.GetDirectoryName(Path.Combine(_repoLocal, movedFrom));
                    FolderMeta.MoveFileMeta(Path.GetDirectoryName(it.FullPath), origFolder, fileName); // metadata follows it back
                    try { File.Delete(it.FullPath); } catch { }    // remove the moved copy
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ReloadItems(); BuildTree();
                    SetStatus($"Move undone: '{fileName}' restored to '{oldDisplay}'.");
                    return;
                }

                // genuinely new file -> remove only this file
                var rn = System.Windows.MessageBox.Show(
                    $"'{it.DisplayName}' is new (never submitted).\n\nDiscarding removes ONLY this file. Continue?",
                    "Discard changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (rn != MessageBoxResult.Yes) { SetStatus("Discard cancelled."); return; }
                try { File.Delete(it.FullPath); }
                catch (Exception ex) { SetStatus("Discard failed: " + ex.Message); return; }
                ReloadItems(); BuildTree();
                SetStatus($"Discarded new file '{it.DisplayName}'.");
            });
        }

        private void DeleteFile(QueryItem it)
        {
            var locker = FolderMeta.GetLock(Path.GetDirectoryName(it.FullPath), Path.GetFileName(it.FullPath));
            if (locker != null) { SetStatus($"Cannot delete '{it.DisplayName}' - locked by {locker}."); return; }
            var r = System.Windows.MessageBox.Show(
                $"Delete '{it.DisplayName}'? It will be removed from the shared library when you Submit.",
                "Delete query", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            try { File.Delete(it.FullPath); }
            catch (Exception ex) { SetStatus("Delete failed: " + ex.Message); return; }
            FolderMeta.RemoveFileMeta(Path.GetDirectoryName(it.FullPath), Path.GetFileName(it.FullPath)); // no orphan lock/deprecated line
            ReloadItems(); BuildTree(); RefreshStateFireAndForget();
            SetStatus($"Deleted '{it.DisplayName}'.");
        }

        private void DiscardFolderChanges(string folderFull)
        {
            if (!EnsureSynced()) return;
            var rel = MakeRelative(_repoLocal, folderFull);
            var r = System.Windows.MessageBox.Show(
                $"Revert all changes under '{MakeRelative(_baseFull, folderFull)}' to their last submitted version?\n\nEdited files are reverted and deleted files are restored. New (never-submitted) files are kept - delete those individually.",
                "Discard folder changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            Run("Discard", async () =>
            {
                await CreateGit().RevertPathAsync(rel);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ReloadItems(); BuildTree();
                SetStatus($"Reverted changes under '{MakeRelative(_baseFull, folderFull)}'.");
            });
        }

        // ---- inline rename -------------------------------------------------

        private static TreeViewItem NodeOf(object menuItemSender)
            => ((menuItemSender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget as TreeViewItem;

        private void BeginRename(TreeViewItem node, bool isNew = false)
        {
            string fullPath; bool isFolder;
            if (node?.Tag is FolderNode fn) { fullPath = fn.FullPath; isFolder = true; }
            else if (node?.Tag is QueryItem qi)
            {
                if (!isNew)
                {
                    var locker = FolderMeta.GetLock(Path.GetDirectoryName(qi.FullPath), Path.GetFileName(qi.FullPath));
                    if (locker != null) { SetStatus($"Cannot rename '{qi.DisplayName}' - locked by {locker}."); return; }
                }
                fullPath = qi.FullPath; isFolder = false;
            }
            else return;

            node.IsSelected = true;
            node.BringIntoView();
            var fileName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
            var editName = isFolder ? fileName : Path.GetFileNameWithoutExtension(fileName);
            var tb = new TextBox { Text = editName, MinWidth = 120, Padding = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Center };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(isFolder ? FolderIcon(ColorFromHex(FolderMeta.ReadColor(fullPath)) ?? DefaultFolderColor) : FileIcon());
            sp.Children.Add(tb);
            node.Header = sp;

            bool done = false, gotFocus = false;
            void Finish(bool commit)
            {
                if (done) return;
                done = true;
                if (commit)
                {
                    if (isNew && string.IsNullOrWhiteSpace(SanitizeName(tb.Text))) DiscardNew(fullPath, isFolder);
                    else TryRename(fullPath, isFolder, tb.Text);
                }
                else if (isNew)
                {
                    DiscardNew(fullPath, isFolder); // new + cancelled (Esc / click away) -> discard
                }
                // existing + cancelled -> keep old name (no-op)
                ReloadItems();
                BuildTree();
                RefreshStateFireAndForget();
            }
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Finish(true); }
                else if (e.Key == Key.Escape) { e.Handled = true; Finish(false); }
            };
            tb.GotKeyboardFocus += (s, e) => gotFocus = true;
            tb.LostKeyboardFocus += (s, e) => { if (gotFocus) Finish(false); };
            tb.Loaded += (s, e) => { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); };
        }

        private bool TryRename(string fullPath, bool isFolder, string newNameRaw)
        {
            try
            {
                var newName = SanitizeName(newNameRaw);
                if (string.IsNullOrWhiteSpace(newName)) return false;
                if (!isFolder && !newName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) newName += ".sql";
                var parent = Path.GetDirectoryName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
                var dest = Path.Combine(parent, newName);
                if (string.Equals(dest.TrimEnd(Path.DirectorySeparatorChar), fullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
                    return false;
                if (isFolder)
                {
                    if (Directory.Exists(dest)) { SetStatus("A folder with that name already exists."); return false; }
                    Directory.Move(fullPath, dest);
                }
                else
                {
                    if (File.Exists(dest)) { SetStatus("A file with that name already exists."); return false; }
                    File.Move(fullPath, dest);
                    FolderMeta.RenameFileMeta(parent, Path.GetFileName(fullPath), newName); // lock/deprecated follow the new name
                }
                SetStatus($"Renamed to '{newName}'.");
                return true;
            }
            catch (Exception ex) { SetStatus("Rename failed: " + ex.Message); return false; }
        }

        private static void DiscardNew(string fullPath, bool isFolder)
        {
            try { if (isFolder) Directory.Delete(fullPath, true); else File.Delete(fullPath); }
            catch { }
        }

        private static string UniquePath(string parent, string baseName, string ext)
            => QueryPaths.UniquePath(parent, baseName, ext);

        private TreeViewItem FindNodeByPath(string fullPath)
        {
            var target = fullPath.TrimEnd(Path.DirectorySeparatorChar);
            foreach (var n in EnumerateAll(_tree))
            {
                if (n.Tag is FolderNode fn && string.Equals(fn.FullPath.TrimEnd(Path.DirectorySeparatorChar), target, StringComparison.OrdinalIgnoreCase)) return n;
                if (n.Tag is QueryItem qi && string.Equals(qi.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)) return n;
            }
            return null;
        }

        private static IEnumerable<TreeViewItem> EnumerateAll(ItemsControl parent)
        {
            foreach (var o in parent.Items)
            {
                if (o is TreeViewItem tvi)
                {
                    yield return tvi;
                    foreach (var c in EnumerateAll(tvi)) yield return c;
                }
            }
        }

        // ---- drag & drop (move files/folders into folders) -----------------

        // Manual drag (no OLE DoDragDrop: that throws DV_E_FORMATETC inside SSMS).
        private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(_tree);
            _dragData = FindItem(e.OriginalSource as DependencyObject)?.Tag;
            _dragging = false;
        }

        private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragData == null || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(_tree);
            if (!_dragging)
            {
                if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                _dragging = true;
                _tree.Cursor = Cursors.Hand;
                _tree.CaptureMouse();
                ShowDragAdorner();
            }
            _dragAdorner?.UpdatePosition(p.X, p.Y);
            HighlightDropTarget(TargetFolderItem(FindItem(_tree.InputHitTest(p) as DependencyObject)));
        }

        private void Tree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) { _dragData = null; return; }
            _dragging = false;
            _tree.ReleaseMouseCapture();
            ClearDragVisuals();

            var data = _dragData;
            _dragData = null;
            var hit = _tree.InputHitTest(e.GetPosition(_tree)) as DependencyObject;
            var folder = TargetFolderOf(FindItem(hit));
            if (folder == null || data == null) return;

            if (data is QueryItem qi)
            {
                var locker = FolderMeta.GetLock(Path.GetDirectoryName(qi.FullPath), Path.GetFileName(qi.FullPath));
                if (locker != null) { SetStatus($"Cannot move '{qi.DisplayName}' - locked by {locker}."); return; }
            }
            MoveInto(data, folder);
        }

        private string TargetFolderOf(TreeViewItem item)
        {
            if (item?.Tag is FolderNode fn) return fn.FullPath;
            if (item?.Tag is QueryItem qi) return Path.GetDirectoryName(qi.FullPath);
            return null;
        }

        private void ShowDragAdorner()
        {
            try
            {
                var layer = AdornerLayer.GetAdornerLayer(_tree);
                if (layer == null) return;
                var name = _dragData is QueryItem qi ? Path.GetFileName(qi.FullPath)
                         : _dragData is FolderNode fn ? Path.GetFileName(fn.FullPath.TrimEnd(Path.DirectorySeparatorChar))
                         : "item";
                var ghost = new Border
                {
                    Background = Frozen(0x33, 0x33, 0x33),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(7, 2, 7, 3),
                    Child = new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 12 },
                };
                _dragAdorner = new DragAdorner(_tree, ghost, layer);
            }
            catch { }
        }

        private void HighlightDropTarget(TreeViewItem folderItem)
        {
            if (ReferenceEquals(folderItem, _dropTargetItem)) return;
            if (_dropTargetItem != null) _dropTargetItem.Background = null;
            _dropTargetItem = folderItem;
            if (_dropTargetItem != null) _dropTargetItem.Background = DropHighlightBrush;
        }

        private TreeViewItem TargetFolderItem(TreeViewItem item)
        {
            if (item == null) return null;
            if (item.Tag is FolderNode) return item;
            if (item.Tag is QueryItem) return ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem;
            return null;
        }

        private void ClearDragVisuals()
        {
            _tree.Cursor = Cursors.Arrow;
            _dragAdorner?.Detach();
            _dragAdorner = null;
            if (_dropTargetItem != null) { _dropTargetItem.Background = null; _dropTargetItem = null; }
        }

        private void MoveInto(object dragged, string targetFolder)
        {
            try
            {
                if (dragged is QueryItem qi)
                {
                    if (string.Equals(Path.GetDirectoryName(qi.FullPath), targetFolder, StringComparison.OrdinalIgnoreCase)) return;
                    var dest = Path.Combine(targetFolder, Path.GetFileName(qi.FullPath));
                    if (File.Exists(dest)) { SetStatus("Target already has a file with that name."); return; }
                    var srcFolder = Path.GetDirectoryName(qi.FullPath);
                    File.Move(qi.FullPath, dest);
                    FolderMeta.MoveFileMeta(srcFolder, targetFolder, Path.GetFileName(dest)); // per-file metadata follows the file
                    SetStatus($"Moved '{Path.GetFileName(qi.FullPath)}' to '{MakeRelative(_baseFull, targetFolder)}'.");
                }
                else if (dragged is FolderNode fn)
                {
                    var srcName = Path.GetFileName(fn.FullPath.TrimEnd(Path.DirectorySeparatorChar));
                    if (IsSameOrDescendant(targetFolder, fn.FullPath)) { SetStatus("Cannot move a folder into itself."); return; }
                    var dest = Path.Combine(targetFolder, srcName);
                    if (string.Equals(fn.FullPath.TrimEnd(Path.DirectorySeparatorChar), dest.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return;
                    if (Directory.Exists(dest)) { SetStatus("Target already has a folder with that name."); return; }
                    Directory.Move(fn.FullPath, dest);
                    SetStatus($"Moved folder '{srcName}' to '{MakeRelative(_baseFull, targetFolder)}'.");
                }
                ReloadItems(); BuildTree(); RefreshStateFireAndForget();
            }
            catch (Exception ex) { SetStatus("Move failed: " + ex.Message); }
        }

        private static bool IsSameOrDescendant(string target, string folder)
            => QueryPaths.IsSameOrDescendant(target, folder);

        // ---- tree ----------------------------------------------------------

        private QueryItem SelectedQuery => (_tree.SelectedItem as TreeViewItem)?.Tag as QueryItem;

        private void ReloadItems()
        {
            var items = new List<QueryItem>();
            if (_baseFull != null && Directory.Exists(_baseFull))
            {
                foreach (var f in Directory.GetFiles(_baseFull, "*.sql", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(new QueryItem
                    {
                        FullPath = f,
                        RelativePath = MakeRelative(_repoLocal, f),
                        DisplayName = MakeRelative(_baseFull, f),
                    });
                }
            }
            _items = items;
        }

        private void BuildTree()
        {
            _suppressExpandMemory = true;  // setting IsExpanded while (re)building must not pollute the saved state
            try { BuildTreeCore(); }
            finally { _suppressExpandMemory = false; }
        }

        private void BuildTreeCore()
        {
            _tree.Items.Clear();
            _nameByRel.Clear();
            _metaByRel.Clear();
            _repoStatusTb = null;

            var favItems = _items.Where(i => _favorites.Contains(i.RelativePath))
                                 .Where(i => NameMatches(Path.GetFileName(i.FullPath)))
                                 .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            if (favItems.Count > 0)
            {
                var favHeader = new StackPanel { Orientation = Orientation.Horizontal };
                favHeader.Children.Add(Icon(GeoStar, StarBrush));
                favHeader.Children.Add(new TextBlock { Text = "Favorites", FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
                var favNode = new TreeViewItem { Header = favHeader, IsExpanded = true, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                foreach (var it in favItems)
                {
                    var locker = FolderMeta.GetLock(Path.GetDirectoryName(it.FullPath), Path.GetFileName(it.FullPath));
                    favNode.Items.Add(MakeLeaf(it, showFullPath: true, locker: locker));
                }
                _tree.Items.Add(favNode);
            }

            if (_baseFull != null && Directory.Exists(_baseFull))
            {
                var repoHeader = new StackPanel { Orientation = Orientation.Horizontal };
                repoHeader.Children.Add(RepoIcon());
                repoHeader.Children.Add(new TextBlock { Text = RepoName(), FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
                repoHeader.Children.Add(new TextBlock { Text = "  " + BasePathLabel(), FontStyle = FontStyles.Italic, Foreground = MetaBrush, VerticalAlignment = VerticalAlignment.Center });
                repoHeader.Children.Add(new TextBlock { Text = $"  ({BranchName()})", Foreground = MetaBrush, VerticalAlignment = VerticalAlignment.Center });
                _repoStatusTb = new TextBlock { Margin = new Thickness(6, 0, 0, 0), Foreground = RepoStatBrush, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                UpdateRepoStatusText();
                repoHeader.Children.Add(_repoStatusTb);

                var repoNode = new TreeViewItem
                {
                    Header = repoHeader,
                    IsExpanded = true, // the repo node always starts open
                    Tag = new FolderNode { FullPath = _baseFull },
                    ContextMenu = BuildFolderMenu(_baseFull, isRoot: true),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                };
                _tree.Items.Add(repoNode);
                AddDirectory(repoNode, _baseFull);
            }
        }

        private void UpdateRepoStatusText()
        {
            if (_repoStatusTb == null) return;
            var parts = new List<string>();
            if (_ahead > 0) parts.Add($"↑{_ahead}");
            if (_behind > 0) parts.Add($"↓{_behind}");
            _repoStatusTb.Text = parts.Count > 0 ? "  " + string.Join("  ", parts) : string.Empty;
        }

        private void AddDirectory(ItemsControl parent, string dirFull)
        {
            foreach (var sub in Directory.GetDirectories(dirFull).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                if (Searching && !DirMatches(sub)) continue; // hide branches with no matching file
                // Show the folder's OWN color, or the default yellow if it has none
                // (inheritance happens at create time by writing into the child's .ssq).
                var node = MakeFolderNode(sub, Path.GetFileName(sub), FolderMeta.ReadColor(sub));
                if (Searching) node.IsExpanded = true; // reveal matches (guarded: doesn't touch saved state)
                parent.Items.Add(node);
                AddDirectory(node, sub);
            }

            var locks = FolderMeta.GetLocks(dirFull);
            foreach (var f in Directory.GetFiles(dirFull, "*.sql").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (Searching && !NameMatches(Path.GetFileName(f))) continue;
                var it = _items.FirstOrDefault(i => string.Equals(i.FullPath, f, StringComparison.OrdinalIgnoreCase))
                         ?? new QueryItem { FullPath = f, RelativePath = MakeRelative(_repoLocal, f), DisplayName = MakeRelative(_baseFull, f) };
                locks.TryGetValue(Path.GetFileName(f), out var locker);
                parent.Items.Add(MakeLeaf(it, showFullPath: false, locker: locker));
            }
        }

        private TreeViewItem MakeFolderNode(string fullPath, string headerName, string colorHex)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(FolderIcon(ColorFromHex(colorHex) ?? DefaultFolderColor));
            // search matches FILE names only; folder names are never highlighted/searched.
            sp.Children.Add(new TextBlock { Text = headerName, VerticalAlignment = VerticalAlignment.Center, Foreground = TextBrush, FontWeight = FontWeights.SemiBold });
            var key = ExpandKey(fullPath);
            var node = new TreeViewItem
            {
                Header = sp,
                IsExpanded = _expanded.Contains(key),
                Tag = new FolderNode { FullPath = fullPath },
                ContextMenu = BuildFolderMenu(fullPath),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AttachExpandMemory(node, key);
            return node;
        }

        private TreeViewItem MakeLeaf(QueryItem it, bool showFullPath, string locker)
        {
            bool fav = _favorites.Contains(it.RelativePath);
            bool modified = _modified.Contains(it.RelativePath);
            bool locked = locker != null;
            bool deprecated = FolderMeta.GetDeprecation(Path.GetDirectoryName(it.FullPath), Path.GetFileName(it.FullPath)) != null;
            // "committed" = the file exists in the repo history; lock/deprecate only apply to shared
            // (already-submitted) files, so those actions are disabled on brand-new files.
            bool committed = _historyMap != null && _historyMap.TryGetValue(it.RelativePath, out var hist) && hist.LastAuthor != null;
            var name = showFullPath ? it.DisplayName : it.DisplayName.Replace('\\', '/').Split('/').Last();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // file/lock icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // favorite star (after name)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // meta

            var fileIcon = locked ? (UIElement)Icon(GeoLock, LockBrush, TreeIconSize) : FileIcon();
            Grid.SetColumn(fileIcon, 0);
            grid.Children.Add(fileIcon);

            var nameTb = BuildHighlightedName(name, modified ? ModifiedBrush : TextBrush);
            if (modified) nameTb.FontWeight = FontWeights.SemiBold;
            if (deprecated) nameTb.TextDecorations = TextDecorations.Strikethrough; // deprecated -> struck through
            Grid.SetColumn(nameTb, 1);
            grid.Children.Add(nameTb);
            if (!_nameByRel.TryGetValue(it.RelativePath, out var list)) { list = new List<TextBlock>(); _nameByRel[it.RelativePath] = list; }
            list.Add(nameTb);

            if (fav)
            {
                var star = Icon(GeoStar, StarBrush);
                star.Margin = new Thickness(5, 0, 0, 0);
                Grid.SetColumn(star, 2);
                grid.Children.Add(star);
            }

            var metaTb = new TextBlock
            {
                Text = "   " + MetaText(it),
                FontStyle = FontStyles.Italic,
                Foreground = MetaBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(metaTb, 3);
            grid.Children.Add(metaTb);
            if (!_metaByRel.TryGetValue(it.RelativePath, out var mlist)) { mlist = new List<TextBlock>(); _metaByRel[it.RelativePath] = mlist; }
            mlist.Add(metaTb);

            var leaf = new TreeViewItem
            {
                Header = grid,
                Tag = it,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ToolTip = locked ? $"{it.DisplayName}\nLocked by {locker}" : it.DisplayName,
            };

            var menu = new ContextMenu();
            // -- content --
            var open = new MenuItem { Header = locked ? $"Open (locked by {locker})" : "Open", IsEnabled = !locked };
            open.Click += (s, e) => { SelectLeaf(it); OpenQuery(it); };
            menu.Items.Add(open);
            var discard = new MenuItem { Header = "Discard changes", IsEnabled = !locked && modified };
            discard.Click += (s, e) => DiscardChanges(it);
            menu.Items.Add(discard);
            var insert = new MenuItem { Header = "Insert into editor" };
            insert.Click += (s, e) => { SelectLeaf(it); Run("Insert", InsertAsync); };
            menu.Items.Add(insert);
            // -- file management --
            menu.Items.Add(new Separator());
            var rename = new MenuItem { Header = "Rename...", IsEnabled = !locked };
            rename.Click += (s, e) => BeginRename(NodeOf(s));
            menu.Items.Add(rename);
            var del = new MenuItem { Header = "Delete", IsEnabled = !locked };
            del.Click += (s, e) => DeleteFile(it);
            menu.Items.Add(del);
            // -- organize --
            menu.Items.Add(new Separator());
            var toggleFav = new MenuItem { Header = fav ? "Remove from favorites" : "Add to favorites" };
            toggleFav.Click += (s, e) => ToggleFavorite(it);
            menu.Items.Add(toggleFav);
            var toggleLock = new MenuItem { Header = locked ? "Unlock" : "Lock for editing", IsEnabled = committed };
            toggleLock.Click += (s, e) => ToggleLock(it);
            menu.Items.Add(toggleLock);
            var toggleDeprecated = new MenuItem { Header = deprecated ? "Remove deprecated mark" : "Mark as deprecated", IsEnabled = committed };
            toggleDeprecated.Click += (s, e) => ToggleDeprecated(it);
            menu.Items.Add(toggleDeprecated);
            // -- info --
            menu.Items.Add(new Separator());
            var info = new MenuItem { Header = "Info" };
            info.Click += (s, e) => ShowFileInfo(it);
            menu.Items.Add(info);
            // keep Discard's enabled state correct even if _modified changed since the tree was built
            menu.Opened += (s, e) =>
            {
                var f = Path.GetDirectoryName(it.FullPath);
                var fn = Path.GetFileName(it.FullPath);
                var lk = FolderMeta.GetLock(f, fn);
                discard.IsEnabled = lk == null && _modified.Contains(it.RelativePath);
                // the deprecated mark can change without a tree rebuild (auto-clear on edit), so
                // re-read it here, else the label goes stale and the toggle inverts.
                toggleDeprecated.Header = FolderMeta.GetDeprecation(f, fn) != null ? "Remove deprecated mark" : "Mark as deprecated";
            };
            leaf.ContextMenu = menu;

            leaf.MouseDoubleClick += (s, e) =>
            {
                if (!ReferenceEquals(_tree.SelectedItem, leaf)) return;
                e.Handled = true;
                // Double-click does the same as right-click > Open (a no-op while locked, like the menu item).
                if (!locked) { SelectLeaf(it); OpenQuery(it); }
            };
            return leaf;
        }

        private ContextMenu BuildFolderMenu(string folderFull, bool isRoot = false)
        {
            var menu = new ContextMenu();
            // -- create --
            var newFile = new MenuItem { Header = "New SQL file..." };
            newFile.Click += (s, e) => NewSqlFile(folderFull);
            menu.Items.Add(newFile);
            var newFolder = new MenuItem { Header = "New folder..." };
            newFolder.Click += (s, e) => NewFolder(folderFull);
            menu.Items.Add(newFolder);
            // -- folder management --
            menu.Items.Add(new Separator());
            if (!isRoot)
            {
                var rename = new MenuItem { Header = "Rename..." };
                rename.Click += (s, e) => BeginRename(NodeOf(s));
                menu.Items.Add(rename);
            }
            var discard = new MenuItem { Header = "Discard changes in folder", IsEnabled = FolderHasChanges(folderFull) };
            discard.Click += (s, e) => DiscardFolderChanges(folderFull);
            menu.Items.Add(discard);
            if (!isRoot)
            {
                var del = new MenuItem { Header = "Delete folder..." };
                del.Click += (s, e) => DeleteFolder(folderFull);
                menu.Items.Add(del);
                // -- appearance --
                menu.Items.Add(new Separator());
                var color = new MenuItem { Header = "Set color..." };
                color.Click += (s, e) => SetFolderColor(folderFull);
                menu.Items.Add(color);
                var resetColor = new MenuItem { Header = "Reset color" };
                resetColor.Click += (s, e) => ResetFolderColor(folderFull);
                menu.Items.Add(resetColor);
            }
            menu.Opened += (s, e) => discard.IsEnabled = FolderHasChanges(folderFull);
            return menu;
        }

        private void RecolorModified()
        {
            foreach (var kv in _nameByRel)
            {
                bool mod = _modified.Contains(kv.Key);
                bool dep = IsDeprecatedRel(kv.Key);
                foreach (var tb in kv.Value)
                {
                    tb.Foreground = mod ? ModifiedBrush : TextBrush;
                    tb.FontWeight = mod ? FontWeights.SemiBold : FontWeights.Normal;
                    tb.TextDecorations = dep ? TextDecorations.Strikethrough : null;
                }
            }
        }

        private bool IsDeprecatedRel(string rel)
        {
            if (_repoLocal == null) return false;
            var full = Path.Combine(_repoLocal, rel);
            return FolderMeta.GetDeprecation(Path.GetDirectoryName(full), Path.GetFileName(full)) != null;
        }

        /// <summary>Refresh the gray metadata text in place (line count, committed->current) without rebuilding.</summary>
        private void RefreshMeta()
        {
            if (_metaByRel.Count == 0) return;
            foreach (var kv in _metaByRel)
            {
                var it = _items.FirstOrDefault(i => string.Equals(i.RelativePath, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (it == null) continue;
                var text = "   " + MetaText(it);
                foreach (var tb in kv.Value) tb.Text = text;
            }
        }

        private void SelectLeaf(QueryItem it)
        {
            foreach (var leaf in EnumerateLeaves(_tree))
                if (ReferenceEquals(leaf.Tag, it)) { leaf.IsSelected = true; return; }
        }

        private static IEnumerable<TreeViewItem> EnumerateLeaves(ItemsControl parent)
        {
            foreach (var obj in parent.Items)
            {
                if (obj is TreeViewItem tvi)
                {
                    if (tvi.Tag is QueryItem) yield return tvi;
                    foreach (var child in EnumerateLeaves(tvi)) yield return child;
                }
            }
        }

        private static TreeViewItem FindItem(DependencyObject src)
        {
            while (src != null && !(src is TreeViewItem)) src = VisualTreeHelper.GetParent(src);
            return src as TreeViewItem;
        }

        // ---- favorites -----------------------------------------------------

        private void ToggleFavorite(QueryItem it)
        {
            if (!_favorites.Remove(it.RelativePath)) _favorites.Add(it.RelativePath);
            SaveFavorites();
            BuildTree();
            SetStatus($"{(_favorites.Contains(it.RelativePath) ? "Added to" : "Removed from")} favorites: {it.DisplayName}");
        }

        private void LoadFavorites()
        {
            try
            {
                _favorites.Clear();
                if (File.Exists(FavoritesPath))
                    foreach (var line in File.ReadAllLines(FavoritesPath))
                        if (!string.IsNullOrWhiteSpace(line)) _favorites.Add(line.Trim());
            }
            catch (Exception ex) { Diagnostics.Log.Write("LoadFavorites failed", ex); }
        }

        private void SaveFavorites()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(FavoritesPath, _favorites.OrderBy(x => x));
            }
            catch (Exception ex) { Diagnostics.Log.Write("SaveFavorites failed", ex); }
        }

        // ---- search (visual filter + blue highlight, local only) -----------

        private bool Searching => !string.IsNullOrEmpty(_searchText);

        private void DoSearch()
        {
            _searchText = (_searchBox.Text ?? string.Empty).Trim();
            BuildTree();
            if (Searching)
            {
                int n = CountVisibleMatches();
                SetStatus(n == 0 ? $"No queries match '{_searchText}'." : $"{n} quer{(n == 1 ? "y" : "ies")} match '{_searchText}'.", history: false);
            }
            else SetStatus("Search cleared.", history: false);
        }

        private void ClearSearch()
        {
            if (_searchBox.Text.Length == 0 && !Searching) return;
            _searchBox.Text = string.Empty;
            _searchText = string.Empty;
            BuildTree();
            SetStatus("Search cleared.", history: false);
        }

        private int CountVisibleMatches() => _items.Count(i => NameMatches(Path.GetFileName(i.FullPath)));

        private bool NameMatches(string fileName)
            => !Searching || fileName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>True if the folder (recursively) contains at least one matching .sql file.</summary>
        private bool DirMatches(string dirFull)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dirFull, "*.sql"))
                    if (NameMatches(Path.GetFileName(f))) return true;
                foreach (var sub in Directory.GetDirectories(dirFull))
                    if (DirMatches(sub)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>A name TextBlock; when searching, the matched letters are rendered in blue.</summary>
        private TextBlock BuildHighlightedName(string name, Brush baseBrush)
        {
            var tb = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = baseBrush };
            if (!Searching) { tb.Text = name; return tb; }
            var term = _searchText;
            int idx = 0;
            while (idx <= name.Length)
            {
                int matchPos = idx < name.Length ? name.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase) : -1;
                if (matchPos < 0) { if (idx < name.Length) tb.Inlines.Add(new Run(name.Substring(idx))); break; }
                if (matchPos > idx) tb.Inlines.Add(new Run(name.Substring(idx, matchPos - idx)));
                tb.Inlines.Add(new Run(name.Substring(matchPos, term.Length)) { Foreground = SearchHighlightBrush, FontWeight = FontWeights.Bold });
                idx = matchPos + term.Length;
            }
            return tb;
        }

        private static UIElement SearchIcon(Brush brush)
        {
            var g = new Grid { Width = 15, Height = 15 };
            g.Children.Add(new System.Windows.Shapes.Ellipse { Width = 9, Height = 9, Stroke = brush, StrokeThickness = 1.6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(1, 1, 0, 0) });
            g.Children.Add(new System.Windows.Shapes.Line { X1 = 9.5, Y1 = 9.5, X2 = 14, Y2 = 14, Stroke = brush, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            return g;
        }

        private static UIElement ClearIcon(Brush brush)
        {
            var g = new Grid { Width = 14, Height = 14 };
            g.Children.Add(new System.Windows.Shapes.Line { X1 = 3, Y1 = 3, X2 = 11, Y2 = 11, Stroke = brush, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            g.Children.Add(new System.Windows.Shapes.Line { X1 = 11, Y1 = 3, X2 = 3, Y2 = 11, Stroke = brush, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            return g;
        }

        /// <summary>True if any unsubmitted change (.sql or .ssq) lives under this folder.</summary>
        private bool FolderHasChanges(string folderFull)
        {
            if (_repoLocal == null || _modified == null || _modified.Count == 0) return false;
            var rel = MakeRelative(_repoLocal, folderFull).TrimEnd('/');
            if (rel.Length == 0) return true; // repo root: any change counts
            foreach (var m in _modified)
                if (m.Equals(rel, StringComparison.OrdinalIgnoreCase)
                    || m.StartsWith(rel + "/", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---- expand/collapse memory (local cache, not synced to git) -------

        private string ExpandKey(string folderFull) => MakeRelative(_baseFull, folderFull);

        private void AttachExpandMemory(TreeViewItem node, string key)
        {
            node.Expanded += (s, e) => { if (!_suppressExpandMemory && ReferenceEquals(s, e.OriginalSource) && _expanded.Add(key)) SaveExpanded(); };
            node.Collapsed += (s, e) => { if (!_suppressExpandMemory && ReferenceEquals(s, e.OriginalSource) && _expanded.Remove(key)) SaveExpanded(); };
        }

        private void LoadExpanded()
        {
            try
            {
                if (File.Exists(ExpandedPath))
                    foreach (var l in File.ReadAllLines(ExpandedPath))
                        if (!string.IsNullOrWhiteSpace(l)) _expanded.Add(l.Trim());
                else SaveExpanded(); // first run: all folders closed (the repo node is always open)
            }
            catch (Exception ex) { Diagnostics.Log.Write("LoadExpanded failed", ex); }
        }

        private void SaveExpanded()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(ExpandedPath, _expanded.OrderBy(x => x));
            }
            catch (Exception ex) { Diagnostics.Log.Write("SaveExpanded failed", ex); }
        }

        // ---- history -------------------------------------------------------

        private void ShowHistory()
            => new HistoryDialog(_history) { Owner = Window.GetWindow(this) }.ShowDialog();

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                    _history.AddRange(File.ReadAllLines(HistoryPath).Where(l => !string.IsNullOrWhiteSpace(l)));
            }
            catch (Exception ex) { Diagnostics.Log.Write("LoadHistory failed", ex); }
        }

        private void AddHistory(string text)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}";
            _history.Add(line);
            try { Directory.CreateDirectory(DataDir); File.AppendAllText(HistoryPath, line + Environment.NewLine); }
            catch { }
        }

        // ---- change count + recolor ---------------------------------------

        private void RefreshStateFireAndForget()
            => ThreadHelper.JoinableTaskFactory.RunAsync(RefreshStateAsync).FileAndForget("SsmsSharedQueries/state");

        private async Task RefreshStateAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            int count = 0, ahead = 0, behind = 0;
            HashSet<string> modified = null;
            Dictionary<string, int> newBase = null;
            try
            {
                if (_repoLocal != null && Directory.Exists(Path.Combine(_repoLocal, ".git")))
                {
                    var git = CreateGit();
                    var status = await git.GetStatusAsync();
                    ahead = await git.GetAheadCountAsync();
                    behind = await git.GetBehindCountAsync();
                    count = status.Count + ahead;
                    modified = await git.GetChangedRelPathsAsync();

                    // editing + saving a deprecated .sql clears its deprecated mark
                    foreach (var rel in modified)
                    {
                        if (!rel.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;
                        var full = Path.Combine(_repoLocal, rel);
                        var folder = Path.GetDirectoryName(full);
                        var fileName = Path.GetFileName(full);
                        if (FolderMeta.GetDeprecation(folder, fileName) != null)
                            FolderMeta.RemoveDeprecation(folder, fileName);
                    }

                    // committed (HEAD) line count for each modified file, cached so each file
                    // costs one "git show" only the first time it becomes modified.
                    var prevBase = _baseLines;
                    newBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rel in modified)
                    {
                        if (prevBase.TryGetValue(rel, out var cached)) { newBase[rel] = cached; continue; }
                        var n = await git.GetHeadLineCountAsync(rel);
                        if (n >= 0) newBase[rel] = n;
                    }
                }
            }
            catch (Exception ex) { Diagnostics.Log.Write("RefreshState failed", ex); }
            finally { _refreshing = false; }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _submitCountTb.Text = count > 0 ? count.ToString() : string.Empty;
            _submitBtn.IsEnabled = count > 0;
            _ahead = ahead; _behind = behind;
            UpdateRepoStatusText();
            if (modified != null) { _modified = modified; if (newBase != null) _baseLines = newBase; RecolorModified(); RefreshMeta(); }
        }

        // ---- helpers -------------------------------------------------------

        private bool EnsureSynced()
        {
            if (_baseFull != null && Directory.Exists(_baseFull)) return true;
            SetStatus("Sync first (circular-arrow button) to set up the local repository.");
            return false;
        }

        private string CurrentUser() => string.IsNullOrWhiteSpace(_userName) ? Environment.UserName : _userName;

        private static string ParseName(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return null;
            var lt = identity.IndexOf(" <", StringComparison.Ordinal);
            return lt > 0 ? identity.Substring(0, lt) : identity;
        }

        private static bool IsConflict(GitResult r)
        {
            var t = ((r?.StdErr ?? string.Empty) + (r?.StdOut ?? string.Empty)).ToLowerInvariant();
            return t.Contains("conflict") || t.Contains("could not apply") || t.Contains("resolve all conflicts");
        }

        private static string RepoName()
        {
            var url = (SharedQueriesPackage.Instance?.Options?.RepositoryUrl ?? string.Empty).TrimEnd('/');
            if (url.Length == 0) return "repository";
            var name = url.Substring(url.LastIndexOf('/') + 1);
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return name.Length == 0 ? "repository" : name;
        }

        private static string BranchName()
        {
            var b = SharedQueriesPackage.Instance?.Options?.Branch;
            return string.IsNullOrWhiteSpace(b) ? "main" : b;
        }

        private static string BasePathLabel()
        {
            var b = BaseDirectory;
            return string.IsNullOrWhiteSpace(b) ? "root" : b;
        }

        private string MetaText(QueryItem it)
        {
            int cur = CountLines(it.FullPath);
            // when edited but not yet submitted, show committed -> current, rendered with the
            // arrow glyph (e.g. "15 → 13 lines")
            string lines = (_modified.Contains(it.RelativePath)
                            && _baseLines.TryGetValue(it.RelativePath, out var b) && b != cur)
                ? $"{b} → {cur} lines"
                : $"{cur} lines";
            var folder = Path.GetDirectoryName(it.FullPath);
            var fileName = Path.GetFileName(it.FullPath);
            var locker = FolderMeta.GetLock(folder, fileName);
            var dep = FolderMeta.GetDeprecation(folder, fileName);
            if (locker != null && dep != null)
                return string.Equals(locker, dep, StringComparison.OrdinalIgnoreCase)
                    ? $"{lines} - locked and deprecated by {locker}"
                    : $"{lines} - locked by {locker}, deprecated by {dep}";
            if (locker != null)
                return $"{lines} - locked by {locker}";
            if (dep != null)
                return $"{lines} - deprecated by {dep}";
            if (_historyMap != null && _historyMap.TryGetValue(it.RelativePath, out var m) && m.LastAuthor != null)
                return $"{lines} - last modified by {m.LastAuthor} ({m.LastDate})";
            return $"{lines} - new";
        }

        private static int CountLines(string path)
        {
            try { return File.ReadAllLines(path).Length; } catch { return 0; }
        }

        private static Geometry ParseGeo(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }

        private static ShapePath Icon(Geometry geo, Brush fill, double size = 14) => new ShapePath
        {
            Data = geo,
            Fill = fill,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>A white text-file icon (page outline + lines), font-independent.</summary>
        private static UIElement FileIcon()
        {
            var g = new Grid { Width = 14, Height = 14 };
            g.Children.Add(new ShapePath { Data = GeoFilePage, Fill = Brushes.White, Stroke = FileLineBrush, StrokeThickness = 1, Stretch = Stretch.None });
            g.Children.Add(new ShapePath { Data = GeoFileLines, Stroke = FileLineBrush, StrokeThickness = 1, Stretch = Stretch.None });
            return new Viewbox { Width = TreeIconSize, Height = TreeIconSize, Child = g, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>The project logo (an embedded PNG) shown on the repository root node, so the
        /// tree's top node matches the Tools-menu icon and the README logo. Falls back to the
        /// database glyph if the resource cannot be loaded.</summary>
        private static UIElement RepoIcon()
        {
            try
            {
                var asm = typeof(QueryPanelControl).Assembly;
                using (var s = asm.GetManifestResourceStream("SsmsSharedQueries.Resources.repo.png"))
                {
                    if (s != null)
                    {
                        var src = System.Windows.Media.Imaging.BitmapFrame.Create(
                            s, System.Windows.Media.Imaging.BitmapCreateOptions.None,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        var image = new Image
                        {
                            Source = src,
                            Width = TreeIconSize,
                            Height = TreeIconSize,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                        };
                        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                        return image;
                    }
                }
            }
            catch (Exception ex) { Diagnostics.Log.Write("RepoIcon load failed", ex); }
            return Icon(GeoDb, DbBrush);
        }

        /// <summary>A folder glyph (the Material "folder" path) filled with the folder's
        /// color, so it reads as a folder in any color instead of a flat colored blob.</summary>
        private static UIElement FolderIcon(Color c) => new ShapePath
        {
            Data = GeoFolderMaterial,
            Fill = Frozen(c),
            Stretch = Stretch.Uniform,
            Width = TreeIconSize,
            Height = TreeIconSize,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        private static Color? ColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return null; }
        }

        private static UIElement SyncIcon(Brush brush)
        {
            var g = new Grid { Width = 15, Height = 15, Margin = new Thickness(1) };
            g.Children.Add(new ShapePath { Data = GeoSyncArc, Stroke = brush, StrokeThickness = 1.7, Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left });
            g.Children.Add(new ShapePath { Data = GeoSyncHead, Fill = brush, Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left });
            return g;
        }

        private Button FlatButton(UIElement content, string tip, RoutedEventHandler onClick)
        {
            var b = new Button { Content = content, ToolTip = tip };
            if (_flatStyle != null) b.Style = _flatStyle;
            else { b.Background = Brushes.Transparent; b.BorderThickness = new Thickness(0); b.Padding = new Thickness(4, 3, 4, 3); }
            b.Click += onClick;
            return b;
        }

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        private static Brush Frozen(Color c)
        {
            var br = new SolidColorBrush(c);
            br.Freeze();
            return br;
        }

        private static void OpenFileInEditor(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, path, Guid.Empty,
                out _, out _, out IVsWindowFrame frame);
            frame?.Show();
        }

        private static GitService CreateGit()
        {
            var o = SharedQueriesPackage.Instance?.Options
                ?? throw new InvalidOperationException("Plugin is not initialized yet.");
            if (string.IsNullOrWhiteSpace(o.RepositoryUrl))
                throw new InvalidOperationException("Set the repository URL in Tools > Options > SSMS Shared Queries.");
            return new GitService(o.RepositoryUrl, o.Branch, o.LocalCachePath);
        }

        private static string BaseDirectory => (SharedQueriesPackage.Instance?.Options?.BaseDirectory ?? string.Empty).Trim();

        private static SqlEditorService GetEditorService()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var tm = ServiceProvider.GlobalProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            return new SqlEditorService(tm);
        }

        private static string MakeRelative(string root, string fullPath)
            => QueryPaths.MakeRelative(root, fullPath);

        private static string SanitizeName(string name)
            => QueryPaths.SanitizeName(name);

        private void Run(string label, Func<Task> action)
        {
            Diagnostics.Log.Write($"Run start: {label}; Instance={(SharedQueriesPackage.Instance == null ? "NULL" : "set")}");
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                SetActionsEnabled(false);
                try
                {
                    await action();
                    Diagnostics.Log.Write($"Run ok: {label}");
                }
                catch (Exception ex)
                {
                    Diagnostics.Log.Write($"Run ERROR: {label}", ex);
                    SetStatus("Error: " + ex.Message);
                    System.Windows.MessageBox.Show(ex.ToString(), "SSMS Shared Queries - error");
                }
                finally
                {
                    SetActionsEnabled(true);
                    await RefreshStateAsync();
                }
            }).FileAndForget("SsmsSharedQueries/panel");
        }

        private void SetActionsEnabled(bool enabled)
        {
            _syncBtn.IsEnabled = enabled;
        }

        private void SetStatus(string text, bool history = true)
        {
            _status.Text = text;
            if (history && !text.EndsWith("...")) AddHistory(text);
        }

        // ---- styles (classic +/- expander, flat toolbar button) ------------
        private const string StylesXaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='ExpandCollapseToggleStyle' TargetType='ToggleButton'>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Width' Value='16'/>
    <Setter Property='Height' Value='16'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToggleButton'>
          <Border Background='Transparent' Width='16' Height='16'>
            <Border Width='9' Height='9' BorderThickness='1' BorderBrush='#888888' Background='White' SnapsToDevicePixels='True'>
              <Grid>
                <Rectangle Width='5' Height='1' Fill='#404040' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                <Rectangle x:Name='V' Width='1' Height='5' Fill='#404040' HorizontalAlignment='Center' VerticalAlignment='Center'/>
              </Grid>
            </Border>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='V' Property='Visibility' Value='Collapsed'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='TreeViewItem'>
    <Setter Property='Padding' Value='2'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TreeViewItem'>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width='16'/>
              <ColumnDefinition Width='*'/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition/>
            </Grid.RowDefinitions>
            <ToggleButton x:Name='Expander' Style='{StaticResource ExpandCollapseToggleStyle}' IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}' ClickMode='Press' VerticalAlignment='Center'/>
            <Border x:Name='Bd' Grid.Column='1' Padding='{TemplateBinding Padding}' Background='{TemplateBinding Background}'>
              <ContentPresenter x:Name='PART_Header' ContentSource='Header' HorizontalAlignment='Stretch'/>
            </Border>
            <ItemsPresenter x:Name='ItemsHost' Grid.Row='1' Grid.Column='1'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsExpanded' Value='False'>
              <Setter TargetName='ItemsHost' Property='Visibility' Value='Collapsed'/>
            </Trigger>
            <Trigger Property='HasItems' Value='False'>
              <Setter TargetName='Expander' Property='Visibility' Value='Hidden'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='#CCE8FF'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='FlatBtn' TargetType='Button'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='3' Padding='4,3,4,3'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='#D8E6F2'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.4'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }
}
