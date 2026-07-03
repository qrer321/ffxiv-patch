using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FFXIVKoreanPatch.Main
{
    public enum ClientPatchState
    {
        NoClient,
        Clean,
        Full,
        FontOnly,
        Mixed,
        Unreadable
    }

    public enum ViewMessageKind
    {
        Info,
        Warning,
        Error
    }

    // One consistent snapshot of everything the dashboard shows. Computed off
    // the UI thread by the controller, applied on the UI thread by the view.
    public sealed class DashboardState
    {
        public bool ControlsEnabled;
        public bool CanFullPatch;
        public bool CanFontPatch;
        public bool CanRemove;
        public bool CanTestPatch;
        public ClientPatchState State;
        public string StateDetail;
        public string GuardNote;
    }

    // WPF shell for the patcher. The visual tree lives in MainWindow.xaml
    // (embedded resource, parsed at runtime) so the project builds with the
    // plain C# MSBuild pipeline without WPF markup compilation.
    public sealed class MainWindow
    {
        private const string mainXamlResourceName = "FFXIVKoreanPatch.Ui.MainWindow.xaml";
        private const string bannerResourceName = "FFXIVKoreanPatch.Ui.Banner.jpg";
        private const string iconResourceName = "FFXIVKoreanPatch.Ui.App.ico";

        private readonly Window window;
        private PatchController controller;
        private bool startRequested;

        // Named elements resolved from the loaded XAML.
        private readonly Border titleBar;
        private readonly Border modeBadge;
        private readonly Button minButton;
        private readonly Button closeButton;
        private readonly Image bannerImage;
        private readonly Ellipse stateDot;
        private readonly TextBlock stateText;
        private readonly Border stateBadge;
        private readonly TextBlock stateBadgeText;
        private readonly TextBlock globalPathText;
        private readonly Button globalBrowseButton;
        private readonly TextBlock koreaPathText;
        private readonly Button koreaBrowseButton;
        private readonly ComboBox languageCombo;
        private readonly Button detectButton;
        private readonly Button resetButton;
        private readonly ToggleButton chipBnpc;
        private readonly ToggleButton chipAction;
        private readonly ToggleButton chipCommon;
        private readonly Button fullPatchButton;
        private readonly Button fontPatchButton;
        private readonly Button removeButton;
        private readonly Button testPatchButton;
        private readonly Border guardNote;
        private readonly TextBlock guardNoteText;
        private readonly TextBlock preflightIcon;
        private readonly TextBlock preflightSummary;
        private readonly ToggleButton preflightToggle;
        private readonly ToggleButton preflightShowAllToggle;
        private readonly StackPanel preflightPanel;
        private readonly ItemsControl preflightList;
        private readonly TextBlock preflightLogPath;
        private readonly Button preflightLogButton;
        private readonly Button preflightRunButton;
        private readonly ToggleButton advancedToggle;
        private readonly StackPanel advancedPanel;
        private readonly Button restoreBackupButton;
        private readonly Button openReleaseButton;
        private readonly Button openLogsButton;
        private readonly Button cleanupButton;
        private readonly DockPanel fontProfileRow;
        private readonly ComboBox fontProfileCombo;
        private readonly TextBlock footerStatus;
        private readonly TextBlock footerPercent;
        private readonly Button openLastLogButton;
        private readonly Border progressTrack;
        private readonly Border progressFill;
        private readonly TranslateTransform progressMarqueeTransform;

        private readonly Brush statusNormalBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2));
        private readonly Brush statusErrorBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x8A, 0x80));
        private readonly Brush dotFaint = new SolidColorBrush(Color.FromRgb(0x62, 0x6C, 0x7D));
        private readonly Brush dotGreen = new SolidColorBrush(Color.FromRgb(0x7E, 0xC9, 0x8F));
        private readonly Brush dotGold = new SolidColorBrush(Color.FromRgb(0xE8, 0xC8, 0x86));
        private readonly Brush dotBlue = new SolidColorBrush(Color.FromRgb(0x6A, 0xA7, 0xD4));
        private readonly Brush dotAmber = new SolidColorBrush(Color.FromRgb(0xE0, 0xB8, 0x5C));
        private readonly Brush dotRed = new SolidColorBrush(Color.FromRgb(0xD4, 0x74, 0x6A));

        private int lastProgressPercent;
        private bool progressMarqueeActive;
        private string lastPreflightLogPath = string.Empty;
        private bool suppressOptionEvents;
        private List<PreflightItemView> preflightAllItems = new List<PreflightItemView>();
        private List<PreflightItemView> preflightProblemItems = new List<PreflightItemView>();

        private static readonly string[] fontProfileValues = new string[]
        {
            "full", "no-trumpgothic", "ui-numeric-safe", "no-miedingermid", "no-jupiter", "no-axis", "fdt-only", "textures-only"
        };

        private static readonly string[] fontProfileNames = new string[]
        {
            "기본", "TrumpGothic 제외", "UI 숫자 보호", "MiedingerMid 제외", "Jupiter 제외", "AXIS 제외", "FDT만 적용", "텍스처만 적용"
        };

        public MainWindow()
        {
            window = XamlResources.Load<Window>(mainXamlResourceName);

            titleBar = Find<Border>("TitleBar");
            modeBadge = Find<Border>("ModeBadge");
            minButton = Find<Button>("MinButton");
            closeButton = Find<Button>("CloseButton");
            bannerImage = Find<Image>("BannerImage");
            stateDot = Find<Ellipse>("StateDot");
            stateText = Find<TextBlock>("StateText");
            stateBadge = Find<Border>("StateBadge");
            stateBadgeText = Find<TextBlock>("StateBadgeText");
            globalPathText = Find<TextBlock>("GlobalPathText");
            globalBrowseButton = Find<Button>("GlobalBrowseButton");
            koreaPathText = Find<TextBlock>("KoreaPathText");
            koreaBrowseButton = Find<Button>("KoreaBrowseButton");
            languageCombo = Find<ComboBox>("LanguageCombo");
            detectButton = Find<Button>("DetectButton");
            resetButton = Find<Button>("ResetButton");
            chipBnpc = Find<ToggleButton>("ChipBnpc");
            chipAction = Find<ToggleButton>("ChipAction");
            chipCommon = Find<ToggleButton>("ChipCommon");
            fullPatchButton = Find<Button>("FullPatchButton");
            fontPatchButton = Find<Button>("FontPatchButton");
            removeButton = Find<Button>("RemoveButton");
            testPatchButton = Find<Button>("TestPatchButton");
            guardNote = Find<Border>("GuardNote");
            guardNoteText = Find<TextBlock>("GuardNoteText");
            preflightIcon = Find<TextBlock>("PreflightIcon");
            preflightSummary = Find<TextBlock>("PreflightSummary");
            preflightToggle = Find<ToggleButton>("PreflightToggle");
            preflightShowAllToggle = Find<ToggleButton>("PreflightShowAllToggle");
            preflightPanel = Find<StackPanel>("PreflightPanel");
            preflightList = Find<ItemsControl>("PreflightList");
            preflightLogPath = Find<TextBlock>("PreflightLogPath");
            preflightLogButton = Find<Button>("PreflightLogButton");
            preflightRunButton = Find<Button>("PreflightRunButton");
            advancedToggle = Find<ToggleButton>("AdvancedToggle");
            advancedPanel = Find<StackPanel>("AdvancedPanel");
            restoreBackupButton = Find<Button>("RestoreBackupButton");
            openReleaseButton = Find<Button>("OpenReleaseButton");
            openLogsButton = Find<Button>("OpenLogsButton");
            cleanupButton = Find<Button>("CleanupButton");
            fontProfileRow = Find<DockPanel>("FontProfileRow");
            fontProfileCombo = Find<ComboBox>("FontProfileCombo");
            footerStatus = Find<TextBlock>("FooterStatus");
            footerPercent = Find<TextBlock>("FooterPercent");
            openLastLogButton = Find<Button>("OpenLastLogButton");
            progressTrack = Find<Border>("ProgressTrack");
            progressFill = Find<Border>("ProgressFill");
            progressMarqueeTransform = (TranslateTransform)progressFill.RenderTransform;

            LoadBrandingResources();
            InitializeChrome();
            InitializeStaticContent();

            // SizeToContent grows with the checklist; never outgrow the screen.
            window.MaxHeight = Math.Max(560, SystemParameters.WorkArea.Height - 24);
        }

        public Window Window
        {
            get { return window; }
        }

        private T Find<T>(string name) where T : class
        {
            T element = window.FindName(name) as T;
            if (element == null)
            {
                throw new InvalidOperationException("MainWindow.xaml is missing element: " + name);
            }

            return element;
        }

        private void LoadBrandingResources()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(bannerResourceName))
            {
                if (stream != null)
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    bannerImage.Source = bitmap;
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream(iconResourceName))
            {
                if (stream != null)
                {
                    window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }
        }

        private void InitializeChrome()
        {
            titleBar.MouseLeftButtonDown += (sender, args) =>
            {
                if (args.ButtonState == MouseButtonState.Pressed)
                {
                    try { window.DragMove(); }
                    catch (InvalidOperationException) { }
                }
            };
            minButton.Click += (sender, args) => window.WindowState = WindowState.Minimized;
            closeButton.Click += (sender, args) => window.Close();

            preflightToggle.Checked += (sender, args) => preflightPanel.Visibility = Visibility.Visible;
            preflightToggle.Unchecked += (sender, args) => preflightPanel.Visibility = Visibility.Collapsed;
            advancedToggle.Checked += (sender, args) => advancedPanel.Visibility = Visibility.Visible;
            advancedToggle.Unchecked += (sender, args) => advancedPanel.Visibility = Visibility.Collapsed;

            preflightLogButton.Click += (sender, args) => OpenFileInShell(lastPreflightLogPath);
            preflightShowAllToggle.Checked += (sender, args) => RefreshPreflightListSource();
            preflightShowAllToggle.Unchecked += (sender, args) => RefreshPreflightListSource();
            progressTrack.SizeChanged += (sender, args) =>
            {
                if (!progressMarqueeActive)
                {
                    ApplyProgressWidth();
                }
            };
        }

        private void InitializeStaticContent()
        {
            languageCombo.Items.Add("일본어 클라이언트 (ja)");
            languageCombo.Items.Add("영어 클라이언트 (en)");
            languageCombo.SelectedIndex = 0;

            for (int i = 0; i < fontProfileNames.Length; i++)
            {
                fontProfileCombo.Items.Add(fontProfileNames[i]);
            }

            fontProfileCombo.SelectedIndex = 0;

#if TEST_BUILD
            window.Title = "FFXIV 한글 패치 - 테스트 빌드";
            modeBadge.Visibility = Visibility.Visible;
            fontProfileRow.Visibility = Visibility.Visible;
            testPatchButton.Visibility = Visibility.Visible;
            fullPatchButton.Visibility = Visibility.Collapsed;
            fontPatchButton.Visibility = Visibility.Collapsed;
            removeButton.Visibility = Visibility.Collapsed;
#endif
        }

        // Hooks up controller callbacks. Initial option state is copied from the
        // controller first so wiring the change events cannot echo defaults back.
        public void AttachController(PatchController patchController)
        {
            controller = patchController;

            suppressOptionEvents = true;
            languageCombo.SelectedIndex = controller.TargetLanguageIndex;
            chipBnpc.IsChecked = controller.PreserveBaseBnpcNames;
            chipAction.IsChecked = controller.PreserveBaseActionNames;
            chipCommon.IsChecked = controller.PreserveBaseCommonPhrases;
            suppressOptionEvents = false;

            globalBrowseButton.Click += (sender, args) => controller.BrowseGlobalPath();
            koreaBrowseButton.Click += (sender, args) => controller.BrowseKoreaPath();
            detectButton.Click += (sender, args) => controller.DetectPaths();
            resetButton.Click += (sender, args) => controller.ResetPaths();
            languageCombo.SelectionChanged += (sender, args) =>
            {
                if (!suppressOptionEvents)
                {
                    controller.SetTargetLanguage(languageCombo.SelectedIndex);
                }
            };

            RoutedEventHandler chipHandler = (sender, args) =>
            {
                if (!suppressOptionEvents)
                {
                    controller.SetPreserveOptions(
                        chipBnpc.IsChecked == true,
                        chipAction.IsChecked == true,
                        chipCommon.IsChecked == true);
                }
            };
            chipBnpc.Checked += chipHandler;
            chipBnpc.Unchecked += chipHandler;
            chipAction.Checked += chipHandler;
            chipAction.Unchecked += chipHandler;
            chipCommon.Checked += chipHandler;
            chipCommon.Unchecked += chipHandler;

            fullPatchButton.Click += (sender, args) => controller.RequestFullPatch();
            fontPatchButton.Click += (sender, args) => controller.RequestFontPatch();
            removeButton.Click += (sender, args) => controller.RequestRemove();
            testPatchButton.Click += (sender, args) => controller.RequestTestPatch();
            preflightRunButton.Click += (sender, args) => controller.RequestPreflight();
            restoreBackupButton.Click += (sender, args) => controller.RequestRestoreBackup();
            openReleaseButton.Click += (sender, args) => controller.OpenReleaseFolder();
            openLogsButton.Click += (sender, args) => controller.OpenLogsFolder();
            cleanupButton.Click += (sender, args) => controller.RequestCleanup();
            openLastLogButton.Click += (sender, args) => controller.OpenLastLog();
            fontProfileCombo.SelectionChanged += (sender, args) =>
            {
                int index = fontProfileCombo.SelectedIndex;
                controller.SetFontPatchProfile(index >= 0 && index < fontProfileValues.Length ? fontProfileValues[index] : "full");
            };

            window.Loaded += (sender, args) =>
            {
                if (!startRequested)
                {
                    startRequested = true;
                    controller.Start();
                }
            };
        }

        public void RunOnUi(Action action)
        {
            if (window.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                window.Dispatcher.Invoke(action);
            }
        }

        private T RunOnUi<T>(Func<T> function)
        {
            if (window.Dispatcher.CheckAccess())
            {
                return function();
            }

            return (T)window.Dispatcher.Invoke(function);
        }

        public void CloseView()
        {
            RunOnUi(() => window.Close());
        }

        public void SetStatus(string text, bool isError)
        {
            RunOnUi(() =>
            {
                footerStatus.Text = text ?? string.Empty;
                footerStatus.Foreground = isError ? statusErrorBrush : statusNormalBrush;
            });
        }

        public void SetSubStatus(string text)
        {
            RunOnUi(() => footerPercent.Text = text ?? string.Empty);
        }

        public void SetPaths(string globalPath, string koreaPath)
        {
            RunOnUi(() =>
            {
                globalPathText.Text = string.IsNullOrEmpty(globalPath) ? "(미설정)" : globalPath;
                koreaPathText.Text = string.IsNullOrEmpty(koreaPath) ? "(미설정)" : koreaPath;
            });
        }

        public void SetProgressValue(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            RunOnUi(() =>
            {
                StopMarquee();
                lastProgressPercent = percent;
                ApplyProgressWidth();
            });
        }

        public void SetProgressMarquee(bool enabled)
        {
            RunOnUi(() =>
            {
                if (enabled)
                {
                    StartMarquee();
                }
                else
                {
                    StopMarquee();
                    lastProgressPercent = 0;
                    ApplyProgressWidth();
                }
            });
        }

        private void ApplyProgressWidth()
        {
            double trackWidth = progressTrack.ActualWidth;
            progressFill.Width = trackWidth > 0 ? trackWidth * lastProgressPercent / 100d : 0d;
        }

        private void StartMarquee()
        {
            if (progressMarqueeActive)
            {
                return;
            }

            progressMarqueeActive = true;
            progressFill.Width = 150;
            double trackWidth = progressTrack.ActualWidth;
            DoubleAnimation animation = new DoubleAnimation(
                -150,
                trackWidth > 0 ? trackWidth : 700,
                new Duration(TimeSpan.FromSeconds(1.3)));
            animation.RepeatBehavior = RepeatBehavior.Forever;
            progressMarqueeTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void StopMarquee()
        {
            if (!progressMarqueeActive)
            {
                return;
            }

            progressMarqueeActive = false;
            progressMarqueeTransform.BeginAnimation(TranslateTransform.XProperty, null);
            progressMarqueeTransform.X = 0;
        }

        public void ApplyDashboard(DashboardState state)
        {
            RunOnUi(() =>
            {
                globalBrowseButton.IsEnabled = state.ControlsEnabled;
                koreaBrowseButton.IsEnabled = state.ControlsEnabled;
                languageCombo.IsEnabled = state.ControlsEnabled;
                detectButton.IsEnabled = state.ControlsEnabled;
                resetButton.IsEnabled = state.ControlsEnabled;
                chipBnpc.IsEnabled = state.ControlsEnabled;
                chipAction.IsEnabled = state.ControlsEnabled;
                chipCommon.IsEnabled = state.ControlsEnabled;
                restoreBackupButton.IsEnabled = state.ControlsEnabled;
                openReleaseButton.IsEnabled = state.ControlsEnabled;
                openLogsButton.IsEnabled = state.ControlsEnabled;
                cleanupButton.IsEnabled = state.ControlsEnabled;
                preflightRunButton.IsEnabled = state.ControlsEnabled;
                fontProfileCombo.IsEnabled = state.ControlsEnabled;

                fullPatchButton.IsEnabled = state.CanFullPatch;
                fontPatchButton.IsEnabled = state.CanFontPatch;
                removeButton.IsEnabled = state.CanRemove;
                testPatchButton.IsEnabled = state.CanTestPatch;

                ApplyStateHeader(state.State, state.StateDetail);

                if (string.IsNullOrEmpty(state.GuardNote))
                {
                    guardNote.Visibility = Visibility.Collapsed;
                }
                else
                {
                    guardNoteText.Text = state.GuardNote;
                    guardNote.Visibility = Visibility.Visible;
                }
            });
        }

        private void ApplyStateHeader(ClientPatchState state, string detail)
        {
            string text;
            Brush brush;
            switch (state)
            {
                case ClientPatchState.Clean:
                    text = "패치 없음 — 원본 상태";
                    brush = dotGreen;
                    break;
                case ClientPatchState.Full:
                    text = "전체 한글 패치 적용됨";
                    brush = dotGold;
                    break;
                case ClientPatchState.FontOnly:
                    text = "폰트만 패치 적용됨";
                    brush = dotBlue;
                    break;
                case ClientPatchState.Mixed:
                    text = "혼합 패치 상태 — 패치 제거 권장";
                    brush = dotAmber;
                    break;
                case ClientPatchState.Unreadable:
                    text = "클라이언트 상태 확인 불가";
                    brush = dotRed;
                    break;
                default:
                    text = "글로벌 클라이언트 경로를 설정해주세요";
                    brush = dotFaint;
                    break;
            }

            stateText.Text = text;
            stateDot.Fill = brush;

            if (string.IsNullOrEmpty(detail))
            {
                stateBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                stateBadgeText.Text = detail;
                stateBadge.Visibility = Visibility.Visible;
            }
        }

        public void SetPreflightRunning()
        {
            RunOnUi(() =>
            {
                preflightIcon.Text = "●";
                preflightIcon.Foreground = dotAmber;
                preflightSummary.Text = "사전 점검 중...";
            });
        }

        public void SetPreflightResult(string summary, int failures, int warnings, string logPath, IList<string> lines)
        {
            RunOnUi(() =>
            {
                lastPreflightLogPath = logPath ?? string.Empty;

                if (failures > 0)
                {
                    preflightIcon.Text = "✕";
                    preflightIcon.Foreground = dotRed;
                }
                else if (warnings > 0)
                {
                    preflightIcon.Text = "⚠";
                    preflightIcon.Foreground = dotAmber;
                }
                else
                {
                    preflightIcon.Text = "✓";
                    preflightIcon.Foreground = dotGreen;
                }

                int total = lines == null ? 0 : lines.Count;
                preflightSummary.Text = summary + "  ·  실패 " + failures + " / 주의 " + warnings + " / 전체 " + total;
                preflightLogPath.Text = string.IsNullOrEmpty(logPath) ? string.Empty : "로그: " + logPath;

                preflightAllItems = new List<PreflightItemView>();
                foreach (string rawLine in lines ?? new List<string>())
                {
                    preflightAllItems.Add(PreflightItemView.Parse(rawLine, dotGreen, dotAmber, dotRed, statusNormalBrush));
                }

                // Default view shows only actionable rows; the full pass sits
                // behind the "전체 항목 보기" toggle to keep the card short.
                preflightProblemItems = preflightAllItems.Where(item => item.Icon != "✓").ToList();
                if (preflightProblemItems.Count == 0)
                {
                    PreflightItemView okItem = new PreflightItemView();
                    okItem.Icon = "✓";
                    okItem.IconBrush = dotGreen;
                    okItem.Text = "모든 항목 통과 — 문제가 발견되지 않았습니다 (" + total + "건 점검)";
                    preflightProblemItems.Add(okItem);
                }

                preflightShowAllToggle.Content = "전체 항목 보기 (" + total + ")";
                suppressOptionEvents = true;
                preflightShowAllToggle.IsChecked = false;
                suppressOptionEvents = false;
                RefreshPreflightListSource();

                // Surface problems without forcing the user to dig; keep the
                // clean pass collapsed so the dashboard stays compact.
                preflightToggle.IsChecked = failures > 0 || warnings > 0;
            });
        }

        private void RefreshPreflightListSource()
        {
            if (suppressOptionEvents)
            {
                return;
            }

            preflightList.ItemsSource = preflightShowAllToggle.IsChecked == true
                ? preflightAllItems
                : preflightProblemItems;
        }

        public void ShowMessage(ViewMessageKind kind, params string[] lines)
        {
            RunOnUi(() => Dialogs.ShowMessage(GetDialogOwner(), kind, JoinLines(lines)));
        }

        public bool ShowConfirm(string title, params string[] lines)
        {
            return RunOnUi(() => Dialogs.ShowConfirm(GetDialogOwner(), title, JoinLines(lines)));
        }

        public void ShowOperationResult(string title, string summary, string logPath, IList<KeyValuePair<string, string>> details)
        {
            RunOnUi(() => Dialogs.ShowOperationResult(GetDialogOwner(), title, summary, logPath, details));
        }

        public string SelectGameExecutablePath(string title)
        {
            return RunOnUi(() =>
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.DefaultExt = "exe";
                dialog.Filter = "FFXIV|ffxiv_dx11.exe";
                dialog.Multiselect = false;
                dialog.Title = title;

                bool? result = window.IsLoaded ? dialog.ShowDialog(window) : dialog.ShowDialog();
                return result == true ? dialog.FileName : null;
            });
        }

        public string PickBackupDirectory(IList<string> candidates)
        {
            return RunOnUi(() => Dialogs.PickFromList(
                GetDialogOwner(),
                "백업 선택",
                "되돌릴 백업 폴더를 선택해주세요. 최근 백업이 위에 표시됩니다.",
                candidates,
                "복구"));
        }

        private Window GetDialogOwner()
        {
            return window.IsLoaded ? window : null;
        }

        private static string JoinLines(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        private static void OpenFileInShell(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch
            {
                // Opening a log is best-effort; the path stays visible in the UI.
            }
        }

        public sealed class PreflightItemView
        {
            public string Icon { get; set; }
            public Brush IconBrush { get; set; }
            public string Text { get; set; }

            public static PreflightItemView Parse(string rawLine, Brush ok, Brush warn, Brush fail, Brush info)
            {
                string text = rawLine ?? string.Empty;
                PreflightItemView item = new PreflightItemView();
                item.Icon = "·";
                item.IconBrush = info;

                if (text.StartsWith("[OK]", StringComparison.OrdinalIgnoreCase))
                {
                    item.Icon = "✓";
                    item.IconBrush = ok;
                    text = text.Substring(4).Trim();
                }
                else if (text.StartsWith("[주의]", StringComparison.Ordinal))
                {
                    item.Icon = "⚠";
                    item.IconBrush = warn;
                    text = text.Substring(4).Trim();
                }
                else if (text.StartsWith("[실패]", StringComparison.Ordinal))
                {
                    item.Icon = "✕";
                    item.IconBrush = fail;
                    text = text.Substring(4).Trim();
                }

                item.Text = text;
                return item;
            }
        }
    }

    internal static class XamlResources
    {
        public static T Load<T>(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Embedded XAML resource not found: " + resourceName);
                }

                return (T)XamlReader.Load(stream);
            }
        }
    }
}
