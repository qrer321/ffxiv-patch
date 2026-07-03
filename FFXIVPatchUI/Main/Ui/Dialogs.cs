using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FFXIVKoreanPatch.Main
{
    // Styled replacements for the old WinForms MessageBox / result dialogs.
    // All entry points must run on the UI thread (MainWindow marshals).
    internal static class Dialogs
    {
        private const string dialogXamlResourceName = "FFXIVKoreanPatch.Ui.DialogWindow.xaml";

        private sealed class DialogShell
        {
            public Window Window;
            public Rectangle AccentBar;
            public TextBlock Title;
            public TextBlock Message;
            public ItemsControl Details;
            public ListBox List;
            public DockPanel LogRow;
            public TextBlock LogPath;
            public Button LogButton;
            public Button PrimaryButton;
            public Button SecondaryButton;
            public bool Accepted;
        }

        private static DialogShell CreateShell(Window owner)
        {
            DialogShell shell = new DialogShell();
            shell.Window = XamlResources.Load<Window>(dialogXamlResourceName);
            shell.AccentBar = (Rectangle)shell.Window.FindName("AccentBar");
            shell.Title = (TextBlock)shell.Window.FindName("DialogTitle");
            shell.Message = (TextBlock)shell.Window.FindName("DialogMessage");
            shell.Details = (ItemsControl)shell.Window.FindName("DialogDetails");
            shell.List = (ListBox)shell.Window.FindName("DialogList");
            shell.LogRow = (DockPanel)shell.Window.FindName("DialogLogRow");
            shell.LogPath = (TextBlock)shell.Window.FindName("DialogLogPath");
            shell.LogButton = (Button)shell.Window.FindName("DialogLogButton");
            shell.PrimaryButton = (Button)shell.Window.FindName("DialogPrimaryButton");
            shell.SecondaryButton = (Button)shell.Window.FindName("DialogSecondaryButton");

            if (owner != null)
            {
                shell.Window.Owner = owner;
                shell.Window.Icon = owner.Icon;
            }
            else
            {
                shell.Window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                shell.Window.ShowInTaskbar = true;
            }

            Border header = (Border)shell.Window.FindName("DialogHeader");
            header.MouseLeftButtonDown += (sender, args) =>
            {
                if (args.ButtonState == MouseButtonState.Pressed)
                {
                    try { shell.Window.DragMove(); }
                    catch (InvalidOperationException) { }
                }
            };

            shell.PrimaryButton.Click += (sender, args) =>
            {
                shell.Accepted = true;
                shell.Window.Close();
            };
            shell.SecondaryButton.Click += (sender, args) => shell.Window.Close();
            shell.Window.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    shell.Window.Close();
                }
            };

            return shell;
        }

        private static Brush AccentFor(ViewMessageKind kind)
        {
            switch (kind)
            {
                case ViewMessageKind.Error:
                    return new SolidColorBrush(Color.FromRgb(0xD4, 0x74, 0x6A));
                case ViewMessageKind.Warning:
                    return new SolidColorBrush(Color.FromRgb(0xE0, 0xB8, 0x5C));
                default:
                    return new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x6A));
            }
        }

        private static string TitleFor(ViewMessageKind kind)
        {
            switch (kind)
            {
                case ViewMessageKind.Error:
                    return "오류";
                case ViewMessageKind.Warning:
                    return "주의";
                default:
                    return "알림";
            }
        }

        public static void ShowMessage(Window owner, ViewMessageKind kind, string message)
        {
            DialogShell shell = CreateShell(owner);
            shell.Title.Text = TitleFor(kind);
            shell.AccentBar.Fill = AccentFor(kind);
            shell.Message.Text = message ?? string.Empty;
            shell.Window.ShowDialog();
        }

        public static bool ShowConfirm(Window owner, string title, string message)
        {
            DialogShell shell = CreateShell(owner);
            shell.Title.Text = string.IsNullOrEmpty(title) ? "확인" : title;
            shell.AccentBar.Fill = AccentFor(ViewMessageKind.Warning);
            shell.Message.Text = message ?? string.Empty;
            shell.PrimaryButton.Content = "계속";
            shell.SecondaryButton.Content = "취소";
            shell.SecondaryButton.Visibility = Visibility.Visible;
            shell.SecondaryButton.Focus();
            shell.Window.ShowDialog();
            return shell.Accepted;
        }

        public static void ShowOperationResult(
            Window owner,
            string title,
            string summary,
            string logPath,
            IList<KeyValuePair<string, string>> details)
        {
            DialogShell shell = CreateShell(owner);
            shell.Title.Text = title ?? "작업 완료";
            shell.AccentBar.Fill = new SolidColorBrush(Color.FromRgb(0x7E, 0xC9, 0x8F));
            shell.Message.Text = summary ?? string.Empty;

            if (details != null && details.Count > 0)
            {
                shell.Details.ItemsSource = details;
                shell.Details.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(logPath))
            {
                shell.LogRow.Visibility = Visibility.Visible;
                shell.LogPath.Text = "로그: " + logPath;
                shell.LogButton.Click += (sender, args) =>
                {
                    try
                    {
                        if (File.Exists(logPath))
                        {
                            Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
                        }
                    }
                    catch
                    {
                        // Best-effort; the log path stays visible in the dialog.
                    }
                };
            }

            shell.Window.ShowDialog();
        }

        public static string PickFromList(Window owner, string title, string message, IList<string> items, string confirmText)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            DialogShell shell = CreateShell(owner);
            shell.Title.Text = title ?? "선택";
            shell.Message.Text = message ?? string.Empty;
            shell.PrimaryButton.Content = string.IsNullOrEmpty(confirmText) ? "선택" : confirmText;
            shell.SecondaryButton.Content = "취소";
            shell.SecondaryButton.Visibility = Visibility.Visible;
            shell.List.Visibility = Visibility.Visible;

            foreach (string item in items)
            {
                shell.List.Items.Add(item);
            }

            shell.List.SelectedIndex = 0;
            shell.List.MouseDoubleClick += (sender, args) =>
            {
                if (shell.List.SelectedItem != null)
                {
                    shell.Accepted = true;
                    shell.Window.Close();
                }
            };

            shell.Window.ShowDialog();
            return shell.Accepted ? shell.List.SelectedItem as string : null;
        }
    }
}
