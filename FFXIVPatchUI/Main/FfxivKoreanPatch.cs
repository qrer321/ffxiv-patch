using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFXIVKoreanPatch.Main
{
    public partial class FFXIVKoreanPatch : Form
    {
        #region Variables

        // Github release server URL that hosts distributed patch files.
        private const string serverUrlBase = "https://github.com/korean-patch/ffxiv-korean-patch/releases/download/";
        private const string patchDisplayName = "FFXIV 글로벌 클라이언트용 한글 패치";
        private const string builderProgressPrefix = "@@FFXIVPATCHGENERATOR_PROGRESS|";

        // Path for the main patch program.
        private string mainPath = string.Empty;
        private const string mainFileName = "FFXIVKoreanPatch";
        private string mainTempPath = string.Empty;

        // Path for the updater program.
        private string updaterPath = string.Empty;
        private const string updaterFileName = "FFXIVKoreanPatchUpdater";

        // Process names to check for before doing the patch.
        private string[] gameProcessNames = new string[]
        {
            "ffxivboot", "ffxivboot64",
            "ffxivlauncher", "ffxivlauncher64",
            "ffxiv", "ffxiv_dx11"
        };

        // List of known files that will be used to verify installation path.
        private string[] requiredFiles = new string[]
        {
            "ffxiv_dx11.exe",
            "ffxivgame.ver",
            "sqpack/ffxiv/000000.win32.index",
            "sqpack/ffxiv/000000.win32.dat0",
            "sqpack/ffxiv/0a0000.win32.index",
            "sqpack/ffxiv/0a0000.win32.dat0"
        };

        private string[] knownGameDirLocations = new string[]
        {
            "SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "Program Files/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "SteamLibrary/steamapps/common/FINAL FANTASY XIV Online/game",
            "Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game",
            "Program Files/Steam/steamapps/common/FINAL FANTASY XIV Online/game"
        };

        private string[] knownKoreaGameDirLocations = new string[]
        {
            "FINAL FANTASY XIV - KOREA/game",
            "Program Files (x86)/FINAL FANTASY XIV - KOREA/game",
            "Program Files/FINAL FANTASY XIV - KOREA/game",
            "SquareEnix/FINAL FANTASY XIV - KOREA/game",
            "Program Files (x86)/SquareEnix/FINAL FANTASY XIV - KOREA/game",
            "Program Files/SquareEnix/FINAL FANTASY XIV - KOREA/game"
        };

        // List of file names that need to be manipulated.
        private string[] fontPatchFiles = new string[]
        {
            "000000.win32.dat1",
            "000000.win32.index"
        };

        private string[] textPatchFiles = new string[]
        {
            "0a0000.win32.dat1",
            "0a0000.win32.index"
        };

        private string[] restoreFiles = new string[]
        {
            "000000.win32.index",
            "0a0000.win32.index"
        };

        // Scancode Map value for registry.
        private byte[] scancodeMap = new byte[]
        {
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x72, 0x00, 0x38, 0xe0,
            0x00, 0x00, 0x00, 0x00
        };

        // Name of the version file that denotes target game client version for the patch.
        private const string versionFileName = "ffxivgame.ver";

        // Target client directory.
        private string targetDir = string.Empty;

        // Korean client directory used when generating release files locally.
        private string koreaSourceDir = string.Empty;

        // Global client language slot to replace when generating release files locally.
        private string targetLanguageCode = "ja";
        private string targetLanguageDisplayName = "일본어";

        // Output directory used when generating release files locally.
        private string releaseOutputDir = string.Empty;

        // Last operation log written by the UI.
        private string lastLogPath = string.Empty;

        // If true, generated files are copied into a local sandbox instead of the real global client.
        private bool useDebugApplyPath;
        private bool buildTextPatch = true;
        private bool buildFontPatch = true;

        // Target client version.
        private string targetVersion = string.Empty;

        // Patch version.
        private string serverVersion = string.Empty;

        // Whether the remote release assets are available for direct download/install buttons.
        private bool serverPatchAvailable;

        #endregion

        public FFXIVKoreanPatch()
        {
            InitializeComponent();

            Text = patchDisplayName;
            debugBuildReleaseButton.Text = "테스트 자동 패치 (원본 변경 없음)";
            buildReleaseButton.Text = "한국 서버 클라이언트로 자동 패치";
            preflightCheckButton.Text = "사전 점검";
            installButton.Text = "전체 한글 패치";
            chatOnlyInstallButton.Text = "한글 폰트 패치";
            removeButton.Text = "한글 패치 제거";
            targetLanguageComboBox.Items.Clear();
            targetLanguageComboBox.Items.Add("일본어 클라이언트 (ja)");
            targetLanguageComboBox.Items.Add("영어 클라이언트 (en)");
            targetLanguageComboBox.SelectedIndex = 0;

#if TEST_BUILD
            Text = patchDisplayName + " - 테스트 빌드";
            buildReleaseButton.Visible = false;
            installButton.Visible = false;
            chatOnlyInstallButton.Visible = false;
            removeButton.Visible = false;
#else
            debugBuildReleaseButton.Visible = false;
            buildReleaseButton.Visible = false;
            detectPathsButton.Visible = false;
            resetPathsButton.Visible = false;
#endif

            ApplyModernLayout();

            // Adjust the background to apply gradient effect.
            AdjustBackground();

            // Empty the labels.
            statusLabel.Text = "";
            downloadLabel.Text = "";

            // Run the initial checker to verify and set up environment.
            initialChecker.RunWorkerAsync();
        }

        #region Functions

        // Grab the background from the form and apply gradient effect.
        private void AdjustBackground()
        {
            // Get the background image as Bitmap first.
            Bitmap origImage = (Bitmap)BackgroundImage;

            // Create a new image that will be used as a new background.
            // This should have the same width as the form, and the same width-height ratio.
            Bitmap newImage = new Bitmap(ClientSize.Width, ClientSize.Width * origImage.Height / origImage.Width);
            newImage.SetResolution(origImage.HorizontalResolution, origImage.VerticalResolution);

            // Starting drawing in the new image...
            using (Graphics g = Graphics.FromImage(newImage))
            {
                // Prepare a rectangle to copy over the original image.
                Rectangle rect = new Rectangle(0, 0, newImage.Width, newImage.Height);

                // Set Graphics parameters...
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Copy over the original image.
                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(origImage, rect, 0, 0, origImage.Width, origImage.Height, GraphicsUnit.Pixel, wrapMode);
                }

                // Prepare a linear gradient brush that is transparent on top and form back color at the bottom.
                LinearGradientBrush brush = new LinearGradientBrush(rect, Color.Transparent, BackColor, 90f);

                // Draw on top of the original image.
                g.FillRectangle(brush, rect);
            }

            // Set the new image as background.
            BackgroundImage = newImage;
        }

        private Label CreateLayoutLabel(string name, string text, int x, int y, int width, int height, Font font, Color color)
        {
            Label label = new Label();
            label.Name = name;
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Font = font;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.TabStop = false;
            Controls.Add(label);
            label.BringToFront();
            return label;
        }

        private void StyleFieldLabel(Label label)
        {
            label.BackColor = Color.Transparent;
            label.ForeColor = Color.FromArgb(198, 205, 214);
            label.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
        }

        private void StyleTextBox(TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.FromArgb(30, 34, 40);
            textBox.ForeColor = Color.FromArgb(242, 245, 248);
            textBox.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
        }

        private void StyleComboBox(ComboBox comboBox)
        {
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = Color.FromArgb(30, 34, 40);
            comboBox.ForeColor = Color.FromArgb(242, 245, 248);
            comboBox.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
        }

        private void StyleButton(Button button, Color backColor, Color borderColor, Color hoverColor)
        {
            button.AutoSize = false;
            button.Dock = DockStyle.None;
            button.BackColor = backColor;
            button.ForeColor = Color.FromArgb(246, 248, 250);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor);
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            button.Padding = new Padding(0);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
        }

        private void PlaceControl(Control control, int x, int y, int width, int height)
        {
            control.Location = new Point(x, y);
            control.Size = new Size(width, height);
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.BringToFront();
        }

        private void ApplyModernLayout()
        {
            SuspendLayout();

            ClientSize = new Size(760, 980);
            BackColor = Color.FromArgb(18, 21, 26);
            ForeColor = Color.FromArgb(240, 243, 247);
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

            Font titleFont = new Font("맑은 고딕", 18F, FontStyle.Bold, GraphicsUnit.Point, 129);
            Font sectionFont = new Font("맑은 고딕", 10F, FontStyle.Bold, GraphicsUnit.Point, 129);
            Font smallFont = new Font("맑은 고딕", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 129);

            int contentTop = 330;

            CreateLayoutLabel("titleLabel", "FFXIV 한글 패치", 24, contentTop, 420, 36, titleFont, Color.White);
            CreateLayoutLabel(
                "modeLabel",
#if TEST_BUILD
                "TEST BUILD",
#else
                "RELEASE",
#endif
                646,
                contentTop + 8,
                90,
                26,
                smallFont,
#if TEST_BUILD
                Color.FromArgb(255, 202, 113)
#else
                Color.FromArgb(125, 220, 151)
#endif
                );
            CreateLayoutLabel("pathsSectionLabel", "클라이언트 경로", 24, contentTop + 60, 180, 24, sectionFont, Color.FromArgb(230, 235, 241));
            CreateLayoutLabel("manageSectionLabel", "관리", 24, contentTop + 266, 180, 24, sectionFont, Color.FromArgb(230, 235, 241));
            CreateLayoutLabel("actionsSectionLabel", "작업", 24, contentTop + 336, 180, 24, sectionFont, Color.FromArgb(230, 235, 241));

            StyleFieldLabel(globalPathLabel);
            StyleFieldLabel(koreaPathLabel);
            StyleFieldLabel(targetLanguageLabel);
            StyleTextBox(globalPathTextBox);
            StyleTextBox(koreaPathTextBox);
            StyleComboBox(targetLanguageComboBox);

            Color neutral = Color.FromArgb(44, 50, 58);
            Color neutralBorder = Color.FromArgb(77, 86, 98);
            Color neutralHover = Color.FromArgb(58, 67, 78);
            Color primary = Color.FromArgb(30, 116, 176);
            Color primaryBorder = Color.FromArgb(73, 152, 208);
            Color primaryHover = Color.FromArgb(38, 134, 202);
            Color success = Color.FromArgb(31, 132, 88);
            Color successBorder = Color.FromArgb(73, 174, 123);
            Color successHover = Color.FromArgb(38, 150, 101);
            Color caution = Color.FromArgb(143, 106, 35);
            Color cautionBorder = Color.FromArgb(198, 154, 63);
            Color cautionHover = Color.FromArgb(165, 124, 43);
            Color danger = Color.FromArgb(137, 55, 55);
            Color dangerBorder = Color.FromArgb(194, 91, 91);
            Color dangerHover = Color.FromArgb(158, 65, 65);

            foreach (Button button in new Button[]
            {
                globalPathBrowseButton,
                koreaPathBrowseButton,
                detectPathsButton,
                resetPathsButton,
                openReleaseButton,
                openLogsButton,
                cleanupButton,
                preflightCheckButton
            })
            {
                StyleButton(button, neutral, neutralBorder, neutralHover);
            }

            StyleButton(restoreBackupButton, caution, cautionBorder, cautionHover);
            StyleButton(debugBuildReleaseButton, success, successBorder, successHover);
            StyleButton(buildReleaseButton, primary, primaryBorder, primaryHover);
            StyleButton(installButton, primary, primaryBorder, primaryHover);
            StyleButton(chatOnlyInstallButton, neutral, neutralBorder, neutralHover);
            StyleButton(removeButton, danger, dangerBorder, dangerHover);

            PlaceControl(globalPathLabel, 24, contentTop + 90, 220, 20);
            PlaceControl(globalPathTextBox, 24, contentTop + 113, 610, 26);
            PlaceControl(globalPathBrowseButton, 646, contentTop + 112, 90, 28);

            PlaceControl(koreaPathLabel, 24, contentTop + 153, 220, 20);
            PlaceControl(koreaPathTextBox, 24, contentTop + 176, 610, 26);
            PlaceControl(koreaPathBrowseButton, 646, contentTop + 175, 90, 28);

            PlaceControl(targetLanguageLabel, 24, contentTop + 216, 220, 20);
            PlaceControl(targetLanguageComboBox, 24, contentTop + 239, 230, 28);
            PlaceControl(detectPathsButton, 270, contentTop + 238, 172, 30);
            PlaceControl(resetPathsButton, 454, contentTop + 238, 140, 30);

            PlaceControl(openReleaseButton, 24, contentTop + 294, 170, 32);
            PlaceControl(openLogsButton, 204, contentTop + 294, 160, 32);
            PlaceControl(cleanupButton, 374, contentTop + 294, 170, 32);
            PlaceControl(restoreBackupButton, 554, contentTop + 294, 182, 32);

#if TEST_BUILD
            PlaceControl(preflightCheckButton, 24, contentTop + 368, 350, 44);
            PlaceControl(debugBuildReleaseButton, 386, contentTop + 368, 350, 44);
#else
            PlaceControl(preflightCheckButton, 24, contentTop + 368, 712, 42);
            PlaceControl(installButton, 24, contentTop + 422, 226, 42);
            PlaceControl(chatOnlyInstallButton, 267, contentTop + 422, 226, 42);
            PlaceControl(removeButton, 510, contentTop + 422, 226, 42);
#endif

            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.None;
            statusLabel.BackColor = Color.FromArgb(24, 28, 34);
            statusLabel.BorderStyle = BorderStyle.FixedSingle;
            statusLabel.ForeColor = Color.White;
            statusLabel.Padding = new Padding(12, 0, 12, 0);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            PlaceControl(statusLabel, 24, contentTop + 548, 712, 38);

            downloadLabel.AutoSize = false;
            downloadLabel.Dock = DockStyle.None;
            downloadLabel.BackColor = Color.Transparent;
            downloadLabel.ForeColor = Color.FromArgb(198, 205, 214);
            downloadLabel.Padding = new Padding(0);
            downloadLabel.TextAlign = ContentAlignment.MiddleLeft;
            PlaceControl(downloadLabel, 24, contentTop + 592, 712, 24);

            progressBar.Dock = DockStyle.None;
            PlaceControl(progressBar, 24, contentTop + 621, 712, 10);

            ResumeLayout(false);
        }

        // Display message box from UI thread.
        private DialogResult ShowMessageBox(MessageBoxButtons buttons, MessageBoxIcon icon, params string[] lines)
        {
            // Compile a single string from given text lines.
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(lines[i]);

                // Append 2 new lines in between...
                if (i != lines.Length - 1)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(Environment.NewLine);
                }
            }

            // Display message box on UI thread with given parameters.
            return (DialogResult)Invoke(new Func<DialogResult>(() =>
            {
                return MessageBox.Show(sb.ToString(), Text, buttons, icon);
            }));
        }

        // Update status label text always from UI thread.
        private void UpdateStatusLabel(string text, bool isRed = false)
        {
            Invoke(new Action(() =>
            {
                statusLabel.Text = text;
                statusLabel.ForeColor = isRed ? Color.Red : Color.White;
            }));
        }

        // Update download label text always from UI thread.
        private void UpdateDownloadLabel(string text)
        {
            Invoke(new Action(() =>
            {
                downloadLabel.Text = text;
            }));
        }

        private void UpdatePathTextBoxes()
        {
            Action action = () =>
            {
                globalPathTextBox.Text = string.IsNullOrEmpty(targetDir) ? "(미설정)" : targetDir;
                koreaPathTextBox.Text = string.IsNullOrEmpty(koreaSourceDir) ? "(미설정)" : koreaSourceDir;
            };

            if (InvokeRequired) Invoke(action);
            else action();
        }

        private void SetPathBrowseButtonsEnabled(bool enabled)
        {
            Action action = () =>
            {
                globalPathBrowseButton.Enabled = enabled;
                koreaPathBrowseButton.Enabled = enabled;
                targetLanguageComboBox.Enabled = enabled;
                detectPathsButton.Enabled = enabled;
                resetPathsButton.Enabled = enabled;
                openReleaseButton.Enabled = enabled;
                openLogsButton.Enabled = enabled;
                cleanupButton.Enabled = enabled;
#if TEST_BUILD
                restoreBackupButton.Enabled = enabled;
#else
                restoreBackupButton.Enabled = enabled;
#endif
            };

            if (InvokeRequired) Invoke(action);
            else action();
        }

        // Close the form always from UI thread.
        private void CloseForm()
        {
            Invoke(new Action(() =>
            {
                Close();
            }));
        }

        // Validates the target client directory by checking if required files are present.
        // Return the directory back if valid, else return null.
        private string CheckTargetDir(string targetDir)
        {
            if (!Directory.Exists(targetDir)) return null;
            if (!requiredFiles.All(requiredFile => File.Exists(Path.Combine(targetDir, requiredFile)))) return null;

            return targetDir;
        }

        private string CleanRegistryPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return null;

            string path = Environment.ExpandEnvironmentVariables(rawPath.Trim());

            if (path.StartsWith("\""))
            {
                int quoteEnd = path.IndexOf('"', 1);
                if (quoteEnd > 1)
                {
                    path = path.Substring(1, quoteEnd - 1);
                }
            }
            else
            {
                int commaIndex = path.IndexOf(',');
                if (commaIndex > 0)
                {
                    path = path.Substring(0, commaIndex);
                }
            }

            return path.Trim().Trim('"');
        }

        private IEnumerable<string> GetCandidateTargetDirs(string rawPath)
        {
            string candidatePath = CleanRegistryPath(rawPath);
            if (string.IsNullOrEmpty(candidatePath)) yield break;

            if (string.Equals(Path.GetFileName(candidatePath), "ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
            {
                candidatePath = Path.GetDirectoryName(candidatePath);
            }

            if (!string.IsNullOrEmpty(candidatePath))
            {
                yield return candidatePath;
                yield return Path.Combine(candidatePath, "game");

                string candidateDir = Path.HasExtension(candidatePath) ? Path.GetDirectoryName(candidatePath) : candidatePath;
                if (!string.IsNullOrEmpty(candidateDir))
                {
                    yield return Path.GetFullPath(Path.Combine(candidateDir, "game"));
                    yield return Path.GetFullPath(Path.Combine(candidateDir, "../game"));
                }
            }
        }

        private bool TryUseTargetDir(string rawPath)
        {
            foreach (string candidateDir in GetCandidateTargetDirs(rawPath))
            {
                string checkedDir = CheckTargetDir(candidateDir);
                if (!string.IsNullOrEmpty(checkedDir))
                {
                    targetDir = checkedDir;
                    return true;
                }
            }

            return false;
        }

        private bool TryUseKoreaSourceDir(string rawPath)
        {
            foreach (string candidateDir in GetCandidateTargetDirs(rawPath))
            {
                string checkedDir = CheckTargetDir(candidateDir);
                if (!string.IsNullOrEmpty(checkedDir))
                {
                    koreaSourceDir = checkedDir;
                    return true;
                }
            }

            return false;
        }

        private bool IsGlobalClientDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            if (displayName.IndexOf("KOREA", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return displayName.Equals("FINAL FANTASY XIV ONLINE", StringComparison.OrdinalIgnoreCase)
                || displayName.IndexOf("FINAL FANTASY XIV", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf("FFXIV", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsKoreaClientDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;

            return displayName.IndexOf("KOREA", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf("한국", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf("한글", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<string> GetKnownGameDirs()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                foreach (string relativePath in knownGameDirLocations)
                {
                    yield return Path.Combine(drive.RootDirectory.FullName, relativePath);
                }
            }
        }

        private IEnumerable<string> GetKnownKoreaGameDirs()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                foreach (string relativePath in knownKoreaGameDirLocations)
                {
                    yield return Path.Combine(drive.RootDirectory.FullName, relativePath);
                }
            }
        }

        private void DetectKoreaClient()
        {
            if (!string.IsNullOrEmpty(koreaSourceDir)) return;

            string[] uninstallKeyNames = new string[]
            {
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
            };

            try
            {
                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32))
                {
                    foreach (string uninstallKeyName in uninstallKeyNames)
                    {
                        if (!string.IsNullOrEmpty(koreaSourceDir)) break;

                        using (RegistryKey uninstallKey = localMachine.OpenSubKey(uninstallKeyName))
                        {
                            if (uninstallKey == null) continue;

                            foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                            {
                                if (!string.IsNullOrEmpty(koreaSourceDir)) break;

                                using (RegistryKey subKey = uninstallKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;

                                    object displayName = subKey.GetValue("DisplayName");
                                    if (displayName == null || !IsKoreaClientDisplayName(displayName.ToString())) continue;

                                    object installLocation = subKey.GetValue("InstallLocation");
                                    if (installLocation != null && TryUseKoreaSourceDir(installLocation.ToString())) continue;

                                    object iconPath = subKey.GetValue("DisplayIcon");
                                    if (iconPath == null) continue;

                                    TryUseKoreaSourceDir(iconPath.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                koreaSourceDir = string.Empty;
            }

            if (string.IsNullOrEmpty(koreaSourceDir))
            {
                foreach (string knownGameDir in GetKnownKoreaGameDirs())
                {
                    if (TryUseKoreaSourceDir(knownGameDir)) break;
                }
            }
        }

        private void DetectGlobalClient()
        {
            if (!string.IsNullOrEmpty(targetDir)) return;

            try
            {
                string[] uninstallKeyNames = new string[]
                {
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                    "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
                };

                string[] uninstallSteamKeyNames = new string[]
                {
                    $"{uninstallKeyNames[0]}\\Steam App 39210",
                    $"{uninstallKeyNames[1]}\\Steam App 39210"
                };

                using (RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32))
                {
                    foreach (string uninstallSteamKeyName in uninstallSteamKeyNames)
                    {
                        if (!string.IsNullOrEmpty(targetDir)) break;

                        using (RegistryKey uninstallKey = localMachine.OpenSubKey(uninstallSteamKeyName))
                        {
                            if (uninstallKey == null) continue;

                            object installLocation = uninstallKey.GetValue("InstallLocation");
                            if (installLocation == null) continue;

                            TryUseTargetDir(installLocation.ToString());
                        }
                    }

                    if (string.IsNullOrEmpty(targetDir))
                    {
                        foreach (string uninstallKeyName in uninstallKeyNames)
                        {
                            if (!string.IsNullOrEmpty(targetDir)) break;

                            using (RegistryKey uninstallKey = localMachine.OpenSubKey(uninstallKeyName))
                            {
                                if (uninstallKey == null) continue;

                                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                                {
                                    if (!string.IsNullOrEmpty(targetDir)) break;

                                    using (RegistryKey subKey = uninstallKey.OpenSubKey(subKeyName))
                                    {
                                        if (subKey == null) continue;

                                        object displayName = subKey.GetValue("DisplayName");
                                        if (displayName == null || !IsGlobalClientDisplayName(displayName.ToString())) continue;

                                        object installLocation = subKey.GetValue("InstallLocation");
                                        if (installLocation != null && TryUseTargetDir(installLocation.ToString())) continue;

                                        object iconPath = subKey.GetValue("DisplayIcon");
                                        if (iconPath == null) continue;

                                        TryUseTargetDir(iconPath.ToString());
                                    }
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(targetDir))
                    {
                        foreach (string knownGameDir in GetKnownGameDirs())
                        {
                            if (TryUseTargetDir(knownGameDir)) break;
                        }
                    }
                }
            }
            catch
            {
                targetDir = string.Empty;
            }
        }

        // Downloads a file from given url while reporting progress on given background worker, then return the response as byte array.
        // If filePath is given, it will write to FileStream instead of memory.
        private byte[] DownloadFile(string url, string fileName, BackgroundWorker worker, string filePath = null)
        {
            using (HttpClient client = new HttpClient())
            {
                // Default user agent and timeout values.
                client.DefaultRequestHeaders.Add("User-Agent", "request");
                client.Timeout = TimeSpan.FromMinutes(5);

                // Indicate what file we're downloading...
                UpdateDownloadLabel($"다운로드중: {fileName}");

                // Download the header first to look at the content length.
                using (HttpResponseMessage responseMessage = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    responseMessage.EnsureSuccessStatusCode();

                    if (responseMessage.Content.Headers.ContentLength != null)
                    {
                        long contentLength = (long)responseMessage.Content.Headers.ContentLength;

                        // Create memory stream or file stream based on param and feed it with the client stream.
                        using (Stream s = string.IsNullOrEmpty(filePath) ? (Stream)new MemoryStream() : new FileStream(filePath, FileMode.Create))
                        using (Stream inStream = responseMessage.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        {
                            // Create a progress reporter that reports the progress to the designated background worker.
                            Progress<int> p = new Progress<int>(new Action<int>((value) =>
                            {
                                worker.ReportProgress(value);
                            }));

                            // Use a reasonable minimum buffer so tiny version/checksum files still copy correctly.
                            // Grab data from http client stream and copy to destination stream.
                            inStream.CopyToAsync(s, Math.Max(81920, (int)(contentLength / 10)), contentLength, p).GetAwaiter().GetResult();

                            // Empty the progress bar after download is complete.
                            worker.ReportProgress(0);

                            // Reset the label.
                            UpdateDownloadLabel("");

                            // If no file path was specified, return as byte array. Else just return.
                            if (string.IsNullOrEmpty(filePath))
                            {
                                return ((MemoryStream)s).ToArray();
                            }
                            else
                            {
                                return new byte[0];
                            }
                        }
                    }
                }
            }

            // If anything happened and didn't reach successful download, throw an exception.
            throw new Exception($"다음 파일을 다운로드하는 중 오류가 발생하였습니다. {url}");
        }

        // Clear all cached files.
        private void ClearCache()
        {
            Directory.CreateDirectory(Application.CommonAppDataPath);

            foreach (string s in Directory.GetFiles(Application.CommonAppDataPath))
            {
                File.Delete(s);
            }
        }

        // Get the SHA1 checksum from a file.
        private string ComputeSHA1(string filePath)
        {
            using (SHA1CryptoServiceProvider cryptoProvider = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(cryptoProvider.ComputeHash(File.ReadAllBytes(filePath))).Replace("-", "");
            }
        }

        // Check SHA1 checksum between given file and server record and return true if they match.
        private bool CheckSHA1(string filePath, string url, string fileName, BackgroundWorker worker)
        {
            return ComputeSHA1(filePath) == Encoding.ASCII.GetString(DownloadFile(url, fileName, worker)).Trim();
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            Action action = () =>
            {
                globalPathBrowseButton.Enabled = enabled;
                koreaPathBrowseButton.Enabled = enabled;
                targetLanguageComboBox.Enabled = enabled;
                detectPathsButton.Enabled = enabled;
                resetPathsButton.Enabled = enabled;
                openReleaseButton.Enabled = enabled;
                openLogsButton.Enabled = enabled;
                cleanupButton.Enabled = enabled;
#if TEST_BUILD
                restoreBackupButton.Enabled = enabled;
#else
                restoreBackupButton.Enabled = enabled;
#endif
                preflightCheckButton.Enabled = enabled;
#if TEST_BUILD
                debugBuildReleaseButton.Enabled = enabled;
                buildReleaseButton.Enabled = false;
                installButton.Enabled = false;
                chatOnlyInstallButton.Enabled = false;
                removeButton.Enabled = false;
#else
                debugBuildReleaseButton.Enabled = false;
                buildReleaseButton.Enabled = false;
                installButton.Enabled = enabled;
                chatOnlyInstallButton.Enabled = enabled;
                removeButton.Enabled = enabled;
#endif
            };

            if (InvokeRequired) Invoke(action);
            else action();
        }

        private bool RefreshTargetVersion()
        {
            if (string.IsNullOrEmpty(targetDir)) return false;

            string versionPath = Path.Combine(targetDir, versionFileName);
            if (!File.Exists(versionPath)) return false;

            targetVersion = File.ReadAllText(versionPath).Trim();
            return true;
        }

        private void SetProgressMarquee(bool enabled)
        {
            Action action = () =>
            {
                progressBar.Style = enabled ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = enabled ? 30 : 0;
                if (!enabled)
                {
                    progressBar.Value = 0;
                }
            };

            if (InvokeRequired) Invoke(action);
            else action();
        }

        private void SetProgressValue(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            Action action = () =>
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = 0;
                progressBar.Value = percent;
            };

            if (InvokeRequired) Invoke(action);
            else action();
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string NormalizeDirectory(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        private static bool IsSameOrChildDirectory(string path, string parent)
        {
            string normalizedPath = NormalizeDirectory(path);
            string normalizedParent = NormalizeDirectory(parent);
            return normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private string FindPatchGeneratorPath()
        {
            string[] candidatePaths = new string[]
            {
                Path.Combine(Application.StartupPath, "FFXIVPatchGenerator.exe"),
                Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\FFXIVPatchGenerator\bin\Release\FFXIVPatchGenerator.exe")),
                Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\FFXIVPatchGenerator\bin\Debug\FFXIVPatchGenerator.exe")),
                Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\..\FFXIVPatchGenerator\bin\Release\FFXIVPatchGenerator.exe"))
            };

            foreach (string candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }

        private string GetServerUrl()
        {
            return serverUrlBase + "release-" + targetLanguageCode;
        }

        private string GetManagedReleaseBaseDir()
        {
            string version = string.IsNullOrWhiteSpace(targetVersion) ? "unknown" : targetVersion.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                version = version.Replace(invalidChar, '_');
            }

            string outputDir = Path.Combine(Application.StartupPath, "generated-release", targetLanguageCode, version);
            if ((!string.IsNullOrEmpty(targetDir) && IsSameOrChildDirectory(outputDir, targetDir))
                || (!string.IsNullOrEmpty(koreaSourceDir) && IsSameOrChildDirectory(outputDir, koreaSourceDir)))
            {
                outputDir = Path.Combine(Application.CommonAppDataPath, "generated-release", targetLanguageCode, version);
            }

            return outputDir;
        }

        private string GetRestoreBaselineBaseDir()
        {
            string version = string.IsNullOrWhiteSpace(targetVersion) ? "unknown" : targetVersion.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                version = version.Replace(invalidChar, '_');
            }

            string outputDir = Path.Combine(Application.StartupPath, "restore-baseline", targetLanguageCode, version);
            if ((!string.IsNullOrEmpty(targetDir) && IsSameOrChildDirectory(outputDir, targetDir))
                || (!string.IsNullOrEmpty(koreaSourceDir) && IsSameOrChildDirectory(outputDir, koreaSourceDir)))
            {
                outputDir = Path.Combine(Application.CommonAppDataPath, "restore-baseline", targetLanguageCode, version);
            }

            return outputDir;
        }

        private string CreateManagedReleaseDirForRun()
        {
            string baseDir = GetManagedReleaseBaseDir();
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string outputDir = Path.Combine(baseDir, stamp);
            int suffix = 2;
            while (Directory.Exists(outputDir))
            {
                outputDir = Path.Combine(baseDir, stamp + "-" + suffix.ToString());
                suffix++;
            }

            return outputDir;
        }

        private string GetApplyGameDir()
        {
            if (!useDebugApplyPath)
            {
                return targetDir;
            }

            return Path.Combine(releaseOutputDir, "debug-apply", "game");
        }

        private bool TryHandleBuilderProgress(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith(builderProgressPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = line.Substring(builderProgressPrefix.Length);
            int separator = payload.IndexOf('|');
            if (separator <= 0)
            {
                return true;
            }

            int percent;
            if (!int.TryParse(payload.Substring(0, separator), out percent))
            {
                return true;
            }

            string message = payload.Substring(separator + 1);
            SetProgressValue(percent);
            UpdateStatusLabel("패치 생성 중... " + message);
            UpdateDownloadLabel(percent.ToString() + "%");
            return true;
        }

        private static string Tail(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(text.Length - maxLength);
        }

        private string FormatPatchGeneratorFailure(string output)
        {
            if (!string.IsNullOrEmpty(output) &&
                (output.IndexOf("already contains", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 output.IndexOf("already containes", StringComparison.OrdinalIgnoreCase) >= 0) &&
                output.IndexOf("dat1 entries", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string pollutedIndex = "0a0000/000000.win32.index";
                if (output.IndexOf("0a0000.win32.index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollutedIndex = "0a0000.win32.index";
                }
                else if (output.IndexOf("000000.win32.index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollutedIndex = "000000.win32.index";
                }

                return
                    "글로벌 서버 클라이언트의 " + pollutedIndex + "가 이미 패치된 상태입니다." + Environment.NewLine + Environment.NewLine +
                    "이전에 영어/일본어 한글 패치를 적용했거나 다른 패쳐가 index를 dat1로 돌려놓은 경우 발생합니다." + Environment.NewLine + Environment.NewLine +
                    "먼저 한글 패치 제거를 한 번 실행해서 글로벌 서버 클라이언트를 원본 index로 되돌린 뒤, 다시 한국 서버 클라이언트로 자동 패치를 실행해주세요." + Environment.NewLine + Environment.NewLine +
                    "제거 버튼이 비활성화되어 있거나 이전 패쳐로 적용한 패치라면, 이전 패쳐의 제거 기능 또는 런처 파일 검사/복구로 글로벌 서버 클라이언트를 원본 상태로 돌린 뒤 다시 시도해야 합니다." + Environment.NewLine + Environment.NewLine +
                    "실제 release 생성에는 깨끗한 글로벌 서버 클라이언트 index가 필요합니다." + Environment.NewLine + Environment.NewLine +
                    "테스트 빌드에서는 진행도/UX 확인을 위해 오염된 index를 허용하지만, 그 결과물은 배포용으로 쓰면 안 됩니다." + Environment.NewLine + Environment.NewLine +
                    Tail(output, 2000);
            }

            return "FFXIVPatchGenerator 실행에 실패했어요." + Environment.NewLine + Tail(output, 4000);
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private int CountIndexDat1Entries(string indexPath)
        {
            byte[] bytes = File.ReadAllBytes(indexPath);
            if (bytes.Length < 0x100)
            {
                throw new InvalidDataException("index 파일 크기가 너무 작습니다.");
            }

            int sqpackHeaderSize = checked((int)ReadUInt32LittleEndian(bytes, 0x0C));
            int indexHeaderOffset = sqpackHeaderSize;
            if (indexHeaderOffset < 0 || indexHeaderOffset + 0x10 > bytes.Length)
            {
                throw new InvalidDataException("index 헤더 위치가 올바르지 않습니다.");
            }

            int indexDataOffset = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x08));
            int indexDataSize = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x0C));
            if (indexDataOffset < 0 || indexDataSize < 0 || indexDataOffset + indexDataSize > bytes.Length)
            {
                throw new InvalidDataException("index 데이터 영역이 올바르지 않습니다.");
            }

            int entryCount = indexDataSize / 16;
            int dat1Count = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = indexDataOffset + i * 16;
                uint data = ReadUInt32LittleEndian(bytes, entryOffset + 8);
                byte datId = (byte)((data & 0xEu) >> 1);
                if (datId == 1)
                {
                    dat1Count++;
                }
            }

            return dat1Count;
        }

        private string GetTargetSqpackDir()
        {
            if (string.IsNullOrEmpty(targetDir))
            {
                return string.Empty;
            }

            return Path.Combine(targetDir, "sqpack", "ffxiv");
        }

        private string GetBackupRootDir()
        {
            string backupRoot = Path.Combine(Application.StartupPath, "backups");
            if ((!string.IsNullOrEmpty(targetDir) && IsSameOrChildDirectory(backupRoot, targetDir))
                || (!string.IsNullOrEmpty(koreaSourceDir) && IsSameOrChildDirectory(backupRoot, koreaSourceDir)))
            {
                backupRoot = Path.Combine(Application.CommonAppDataPath, "backups");
            }

            return backupRoot;
        }

        private static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "backup";
            }

            string result = value.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidChar, '_');
            }

            return result;
        }

        private string BackupTargetFiles(IEnumerable<string> fileNames, string reason)
        {
            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir) || !Directory.Exists(sqpackDir))
            {
                throw new DirectoryNotFoundException("글로벌 서버 클라이언트 sqpack 경로를 찾을 수 없어요.");
            }

            string backupDir = Path.Combine(
                GetBackupRootDir(),
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + SanitizeFileNamePart(reason));
            Directory.CreateDirectory(backupDir);

            bool copiedAny = false;
            foreach (string fileName in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string sourcePath = Path.Combine(sqpackDir, fileName);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                File.Copy(sourcePath, Path.Combine(backupDir, fileName), true);
                copiedAny = true;
            }

            return copiedAny ? backupDir : string.Empty;
        }

        private bool TryCreateLocalRestoreBaseline(List<string> lines, out string baselineDir)
        {
            baselineDir = null;

            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir) || !Directory.Exists(sqpackDir))
            {
                if (lines != null)
                {
                    lines.Add("[주의] 로컬 복구 기준을 만들 수 없습니다. 글로벌 sqpack 경로가 아직 설정되지 않았습니다.");
                }

                return false;
            }

            string outputDir = GetRestoreBaselineBaseDir();
            Directory.CreateDirectory(outputDir);

            bool allReady = true;
            bool wroteAny = false;
            foreach (string fileName in restoreFiles)
            {
                string destinationPath = Path.Combine(outputDir, "orig." + fileName);
                if (File.Exists(destinationPath))
                {
                    continue;
                }

                string currentIndexPath = Path.Combine(sqpackDir, fileName);
                string installedOrigPath = Path.Combine(sqpackDir, "orig." + fileName);
                string sourcePath = null;

                if (File.Exists(installedOrigPath))
                {
                    sourcePath = installedOrigPath;
                }
                else if (File.Exists(currentIndexPath))
                {
                    int dat1Count = CountIndexDat1Entries(currentIndexPath);
                    if (dat1Count == 0)
                    {
                        sourcePath = currentIndexPath;
                    }
                    else if (lines != null)
                    {
                        lines.Add("[주의] " + fileName + "는 이미 dat1 엔트리 " + dat1Count + "개를 포함하고 있어 복구 기준으로 저장하지 않았습니다.");
                    }
                }

                if (string.IsNullOrEmpty(sourcePath))
                {
                    allReady = false;
                    continue;
                }

                File.Copy(sourcePath, destinationPath, false);
                wroteAny = true;
            }

            string versionPath = Path.Combine(targetDir, versionFileName);
            if (File.Exists(versionPath))
            {
                string baselineVersionPath = Path.Combine(outputDir, versionFileName);
                if (!File.Exists(baselineVersionPath))
                {
                    File.Copy(versionPath, baselineVersionPath, false);
                }
            }

            string readmePath = Path.Combine(outputDir, "restore-baseline.txt");
            if (!File.Exists(readmePath))
            {
                File.WriteAllText(
                    readmePath,
                    "This folder stores original index files captured before applying a Korean patch." + Environment.NewLine +
                    "Global game: " + targetDir + Environment.NewLine +
                    "Target language: " + targetLanguageDisplayName + " (" + targetLanguageCode + ")" + Environment.NewLine +
                    "Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine,
                    Encoding.UTF8);
            }

            bool hasAllBaselineFiles = restoreFiles.All(fileName => File.Exists(Path.Combine(outputDir, "orig." + fileName)));
            if (hasAllBaselineFiles)
            {
                baselineDir = outputDir;
                if (lines != null)
                {
                    lines.Add((wroteAny ? "[OK] 로컬 복구 기준 생성: " : "[OK] 로컬 복구 기준 확인: ") + outputDir);
                }

                return true;
            }

            if (!allReady && lines != null)
            {
                lines.Add("[주의] 로컬 복구 기준을 완성하지 못했습니다. clean index 또는 기존 orig index가 필요합니다: " + outputDir);
            }

            return false;
        }

        private string GetLogRootDir()
        {
            string logRoot = Path.Combine(Application.StartupPath, "logs");
            if ((!string.IsNullOrEmpty(targetDir) && IsSameOrChildDirectory(logRoot, targetDir))
                || (!string.IsNullOrEmpty(koreaSourceDir) && IsSameOrChildDirectory(logRoot, koreaSourceDir)))
            {
                logRoot = Path.Combine(Application.CommonAppDataPath, "logs");
            }

            return logRoot;
        }

        private string WriteOperationLog(string operation, IEnumerable<string> lines)
        {
            string logRoot = GetLogRootDir();
            Directory.CreateDirectory(logRoot);

            string logPath = Path.Combine(
                logRoot,
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + SanitizeFileNamePart(operation) + ".log");

            List<string> output = new List<string>();
            output.Add("Operation: " + operation);
            output.Add("Build: " +
#if TEST_BUILD
                "TEST"
#else
                "RELEASE"
#endif
                );
            output.Add("Time Local: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            output.Add("Target Language: " + targetLanguageDisplayName + " (" + targetLanguageCode + ")");
            output.Add("Global Game: " + (string.IsNullOrEmpty(targetDir) ? "(unset)" : targetDir));
            output.Add("Korean Game: " + (string.IsNullOrEmpty(koreaSourceDir) ? "(unset)" : koreaSourceDir));
            output.Add("Global Version: " + (string.IsNullOrEmpty(targetVersion) ? "(unknown)" : targetVersion));
            output.Add("Release Output: " + (string.IsNullOrEmpty(releaseOutputDir) ? "(unset)" : releaseOutputDir));
            output.Add(new string('-', 72));

            if (lines != null)
            {
                output.AddRange(lines);
            }

            File.WriteAllText(logPath, string.Join(Environment.NewLine, output.ToArray()), Encoding.UTF8);
            lastLogPath = logPath;
            return logPath;
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        private static void AppendJsonString(StringBuilder sb, string name, string value, bool appendComma = true)
        {
            sb.Append("  \"");
            sb.Append(JsonEscape(name));
            sb.Append("\": \"");
            sb.Append(JsonEscape(value));
            sb.Append("\"");
            if (appendComma) sb.Append(",");
            sb.AppendLine();
        }

        private string GetKoreaVersion()
        {
            if (string.IsNullOrEmpty(koreaSourceDir)) return string.Empty;

            string versionPath = Path.Combine(koreaSourceDir, versionFileName);
            return File.Exists(versionPath) ? File.ReadAllText(versionPath).Trim() : string.Empty;
        }

        private string WriteManifest(string outputDir, string applyGameDir, bool debugApply, string backupDir)
        {
            string manifestPath = Path.Combine(outputDir, "manifest.json");
            List<string> manifestFiles = textPatchFiles
                .Concat(fontPatchFiles)
                .Concat(restoreFiles.Select(fileName => "orig." + fileName))
                .Concat(new string[] { versionFileName })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(fileName => File.Exists(Path.Combine(outputDir, fileName)))
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"schemaVersion\": 1,");
            AppendJsonString(sb, "generatedAtLocal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendJsonString(sb, "generatedAtUtc", DateTime.UtcNow.ToString("o"));
            AppendJsonString(sb, "targetLanguage", targetLanguageCode);
            AppendJsonString(sb, "targetLanguageDisplayName", targetLanguageDisplayName);
            AppendJsonString(sb, "globalVersion", targetVersion);
            AppendJsonString(sb, "koreanVersion", GetKoreaVersion());
            AppendJsonString(sb, "globalGamePath", targetDir);
            AppendJsonString(sb, "koreanGamePath", koreaSourceDir);
            AppendJsonString(sb, "releaseOutputDir", outputDir);
            AppendJsonString(sb, "applyGameDir", applyGameDir);
            AppendJsonString(sb, "backupDir", backupDir);
            sb.AppendLine("  \"debugApply\": " + (debugApply ? "true" : "false") + ",");
            sb.AppendLine("  \"files\": [");

            for (int i = 0; i < manifestFiles.Count; i++)
            {
                string fileName = manifestFiles[i];
                string filePath = Path.Combine(outputDir, fileName);
                FileInfo info = new FileInfo(filePath);
                sb.AppendLine("    {");
                AppendJsonString(sb, "name", fileName);
                sb.AppendLine("      \"size\": " + info.Length.ToString() + ",");
                AppendJsonString(sb, "sha1", ComputeSHA1(filePath), false);
                sb.Append("    }");
                if (i != manifestFiles.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(manifestPath, sb.ToString(), Encoding.UTF8);
            return manifestPath;
        }

        private void ValidateReleaseOutput(string outputDir, IEnumerable<string> expectedPatchFiles)
        {
            string[] patchFiles = expectedPatchFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            List<string> requiredOrigFiles = new List<string>();
            if (patchFiles.Any(fileName => textPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)))
            {
                requiredOrigFiles.Add("orig.0a0000.win32.index");
            }

            if (patchFiles.Any(fileName => fontPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)))
            {
                requiredOrigFiles.Add("orig.000000.win32.index");
            }

            List<string> requiredReleaseFiles = patchFiles
                .Concat(requiredOrigFiles)
                .Concat(new string[] { versionFileName })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string fileName in requiredReleaseFiles)
            {
                string filePath = Path.Combine(outputDir, fileName);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("생성된 release 파일이 누락되었습니다.", filePath);
                }
            }

            string versionPath = Path.Combine(outputDir, versionFileName);
            string releaseVersion = File.ReadAllText(versionPath).Trim();
            if (!string.IsNullOrEmpty(targetVersion) && !string.Equals(releaseVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("생성된 ffxivgame.ver가 글로벌 클라이언트 버전과 다릅니다. 생성 버전: " + releaseVersion + ", 글로벌 버전: " + targetVersion);
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "0a0000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                int textDat1Count = CountIndexDat1Entries(Path.Combine(outputDir, "0a0000.win32.index"));
                if (textDat1Count <= 0)
                {
                    throw new InvalidDataException("생성된 0a0000.win32.index에 dat1 엔트리가 없습니다.");
                }
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "000000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                int fontDat1Count = CountIndexDat1Entries(Path.Combine(outputDir, "000000.win32.index"));
                if (fontDat1Count <= 0)
                {
                    throw new InvalidDataException("생성된 000000.win32.index에 dat1 엔트리가 없습니다.");
                }
            }
        }

        private void ValidateFilesExist(string directory, IEnumerable<string> fileNames, string context)
        {
            foreach (string fileName in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string filePath = Path.Combine(directory, fileName);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(context + " 파일이 누락되었습니다.", filePath);
                }
            }
        }

        private string FindLatestDirectory(params string[] roots)
        {
            DirectoryInfo latest = null;
            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;

                    foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        DirectoryInfo info = new DirectoryInfo(directory);
                        if (latest == null || info.LastWriteTimeUtc > latest.LastWriteTimeUtc)
                        {
                            latest = info;
                        }
                    }
                }
                catch
                {
                    // Keep searching other roots.
                }
            }

            return latest == null ? null : latest.FullName;
        }

        private string ResolveReleaseFolderToOpen()
        {
            if (!string.IsNullOrEmpty(releaseOutputDir) && Directory.Exists(releaseOutputDir))
            {
                return releaseOutputDir;
            }

            string languageRoot = Path.Combine(Application.StartupPath, "generated-release", targetLanguageCode);
            string commonLanguageRoot = Path.Combine(Application.CommonAppDataPath, "generated-release", targetLanguageCode);
            string latest = FindLatestDirectory(languageRoot, commonLanguageRoot);
            if (!string.IsNullOrEmpty(latest))
            {
                return latest;
            }

            string appRoot = Path.Combine(Application.StartupPath, "generated-release");
            string commonRoot = Path.Combine(Application.CommonAppDataPath, "generated-release");
            return FindLatestDirectory(appRoot, commonRoot);
        }

        private void OpenFolder(string folderPath, string missingMessage)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show(missingMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start(new ProcessStartInfo()
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }

        private IEnumerable<string> GetManagedRootDirs()
        {
            return new string[]
            {
                Path.Combine(Application.StartupPath, "generated-release"),
                Path.Combine(Application.CommonAppDataPath, "generated-release"),
                Path.Combine(Application.StartupPath, "restore-baseline"),
                Path.Combine(Application.CommonAppDataPath, "restore-baseline"),
                GetBackupRootDir(),
                GetLogRootDir()
            }.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private int CleanupOldManagedFiles(int olderThanDays, List<string> logLines)
        {
            DateTime threshold = DateTime.Now.AddDays(-olderThanDays);
            int deleted = 0;

            foreach (string root in GetManagedRootDirs())
            {
                if (!Directory.Exists(root)) continue;

                foreach (string filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    FileInfo file = new FileInfo(filePath);
                    if (file.LastWriteTime >= threshold) continue;

                    file.Delete();
                    deleted++;
                    logLines.Add("Deleted file: " + filePath);
                }

                string[] directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                    .OrderByDescending(path => path.Length)
                    .ToArray();
                foreach (string directory in directories)
                {
                    if (!Directory.Exists(directory)) continue;
                    if (Directory.EnumerateFileSystemEntries(directory).Any()) continue;

                    DirectoryInfo info = new DirectoryInfo(directory);
                    if (info.LastWriteTime >= threshold) continue;

                    Directory.Delete(directory, false);
                    deleted++;
                    logLines.Add("Deleted empty directory: " + directory);
                }
            }

            return deleted;
        }

        private string SelectBackupDirectory()
        {
            string backupRoot = GetBackupRootDir();
            if (!Directory.Exists(backupRoot))
            {
                return null;
            }

            DirectoryInfo[] backups = new DirectoryInfo(backupRoot)
                .GetDirectories()
                .OrderByDescending(directory => directory.LastWriteTimeUtc)
                .Take(30)
                .ToArray();
            if (backups.Length == 0)
            {
                return null;
            }

            using (Form dialog = new Form())
            using (ListBox listBox = new ListBox())
            using (Button restoreButton = new Button())
            using (Button cancelButton = new Button())
            {
                dialog.Text = "백업 선택";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(560, 320);

                listBox.Location = new Point(12, 12);
                listBox.Size = new Size(536, 250);
                listBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                listBox.DisplayMember = "Name";

                foreach (DirectoryInfo backup in backups)
                {
                    listBox.Items.Add(backup);
                }

                restoreButton.Text = "복구";
                restoreButton.Location = new Point(372, 278);
                restoreButton.Size = new Size(84, 28);
                restoreButton.DialogResult = DialogResult.OK;

                cancelButton.Text = "취소";
                cancelButton.Location = new Point(464, 278);
                cancelButton.Size = new Size(84, 28);
                cancelButton.DialogResult = DialogResult.Cancel;

                dialog.Controls.Add(listBox);
                dialog.Controls.Add(restoreButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = restoreButton;
                dialog.CancelButton = cancelButton;

                listBox.SelectedIndex = 0;
                if (dialog.ShowDialog(this) != DialogResult.OK || listBox.SelectedItem == null)
                {
                    return null;
                }

                return ((DirectoryInfo)listBox.SelectedItem).FullName;
            }
        }

        private void RestoreSelectedBackup(string backupDir)
        {
            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir) || !Directory.Exists(sqpackDir))
            {
                throw new DirectoryNotFoundException("글로벌 서버 클라이언트 sqpack 경로를 찾을 수 없어요.");
            }

            string[] backupFiles = Directory.GetFiles(backupDir)
                .Select(Path.GetFileName)
                .Where(fileName =>
                    textPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                    || fontPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                    || restoreFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (backupFiles.Length == 0)
            {
                throw new FileNotFoundException("선택한 백업 폴더에 복구 가능한 index/dat 파일이 없습니다.", backupDir);
            }

            string beforeRestoreBackup = BackupTargetFiles(backupFiles, "before-backup-restore");
            foreach (string fileName in backupFiles)
            {
                File.Copy(Path.Combine(backupDir, fileName), Path.Combine(sqpackDir, fileName), true);
            }

            string logPath = WriteOperationLog("backup-restore", new string[]
            {
                "Restored backup: " + backupDir,
                "Before restore backup: " + beforeRestoreBackup,
                "Files: " + string.Join(", ", backupFiles)
            });

            MessageBox.Show(
                "백업 복구가 완료되었어요." + Environment.NewLine + Environment.NewLine +
                "복구한 백업: " + backupDir + Environment.NewLine +
                "복구 전 백업: " + beforeRestoreBackup + Environment.NewLine +
                "로그: " + logPath,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private IEnumerable<string> GetLocalOrigIndexCandidateDirs()
        {
            List<string> candidateDirs = new List<string>();

            string targetSqpackDir = GetTargetSqpackDir();
            if (!string.IsNullOrEmpty(targetSqpackDir))
            {
                candidateDirs.Add(targetSqpackDir);
            }

            if (!string.IsNullOrEmpty(releaseOutputDir))
            {
                candidateDirs.Add(releaseOutputDir);
            }

            try
            {
                if (!string.IsNullOrEmpty(targetVersion))
                {
                    candidateDirs.Add(GetManagedReleaseBaseDir());
                }
            }
            catch
            {
                // The managed path is only a convenience candidate.
            }

            string[] roots = new string[]
            {
                Path.Combine(Application.StartupPath, "generated-release"),
                Path.Combine(Application.CommonAppDataPath, "generated-release"),
                Path.Combine(Application.StartupPath, "restore-baseline"),
                Path.Combine(Application.CommonAppDataPath, "restore-baseline")
            };

            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;

                    candidateDirs.Add(root);
                    candidateDirs.AddRange(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
                }
                catch
                {
                    // Ignore folders that cannot be enumerated and keep checking other candidates.
                }
            }

            return candidateDirs
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .ToArray();
        }

        private bool TryFindLocalOrigIndexDir(out string sourceDir)
        {
            foreach (string candidateDir in GetLocalOrigIndexCandidateDirs())
            {
                bool hasAllOrigIndexes = restoreFiles.All(fileName => File.Exists(Path.Combine(candidateDir, "orig." + fileName)));
                if (!hasAllOrigIndexes)
                {
                    continue;
                }

                sourceDir = candidateDir;
                return true;
            }

            sourceDir = null;
            return false;
        }

        private bool RestoreFromLocalOrigIndexes(out string restoreSourceDir, out string backupDir)
        {
            restoreSourceDir = null;
            backupDir = null;

            string sourceDir;
            if (!TryFindLocalOrigIndexDir(out sourceDir))
            {
                return false;
            }

            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir) || !Directory.Exists(sqpackDir))
            {
                throw new DirectoryNotFoundException("글로벌 서버 클라이언트 sqpack 경로를 찾을 수 없어요.");
            }

            UpdateStatusLabel("로컬 orig index로 복구 중...");
            UpdateDownloadLabel("");
            SetProgressValue(0);

            backupDir = BackupTargetFiles(restoreFiles, "local-restore");

            for (int i = 0; i < restoreFiles.Length; i++)
            {
                string fileName = restoreFiles[i];
                File.Copy(Path.Combine(sourceDir, "orig." + fileName), Path.Combine(sqpackDir, fileName), true);
                SetProgressValue((i + 1) * 100 / restoreFiles.Length);
            }

            restoreSourceDir = sourceDir;
            return true;
        }

        private void AddPreflightIndexStatus(List<string> lines, ref int failures, ref int warnings, string indexFileName)
        {
            string sqpackDir = GetTargetSqpackDir();
            string indexPath = Path.Combine(sqpackDir, indexFileName);
            string origIndexPath = Path.Combine(sqpackDir, "orig." + indexFileName);

            if (!File.Exists(indexPath))
            {
                failures++;
                lines.Add("[실패] 글로벌 " + indexFileName + " 파일이 없습니다.");
                return;
            }

            try
            {
                int dat1Count = CountIndexDat1Entries(indexPath);
                if (dat1Count <= 0)
                {
                    lines.Add("[OK] 글로벌 " + indexFileName + "는 clean index로 보입니다.");
                    return;
                }

                if (File.Exists(origIndexPath))
                {
                    warnings++;
                    lines.Add("[주의] 글로벌 " + indexFileName + "에 dat1 엔트리 " + dat1Count + "개가 있지만, 같은 폴더에 orig index가 있어 복구 또는 빌더 기준 파일로 사용할 수 있습니다.");
                }
                else
                {
#if TEST_BUILD
                    warnings++;
                    lines.Add("[주의] 글로벌 " + indexFileName + "에 dat1 엔트리 " + dat1Count + "개가 있습니다. 테스트 빌드는 진행 가능하지만 결과물은 배포용으로 쓰면 안 됩니다.");
#else
                    failures++;
                    lines.Add("[실패] 글로벌 " + indexFileName + "에 dat1 엔트리 " + dat1Count + "개가 있습니다. 먼저 한글 패치 제거 또는 런처 복구로 clean index를 되돌려야 합니다.");
#endif
                }
            }
            catch (Exception exception)
            {
                warnings++;
                lines.Add("[주의] 글로벌 " + indexFileName + " dat1 상태를 읽지 못했습니다: " + exception.Message);
            }
        }

        private void PreflightCheckWork()
        {
            try
            {
                UpdateStatusLabel("사전 점검 중...");
                UpdateDownloadLabel("");
                SetProgressValue(0);

                if (string.IsNullOrEmpty(koreaSourceDir))
                {
                    DetectKoreaClient();
                }

                RefreshTargetVersion();
                UpdatePathTextBoxes();

                List<string> lines = new List<string>();
                int failures = 0;
                int warnings = 0;

#if TEST_BUILD
                warnings++;
                lines.Add("[주의] 현재 실행 파일은 테스트 빌드입니다. 실제 글로벌 서버 클라이언트에는 적용하지 않고 debug-apply 폴더만 사용합니다.");
#else
                lines.Add("[OK] 현재 실행 파일은 릴리즈 빌드입니다. 실제 적용 버튼은 글로벌 서버 클라이언트 폴더를 변경합니다.");
#endif
                lines.Add("[OK] 베이스 클라이언트 언어: " + targetLanguageDisplayName + " (" + targetLanguageCode + ")");

                if (string.IsNullOrEmpty(targetDir) || CheckTargetDir(targetDir) == null)
                {
                    failures++;
                    lines.Add("[실패] 글로벌 서버 클라이언트 경로가 올바르지 않습니다.");
                }
                else
                {
                    lines.Add("[OK] 글로벌 서버 클라이언트: " + targetDir);
                    string versionPath = Path.Combine(targetDir, versionFileName);
                    if (File.Exists(versionPath))
                    {
                        lines.Add("[OK] 글로벌 서버 버전: " + File.ReadAllText(versionPath).Trim());
                    }
                }

                if (string.IsNullOrEmpty(koreaSourceDir) || CheckTargetDir(koreaSourceDir) == null)
                {
                    failures++;
                    lines.Add("[실패] 한국 서버 클라이언트 경로가 올바르지 않습니다.");
                }
                else
                {
                    lines.Add("[OK] 한국 서버 클라이언트: " + koreaSourceDir);
                    string versionPath = Path.Combine(koreaSourceDir, versionFileName);
                    if (File.Exists(versionPath))
                    {
                        lines.Add("[OK] 한국 서버 버전: " + File.ReadAllText(versionPath).Trim());
                    }
                }

                string patchGeneratorPath = FindPatchGeneratorPath();
                if (string.IsNullOrEmpty(patchGeneratorPath))
                {
                    failures++;
                    lines.Add("[실패] FFXIVPatchGenerator.exe를 찾을 수 없습니다.");
                }
                else
                {
                    lines.Add("[OK] FFXIVPatchGenerator.exe: " + patchGeneratorPath);
                }

                if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                {
                    string sqpackDir = GetTargetSqpackDir();
                    string[] requiredSqpackFiles = new string[]
                    {
                        "000000.win32.index",
                        "000000.win32.dat0",
                        "0a0000.win32.index",
                        "0a0000.win32.dat0"
                    };

                    foreach (string requiredSqpackFile in requiredSqpackFiles)
                    {
                        if (File.Exists(Path.Combine(sqpackDir, requiredSqpackFile)))
                        {
                            lines.Add("[OK] 글로벌 " + requiredSqpackFile + " 확인");
                        }
                        else
                        {
                            failures++;
                            lines.Add("[실패] 글로벌 " + requiredSqpackFile + " 파일이 없습니다.");
                        }
                    }

                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "0a0000.win32.index");
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "000000.win32.index");
                }

                try
                {
                    releaseOutputDir = GetManagedReleaseBaseDir();
                    Directory.CreateDirectory(releaseOutputDir);
                    lines.Add("[OK] 생성 release 관리 폴더: " + releaseOutputDir);
                }
                catch (Exception exception)
                {
                    failures++;
                    lines.Add("[실패] 생성 release 관리 폴더를 준비하지 못했습니다: " + exception.Message);
                }

                string localRestoreDir;
                TryCreateLocalRestoreBaseline(lines, out localRestoreDir);
                if (TryFindLocalOrigIndexDir(out localRestoreDir))
                {
                    lines.Add("[OK] 로컬 복구용 orig index 발견: " + localRestoreDir);
                }
                else
                {
                    warnings++;
                    lines.Add("[주의] 로컬 복구용 orig index를 찾지 못했습니다. 제거 시 이전 패쳐의 제거 기능 또는 런처 파일 검사/복구가 필요할 수 있습니다.");
                }

                string summary = failures == 0
                    ? (warnings == 0 ? "사전 점검 통과" : "사전 점검 완료 - 주의 항목 있음")
                    : "사전 점검 실패 - 조치 필요";
                string logPath = WriteOperationLog("preflight", lines);

                UpdateStatusLabel(summary, failures > 0);
                SetProgressValue(failures == 0 ? 100 : 0);

                ShowMessageBox(
                    MessageBoxButtons.OK,
                    failures > 0 ? MessageBoxIcon.Warning : (warnings > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Information),
                    summary,
                    "로그: " + logPath,
                    string.Join(Environment.NewLine, lines.ToArray()));
            }
            catch (Exception exception)
            {
                UpdateStatusLabel("사전 점검 실패", true);
                string logPath = WriteOperationLog("preflight-error", new string[] { exception.ToString() });
                ShowMessageBox(
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    "사전 점검 중 오류가 발생했어요.",
                    "로그: " + logPath,
                    "에러 내용:",
                    exception.ToString());
            }
            finally
            {
                SetActionButtonsEnabled(true);
            }
        }

        private bool ConfirmActualClientWrite(string actionName)
        {
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("글로벌 서버 클라이언트 경로가 설정되지 않았어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            DialogResult result = MessageBox.Show(
                actionName + " 작업은 실제 글로벌 서버 클라이언트 폴더에 파일을 복사합니다." + Environment.NewLine + Environment.NewLine +
                "대상 경로:" + Environment.NewLine +
                targetDir + Environment.NewLine + Environment.NewLine +
                "작업 전에 현재 파일은 자동으로 backups 폴더에 저장됩니다." + Environment.NewLine + Environment.NewLine +
                "계속할까요?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            return result == DialogResult.Yes;
        }

        #endregion

        #region Event Handlers

        // Show progress using progress bar.
        private void initialChecker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }

        // Background worker that does initial checks.
        private void initialChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // Wait until the UI is ready.
                while (!statusLabel.IsHandleCreated) { }

                UpdateStatusLabel("환경 체크 중...");

                // Check if the patch program is already running, and terminate if it is.
                if (Process.GetProcessesByName(mainFileName).Length > 1)
                {
                    ShowMessageBox(
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error,
                        "FFXIV 한글 패치 프로그램이 이미 실행중이에요.");
                    CloseForm();
                    return;
                }

                // Check if FFXIV game process is running.
#if TEST_BUILD
                if (Process.GetProcesses().Any(p => gameProcessNames.Contains(p.ProcessName.ToLower())))
                {
                    UpdateStatusLabel("테스트 빌드: FFXIV 실행 중이어도 계속 진행합니다.");
                }
#else
                if (Process.GetProcesses().Any(p => gameProcessNames.Contains(p.ProcessName.ToLower())))
                {
                    ShowMessageBox(
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error,
                        "FFXIV가 이미 실행중이에요.",
                        "FFXIV를 종료한 후 한글 패치 프로그램을 다시 실행해주세요.");
                    CloseForm();
                    return;
                }
#endif

                // Populate necessary paths.
                mainPath = Application.ExecutablePath;
                mainTempPath = Path.Combine(Application.CommonAppDataPath, $"{mainFileName}.exe");
                updaterPath = Path.Combine(Application.CommonAppDataPath, $"{updaterFileName}.exe");

                // Clean up some stuff.
                ClearCache();

                // Remote self-update is intentionally disabled in this fork.
                // Release builds generate patch files locally and should not depend on old upstream release assets.
                UpdateStatusLabel("로컬 생성 모드로 실행 중...");

                // Try to detect FFXIV client path.
                UpdateStatusLabel("글로벌 서버 클라이언트를 찾는 중...");

                try
                {
                    // Check windows registry uninstall list to find the FFXIV installation.
                    string[] uninstallKeyNames = new string[]
                    {
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
                        "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
                    };

                    string[] uninstallSteamKeyNames = new string[]
                    {
                        $"{uninstallKeyNames[0]}\\Steam App 39210",
                        $"{uninstallKeyNames[1]}\\Steam App 39210"
                    };

                    // Check steam registry first...
                    using (RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32))
                    {
                        foreach (string uninstallSteamKeyName in uninstallSteamKeyNames)
                        {
                            if (!string.IsNullOrEmpty(targetDir)) break;

                            using (RegistryKey uninstallKey = localMachine.OpenSubKey(uninstallSteamKeyName))
                            {
                                if (uninstallKey == null) continue;

                                object installLocation = uninstallKey.GetValue("InstallLocation");
                                if (installLocation == null) continue;

                                TryUseTargetDir(installLocation.ToString());
                            }
                        }

                        // If target directory is still not set, search for square enix installation path.
                        if (string.IsNullOrEmpty(targetDir))
                        {
                            foreach(string uninstallKeyName in uninstallKeyNames)
                            {
                                if (!string.IsNullOrEmpty(targetDir)) break;

                                using (RegistryKey uninstallKey = localMachine.OpenSubKey(uninstallKeyName))
                                {
                                    if (uninstallKey == null) continue;

                                    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                                    {
                                        if (!string.IsNullOrEmpty(targetDir)) break;

                                        using (RegistryKey subKey = uninstallKey.OpenSubKey(subKeyName))
                                        {
                                            if (subKey == null) continue;

                                            object displayName = subKey.GetValue("DisplayName");
                                            if (displayName == null || !IsGlobalClientDisplayName(displayName.ToString())) continue;

                                            object installLocation = subKey.GetValue("InstallLocation");
                                            if (installLocation != null && TryUseTargetDir(installLocation.ToString())) continue;

                                            object iconPath = subKey.GetValue("DisplayIcon");
                                            if (iconPath == null) continue;

                                            TryUseTargetDir(iconPath.ToString());
                                        }
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(targetDir))
                        {
                            foreach (string knownGameDir in GetKnownGameDirs())
                            {
                                if (TryUseTargetDir(knownGameDir)) break;
                            }
                        }
                    }
                }
                catch
                {
                    // Any exception happened during dtection, just set directory to not found so user can select it.
                    targetDir = string.Empty;
                }

                UpdateStatusLabel("한국 서버 클라이언트를 찾는 중...");
                DetectKoreaClient();
                UpdatePathTextBoxes();

                if (string.IsNullOrEmpty(targetDir))
                {
                    UpdateStatusLabel("글로벌 서버 클라이언트 경로를 수동으로 지정해주세요.", true);
                    SetPathBrowseButtonsEnabled(true);
                    Invoke(new Action(() =>
                    {
                        progressBar.Value = 0;
                        downloadLabel.Text = "";
                    }));
                    return;
                }

                // Check if korean chat registry is installed.
                UpdateStatusLabel("한글 채팅 레지스트리 설치 확인 중...");

#if TEST_BUILD
                UpdateStatusLabel("테스트 빌드: 한글 채팅 레지스트리 설치 확인을 생략합니다.");
#else
                // Check registry for scancode map.
                using (RegistryKey keyboardLayoutKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Keyboard Layout", true))
                {
                    if (keyboardLayoutKey != null)
                    {
                        object scancodeMap = keyboardLayoutKey.GetValue("Scancode Map");

                        // If scancode map doesn't exist or is invalid...
                        if (scancodeMap == null || !this.scancodeMap.SequenceEqual((byte[])scancodeMap))
                        {
                            // Install scancode map.
                            UpdateStatusLabel("한글 채팅 레지스트리 설치 중...");

                            ShowMessageBox(
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information,
                                "한글 채팅 레지스트리를 설치할게요.",
                                "설치가 끝난 후 컴퓨터를 재시작하지 않으면 FFXIV 클라이언트 내부에서 한/영 키 입력이 제대로 동작하지 않을 수 있어요.");

                            keyboardLayoutKey.SetValue("Scancode Map", this.scancodeMap);

                            ShowMessageBox(
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information,
                                "한글 채팅 레지스트리 설치가 완료되었어요.",
                                "컴퓨터를 재시작한 후 다시 실행해주세요.");

                            CloseForm();
                            return;
                        }
                    }
                }
#endif

                // Check the target client version.
                UpdateStatusLabel("클라이언트 버전 체크 중...");

                // Read the version from target client.
                RefreshTargetVersion();

                // This fork does not download prebuilt patch releases.
                // All patch files are generated from the user's local Korean/global clients.
                serverVersion = "로컬 생성 모드";
                serverPatchAvailable = false;
                UpdateDownloadLabel("");

                // Check all done!
                UpdateStatusLabel(serverPatchAvailable
                    ? $"버전 {targetVersion}, {targetLanguageDisplayName}용 패치 버전 {serverVersion}"
                    : $"버전 {targetVersion}, 로컬 생성 모드", serverPatchAvailable && !serverVersion.Equals(targetVersion));

                Invoke(new Action(() =>
                {
                    globalPathBrowseButton.Enabled = true;
                    koreaPathBrowseButton.Enabled = true;
                    targetLanguageComboBox.Enabled = true;
                    detectPathsButton.Enabled = true;
                    resetPathsButton.Enabled = true;
                    openReleaseButton.Enabled = true;
                    openLogsButton.Enabled = true;
                    cleanupButton.Enabled = true;
#if TEST_BUILD
                    debugBuildReleaseButton.Enabled = true;
                    preflightCheckButton.Enabled = true;
                    restoreBackupButton.Enabled = true;
                    buildReleaseButton.Enabled = false;
                    installButton.Enabled = false;
                    chatOnlyInstallButton.Enabled = false;
                    removeButton.Enabled = false;
#else
                    debugBuildReleaseButton.Enabled = false;
                    preflightCheckButton.Enabled = true;
                    restoreBackupButton.Enabled = true;
                    buildReleaseButton.Enabled = false;
                    installButton.Enabled = true;
                    chatOnlyInstallButton.Enabled = true;
                    removeButton.Enabled = true;
#endif
                    progressBar.Value = 0;
                    downloadLabel.Text = "";
                }));
            }
            catch (Exception exception)
            {
                ShowMessageBox(
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    "처리되지 않은 예외가 발생했어요.",
                    "에러 내용:",
                    exception.ToString());
                CloseForm();
                return;
            }
        }

        private string SelectGameDirectory(string title)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.DefaultExt = "exe";
                dialog.Filter = "FFXIV|ffxiv_dx11.exe";
                dialog.Multiselect = false;
                dialog.Title = title;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                return CheckTargetDir(Path.GetFullPath(Path.GetDirectoryName(dialog.FileName)));
            }
        }

        private void globalPathBrowseButton_Click(object sender, EventArgs e)
        {
            string selectedDir = SelectGameDirectory("글로벌 서버 클라이언트 ffxiv_dx11.exe 파일을 선택해주세요...");
            if (string.IsNullOrEmpty(selectedDir))
            {
                MessageBox.Show("선택된 글로벌 서버 클라이언트 경로가 올바르지 않아요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            targetDir = selectedDir;
            RefreshTargetVersion();
            UpdatePathTextBoxes();
            UpdateStatusLabel(string.IsNullOrEmpty(koreaSourceDir)
                ? "한국 서버 클라이언트 경로를 확인하거나 수동으로 지정해주세요."
                : $"버전 {targetVersion}, 글로벌/한국 서버 클라이언트 경로 설정 완료");
            SetActionButtonsEnabled(true);
        }

        private void koreaPathBrowseButton_Click(object sender, EventArgs e)
        {
            string selectedDir = SelectGameDirectory("한국 서버 클라이언트 ffxiv_dx11.exe 파일을 선택해주세요...");
            if (string.IsNullOrEmpty(selectedDir))
            {
                MessageBox.Show("선택된 한국 서버 클라이언트 경로가 올바르지 않아요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            koreaSourceDir = selectedDir;
            UpdatePathTextBoxes();
            UpdateStatusLabel(string.IsNullOrEmpty(targetDir)
                ? "글로벌 서버 클라이언트 경로를 확인하거나 수동으로 지정해주세요."
                : $"버전 {targetVersion}, 글로벌/한국 서버 클라이언트 경로 설정 완료");
            if (!string.IsNullOrEmpty(targetDir))
            {
                SetActionButtonsEnabled(true);
            }
        }

        private void targetLanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (targetLanguageComboBox.SelectedIndex == 1)
            {
                targetLanguageCode = "en";
                targetLanguageDisplayName = "영어";
            }
            else
            {
                targetLanguageCode = "ja";
                targetLanguageDisplayName = "일본어";
            }

            serverPatchAvailable = false;
            if (statusLabel != null && statusLabel.IsHandleCreated)
            {
                UpdateStatusLabel("베이스 클라이언트 언어: " + targetLanguageDisplayName + " (" + targetLanguageCode + "), 로컬 생성 모드");
            }
        }

        private void detectPathsButton_Click(object sender, EventArgs e)
        {
            targetDir = string.Empty;
            koreaSourceDir = string.Empty;
            targetVersion = string.Empty;
            releaseOutputDir = string.Empty;

            UpdateStatusLabel("경로 자동 탐색 중...");
            DetectGlobalClient();
            DetectKoreaClient();
            RefreshTargetVersion();
            UpdatePathTextBoxes();

            if (string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(koreaSourceDir))
            {
                UpdateStatusLabel("자동 탐색 완료: 찾지 못한 경로는 수동으로 지정해주세요.", true);
            }
            else
            {
                UpdateStatusLabel("자동 탐색 완료: 글로벌/한국 서버 클라이언트 경로 설정됨");
            }

            SetActionButtonsEnabled(true);
        }

        private void resetPathsButton_Click(object sender, EventArgs e)
        {
            targetDir = string.Empty;
            koreaSourceDir = string.Empty;
            targetVersion = string.Empty;
            serverVersion = string.Empty;
            serverPatchAvailable = false;
            releaseOutputDir = string.Empty;
            useDebugApplyPath = false;

            UpdatePathTextBoxes();
            UpdateStatusLabel("경로를 리셋했어요. 자동 탐색 또는 수동 변경을 사용해주세요.");
            UpdateDownloadLabel("");
            SetProgressValue(0);
            SetActionButtonsEnabled(true);
        }

        private void openReleaseButton_Click(object sender, EventArgs e)
        {
            OpenFolder(ResolveReleaseFolderToOpen(), "열 수 있는 생성 release 폴더가 아직 없습니다.");
        }

        private void openLogsButton_Click(object sender, EventArgs e)
        {
            string logRoot = GetLogRootDir();
            Directory.CreateDirectory(logRoot);
            OpenFolder(logRoot, "로그 폴더가 아직 없습니다.");
        }

        private void cleanupButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "generated-release, backups, logs에서 30일 지난 파일과 빈 폴더를 정리할까요?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                List<string> logLines = new List<string>();
                int deleted = CleanupOldManagedFiles(30, logLines);
                string logPath = WriteOperationLog("cleanup", logLines);
                MessageBox.Show(
                    "정리가 완료되었어요." + Environment.NewLine + Environment.NewLine +
                    "삭제한 항목: " + deleted + Environment.NewLine +
                    "로그: " + logPath,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                string logPath = WriteOperationLog("cleanup-error", new string[] { exception.ToString() });
                MessageBox.Show(
                    "정리 중 오류가 발생했어요." + Environment.NewLine + Environment.NewLine +
                    "로그: " + logPath + Environment.NewLine +
                    exception,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void restoreBackupButton_Click(object sender, EventArgs e)
        {
#if TEST_BUILD
            MessageBox.Show("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더를 변경할 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
#else
            if (!ConfirmActualClientWrite("백업으로 복구"))
            {
                return;
            }

            string backupDir = SelectBackupDirectory();
            if (string.IsNullOrEmpty(backupDir))
            {
                MessageBox.Show("복구할 백업을 찾지 못했거나 선택하지 않았어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(
                "선택한 백업으로 글로벌 서버 클라이언트 파일을 되돌릴까요?" + Environment.NewLine + Environment.NewLine +
                backupDir,
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                RestoreSelectedBackup(backupDir);
            }
            catch (Exception exception)
            {
                string logPath = WriteOperationLog("backup-restore-error", new string[] { "Backup: " + backupDir, exception.ToString() });
                MessageBox.Show(
                    "백업 복구 중 오류가 발생했어요." + Environment.NewLine + Environment.NewLine +
                    "로그: " + logPath + Environment.NewLine +
                    exception,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
#endif
        }

        private void StartLocalPatchBuild(bool includeTextPatch, bool includeFontPatch, bool debugApply)
        {
            if (buildReleaseWorker.IsBusy)
            {
                return;
            }

            buildTextPatch = includeTextPatch;
            buildFontPatch = includeFontPatch;
            useDebugApplyPath = debugApply;
            SetActionButtonsEnabled(false);
            buildReleaseWorker.RunWorkerAsync();
        }

        private void buildReleaseButton_Click(object sender, EventArgs e)
        {
#if TEST_BUILD
            MessageBox.Show("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더에 적용할 수 없어요. 테스트 자동 패치를 사용해주세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
#else
            if (!ConfirmActualClientWrite("한국 서버 클라이언트로 자동 패치"))
            {
                return;
            }

            StartLocalPatchBuild(true, true, false);
#endif
        }

        private void debugBuildReleaseButton_Click(object sender, EventArgs e)
        {
            StartLocalPatchBuild(true, true, true);
        }

        private void installButton_Click(object sender, EventArgs e)
        {
#if TEST_BUILD
            MessageBox.Show("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더에 설치할 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
#else
            if (!ConfirmActualClientWrite("전체 한글 패치"))
            {
                return;
            }

            StartLocalPatchBuild(true, true, false);
#endif
        }

        private void chatOnlyInstallButton_Click(object sender, EventArgs e)
        {
#if TEST_BUILD
            MessageBox.Show("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더에 설치할 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
#else
            if (!ConfirmActualClientWrite("한글 폰트 패치"))
            {
                return;
            }

            StartLocalPatchBuild(false, true, false);
#endif
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
#if TEST_BUILD
            MessageBox.Show("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더를 변경할 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
#else
            if (!ConfirmActualClientWrite("한글 패치 제거"))
            {
                return;
            }

            // Block further inputs.
            SetActionButtonsEnabled(false);

            // Start the background worker to remove the korean patch.
            removeWorker.RunWorkerAsync();
#endif
        }

        private void preflightCheckButton_Click(object sender, EventArgs e)
        {
            SetActionButtonsEnabled(false);
            preflightWorker.RunWorkerAsync();
        }

        private string[] GetSelectedPatchFiles()
        {
            List<string> files = new List<string>();
            if (buildTextPatch)
            {
                files.AddRange(textPatchFiles);
            }

            if (buildFontPatch)
            {
                files.AddRange(fontPatchFiles);
            }

            return files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void BuildReleaseWork()
        {
            StringBuilder processOutput = new StringBuilder();
            object outputLock = new object();
            List<string> logLines = new List<string>();

            try
            {
#if TEST_BUILD
                useDebugApplyPath = true;
#endif
                string[] selectedPatchFiles = GetSelectedPatchFiles();
                if (selectedPatchFiles.Length == 0)
                {
                    throw new InvalidOperationException("생성할 패치 종류가 선택되지 않았어요.");
                }

                if (string.IsNullOrEmpty(targetDir))
                {
                    throw new DirectoryNotFoundException("글로벌 서버 클라이언트 경로가 설정되지 않았어요.");
                }

                UpdateStatusLabel("한국 서버 클라이언트를 찾는 중...");
                DetectKoreaClient();
                UpdatePathTextBoxes();

                if (string.IsNullOrEmpty(koreaSourceDir))
                {
                    throw new DirectoryNotFoundException("한국 서버 클라이언트를 자동으로 찾을 수 없어요.");
                }

                RefreshTargetVersion();
                List<string> baselineLines = new List<string>();
                string baselineDir;
                TryCreateLocalRestoreBaseline(baselineLines, out baselineDir);
                logLines.AddRange(baselineLines);

                string patchGeneratorPath = FindPatchGeneratorPath();
                if (string.IsNullOrEmpty(patchGeneratorPath))
                {
                    throw new FileNotFoundException("FFXIVPatchGenerator.exe를 찾을 수 없어요. FFXIVPatchGenerator를 먼저 빌드하거나 UI 실행 파일과 같은 폴더에 FFXIVPatchGenerator.exe를 넣어주세요.");
                }

                releaseOutputDir = CreateManagedReleaseDirForRun();
                Directory.CreateDirectory(releaseOutputDir);

                string arguments =
                    "--global " + QuoteArgument(targetDir) + " " +
                    "--korea " + QuoteArgument(koreaSourceDir) + " " +
                    "--target-language " + targetLanguageCode + " " +
                    "--output " + QuoteArgument(releaseOutputDir);
                if (buildFontPatch)
                {
                    arguments += " --include-font";
                }

                if (!buildTextPatch && buildFontPatch)
                {
                    arguments += " --font-only";
                }

#if TEST_BUILD
                arguments += " --allow-patched-global";
#endif
                logLines.Add("FFXIVPatchGenerator: " + patchGeneratorPath);
                logLines.Add("Arguments: " + arguments);

                string buildDescription = buildTextPatch && buildFontPatch
                    ? targetLanguageDisplayName + " 클라이언트용 전체 패치"
                    : "한글 폰트 패치";
                UpdateStatusLabel(buildDescription + " 생성 중...");
                UpdateDownloadLabel("0%");
                SetProgressValue(0);

                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo()
                    {
                        FileName = patchGeneratorPath,
                        Arguments = arguments,
                        WorkingDirectory = Path.GetDirectoryName(patchGeneratorPath),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        CreateNoWindow = true
                    };

                    DataReceivedEventHandler outputHandler = (processSender, eventArgs) =>
                    {
                        if (string.IsNullOrEmpty(eventArgs.Data)) return;

                        lock (outputLock)
                        {
                            logLines.Add(eventArgs.Data);
                            processOutput.AppendLine(eventArgs.Data);
                        }

                        if (TryHandleBuilderProgress(eventArgs.Data)) return;

                        if (eventArgs.Data.StartsWith("  Patched EXD pages:", StringComparison.OrdinalIgnoreCase)
                            || eventArgs.Data.StartsWith("Using base", StringComparison.OrdinalIgnoreCase)
                            || eventArgs.Data.StartsWith("Writing output:", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateStatusLabel("패치 release 생성 중... " + eventArgs.Data.Trim());
                        }
                    };

                    process.OutputDataReceived += outputHandler;
                    process.ErrorDataReceived += outputHandler;

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string output;
                        lock (outputLock)
                        {
                            output = processOutput.ToString();
                        }

                        throw new Exception(FormatPatchGeneratorFailure(output));
                    }
                }

                ValidateReleaseOutput(releaseOutputDir, selectedPatchFiles);
                logLines.Add("Release validation: OK");

                string applyGameDir = GetApplyGameDir();
                string applySqpackDir = Path.Combine(applyGameDir, "sqpack", "ffxiv");
                Directory.CreateDirectory(applySqpackDir);

                UpdateStatusLabel(useDebugApplyPath ? "생성된 패치 테스트 경로에 적용 중..." : "생성된 패치 적용 중...");
                UpdateDownloadLabel("");
                SetProgressValue(100);

                string backupDir = string.Empty;
                if (!useDebugApplyPath)
                {
                    backupDir = BackupTargetFiles(selectedPatchFiles, buildTextPatch ? "auto-apply" : "font-apply");
                }

                foreach (string patchFile in selectedPatchFiles)
                {
                    string sourcePath = Path.Combine(releaseOutputDir, patchFile);
                    if (!File.Exists(sourcePath))
                    {
                        throw new FileNotFoundException("생성된 패치 파일을 찾을 수 없어요.", sourcePath);
                    }

                    File.Copy(sourcePath, Path.Combine(applySqpackDir, patchFile), true);
                }

                ValidateFilesExist(applySqpackDir, selectedPatchFiles, "적용 대상");
                logLines.Add("Apply validation: OK");

                if (useDebugApplyPath)
                {
                    File.WriteAllText(
                        Path.Combine(releaseOutputDir, "debug-apply.txt"),
                        "This is a test apply folder. The real global client was not modified." + Environment.NewLine +
                        "Source global game: " + targetDir + Environment.NewLine +
                        "Source Korean game: " + koreaSourceDir + Environment.NewLine +
                        "Debug apply game: " + applyGameDir + Environment.NewLine,
                        Encoding.UTF8);
                }

                string manifestPath = WriteManifest(releaseOutputDir, applyGameDir, useDebugApplyPath, backupDir);
                logLines.Add("Manifest: " + manifestPath);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    logLines.Add("Backup: " + backupDir);
                }

                string logPath = WriteOperationLog("build-release", logLines);

                ShowMessageBox(
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    useDebugApplyPath ? "패치 생성과 테스트 적용이 완료되었어요." : "패치 생성과 설치가 완료되었어요.",
                    "로그: " + logPath,
                    "Manifest: " + manifestPath,
                    (useDebugApplyPath ? applyGameDir : releaseOutputDir) +
                    (string.IsNullOrEmpty(backupDir) ? "" : Environment.NewLine + "백업 위치: " + backupDir));
                CloseForm();
            }
            catch (Exception exception)
            {
                UpdateDownloadLabel("");
                logLines.Add("ERROR:");
                logLines.Add(exception.ToString());
                string logPath = WriteOperationLog("build-release-error", logLines);
                ShowMessageBox(
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    "패치 release 생성에 실패했어요.",
                    "로그: " + logPath,
                    "에러 내용:",
                    exception.ToString());
                CloseForm();
            }
            finally
            {
                SetProgressMarquee(false);
            }
        }

        private void DownloadWork(string[] patchFiles, BackgroundWorker worker, bool isRemove = false)
        {
            List<string> logLines = new List<string>();

            try
            {
#if TEST_BUILD
                throw new InvalidOperationException("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더에 파일을 복사할 수 없어요.");
#else
                if (!isRemove)
                {
                    throw new InvalidOperationException("다운로드 설치 모드는 더 이상 사용하지 않아요. 전체 한글 패치 또는 한글 폰트 패치 버튼은 로컬 생성기를 통해 처리됩니다.");
                }

                string restoreSourceDir;
                string localBackupDir;
                if (RestoreFromLocalOrigIndexes(out restoreSourceDir, out localBackupDir))
                {
                    logLines.Add("Local restore source: " + restoreSourceDir);
                    logLines.Add("Backup before restore: " + localBackupDir);
                    string localRestoreLogPath = WriteOperationLog("remove-local-restore", logLines);
                    ShowMessageBox(
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information,
                        "로컬 orig index로 한글 패치를 제거했어요.",
                        "로그: " + localRestoreLogPath,
                        "복구 원본: " + restoreSourceDir +
                        (string.IsNullOrEmpty(localBackupDir) ? "" : Environment.NewLine + "백업 위치: " + localBackupDir));
                    CloseForm();
                    return;
                }

                throw new InvalidOperationException(
                    "로컬 복구용 orig index를 찾지 못했어요." + Environment.NewLine +
                    "이전 패쳐로 적용한 패치라면 그 패쳐의 제거 기능을 먼저 사용하거나, FFXIV 런처 파일 검사/복구로 글로벌 서버 클라이언트를 원본 상태로 되돌려주세요.");
#endif
            }
            catch (Exception exception)
            {
                logLines.Add("ERROR:");
                logLines.Add(exception.ToString());
                string logPath = WriteOperationLog(isRemove ? "remove-error" : "install-error", logLines);
                ShowMessageBox(
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    "처리되지 않은 예외가 발생했어요.",
                    "로그: " + logPath,
                    "에러 내용:",
                    exception.ToString());
                CloseForm();
                return;
            }
        }

        private void installWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadWork(textPatchFiles.Concat(fontPatchFiles).ToArray(), installWorker);
        }

        private void buildReleaseWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BuildReleaseWork();
        }

        private void preflightWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            PreflightCheckWork();
        }

        private void chatOnlyWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadWork(fontPatchFiles, chatOnlyWorker);
        }

        private void removeWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadWork(restoreFiles, removeWorker, true);
        }

        #endregion
    }

    // Extending HTTP client stream to report download progress while copying.
    public static class StreamExtensions
    {
        // Extending CopyToAsync to accept interface that reports an integer progress.
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, long totalLength, IProgress<int> progress)
        {
            // Check parameters.
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead) throw new ArgumentException("Has to be readable.", nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite) throw new ArgumentException("Has to be writable.", nameof(destination));
            if (bufferSize < 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            // Make a buffer with given buffer size.
            byte[] buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            int progressReport = 0;

            // Fill buffer.
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                // Write buffer to destination.
                await destination.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                // Up the total counter.
                totalBytesRead += bytesRead;
                int newProgressReport = (int)(totalBytesRead * 100 / totalLength);

                // Only report if progress became higher.
                if (newProgressReport > progressReport)
                {
                    // Report the progress.
                    progressReport = newProgressReport;
                    progress.Report(progressReport);
                }
            }
        }
    }
}
