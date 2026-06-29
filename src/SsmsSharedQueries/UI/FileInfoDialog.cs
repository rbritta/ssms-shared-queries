using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SsmsSharedQueries.Git;

namespace SsmsSharedQueries.UI
{
    /// <summary>Summarized history + lock info for a single query file.</summary>
    internal sealed class FileInfoDialog : Window
    {
        public FileInfoDialog(string displayName, int lines, FileMeta meta, string locker, string deprecatedBy, IEnumerable<string> commits)
        {
            Title = "File info";
            Width = 560;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(10) };

            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = displayName, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
            header.Children.Add(Info("Lines", lines.ToString()));
            header.Children.Add(Info("Created", meta?.Creator != null ? $"{meta.Creator}  ({meta.CreateDate})" : "(uncommitted)"));
            header.Children.Add(Info("Last modified", meta?.LastAuthor != null ? $"{meta.LastAuthor}  ({meta.LastDate})" : "(uncommitted)"));
            header.Children.Add(Info("Lock", string.IsNullOrEmpty(locker) ? "unlocked" : $"locked by {locker}"));
            header.Children.Add(Info("Deprecated", string.IsNullOrEmpty(deprecatedBy) ? "no" : $"yes, by {deprecatedBy}"));
            header.Children.Add(new TextBlock { Text = "Recent commits:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);

            var close = new Button { Content = "Close", IsCancel = true, MinWidth = 74, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(6, 2, 6, 2) };
            DockPanel.SetDock(close, Dock.Bottom);
            panel.Children.Add(close);

            var list = new ListBox();
            foreach (var c in commits) list.Items.Add(c);
            if (list.Items.Count == 0) list.Items.Add("(no commits yet for this file)");
            panel.Children.Add(list);

            Content = panel;
        }

        private static TextBlock Info(string key, string value)
            => new TextBlock { Margin = new Thickness(0, 1, 0, 1), Text = $"{key}: {value}" };
    }
}
