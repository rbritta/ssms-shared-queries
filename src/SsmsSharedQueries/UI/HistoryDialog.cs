using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SsmsSharedQueries.UI
{
    /// <summary>Shows the local operation history (most recent first).</summary>
    internal sealed class HistoryDialog : Window
    {
        public HistoryDialog(IEnumerable<string> entries)
        {
            Title = "Operation history (local)";
            Width = 560;
            Height = 420;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(8) };

            var close = new Button { Content = "Close", IsCancel = true, MinWidth = 74, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(6, 2, 6, 2) };
            DockPanel.SetDock(close, Dock.Bottom);
            panel.Children.Add(close);

            var list = new ListBox();
            foreach (var e in entries.Reverse()) list.Items.Add(e);
            if (list.Items.Count == 0) list.Items.Add("(no operations yet)");
            panel.Children.Add(list);

            Content = panel;
        }
    }
}
