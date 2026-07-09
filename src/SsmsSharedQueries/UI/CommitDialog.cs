using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// Confirms a submit: lists the files that will be committed and collects a commit
    /// message. OK is enabled only once a message is typed.
    /// </summary>
    internal sealed class CommitDialog : Window
    {
        private readonly TextBox _message = new TextBox
        {
            Margin = new Thickness(12, 2, 12, 0),
            MinWidth = 460,
            MinHeight = 64,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        public string Message => _message.Text.Trim();

        private readonly string _repoRoot;

        public CommitDialog(IEnumerable<string> files, string repoRoot = null)
        {
            _repoRoot = repoRoot;
            Title = "Submit changes";
            SizeToContent = SizeToContent.Height;
            Width = 520;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 12) };

            panel.Children.Add(new TextBlock { Text = "Files to be committed:", Margin = new Thickness(12, 4, 12, 2), FontWeight = FontWeights.SemiBold });
            var list = new ListBox { Margin = new Thickness(12, 0, 12, 8), Height = 150 };
            foreach (var f in files) list.Items.Add(MakeRow(f));
            panel.Children.Add(list);

            panel.Children.Add(new TextBlock { Text = "Commit message:", Margin = new Thickness(12, 4, 12, 2), FontWeight = FontWeights.SemiBold });
            panel.Children.Add(_message);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 12, 12, 0),
            };
            var ok = new Button { Content = "Commit & Push", IsDefault = true, MinWidth = 110, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 2, 6, 2), IsEnabled = false };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 74, Padding = new Thickness(6, 2, 6, 2) };
            ok.Click += (s, e) => { DialogResult = true; };
            _message.TextChanged += (s, e) => ok.IsEnabled = Message.Length > 0;
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            Content = panel;
            Loaded += (s, e) => _message.Focus();
        }

        // ---- friendly status rows (instead of raw "??", " M", " D" git codes) ----

        private static readonly Brush NewBrush = Frozen(0x2E, 0x8B, 0x57);      // green
        private static readonly Brush ModBrush = Frozen(0xC0, 0x50, 0x4D);      // soft red
        private static readonly Brush DelBrush = Frozen(0x9A, 0x9A, 0x9A);      // gray (going away)
        private static readonly Brush RenBrush = Frozen(0x4F, 0x8A, 0xC0);      // blue

        /// <summary>
        /// Render one porcelain status line as a readable row: a colored word
        /// (new / modified / deleted / renamed, or "folder settings" for the hidden .ssq marker)
        /// followed by the path. The mapping itself lives in <see cref="RowStatusMapper"/>; the repo
        /// root lets it tell a folder deletion from a mere metadata clear.
        /// </summary>
        private UIElement MakeRow(string porcelain)
        {
            Func<string, bool> folderExists = _repoRoot == null
                ? (Func<string, bool>)null
                : rel => Directory.Exists(Path.Combine(_repoRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
            var row = RowStatusMapper.Map(porcelain, folderExists);
            Brush color = row.Kind == RowKind.New ? NewBrush
                        : row.Kind == RowKind.Deleted ? DelBrush
                        : row.Kind == RowKind.Renamed ? RenBrush
                        : ModBrush;

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = row.Label, Width = 92, Foreground = color, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = row.Path });
            return sp;
        }

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }
    }
}
