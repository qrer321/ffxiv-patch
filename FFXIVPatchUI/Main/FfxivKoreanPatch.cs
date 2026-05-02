// 이 프로젝트는 FFXIV 한글 패치 원작자 https://github.com/korean-patch 의 작업을 참고했습니다.
// 한글 패치의 기반과 구현 흐름을 만들어주신 원작자에게 감사드립니다.

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
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace FFXIVKoreanPatch.Main
{
    public partial class FFXIVKoreanPatch : Form
    {
        #region Variables

        private const string patchDisplayName = "FFXIV 글로벌 클라이언트용 한글 패치";

        // Generator stdout protocol. Progress lines are "prefix + percent|message".
        private const string builderProgressPrefix = "@@FFXIVPATCHGENERATOR_PROGRESS|";

        // Single-exe release payloads embedded into FFXIVKoreanPatch.exe.
        private const string embeddedGeneratorResourceName = "EmbeddedPayloads.FFXIVPatchGenerator.exe";
        private const string embeddedTtmpMpdResourceName = "EmbeddedPayloads.TTMPD.mpd";
        private const string embeddedTtmpMplResourceName = "EmbeddedPayloads.TTMPL.mpl";

        // Local backup folder kept outside release output so users can manually restore sqpack files.
        private const string manualRollbackDirName = "manual-sqpack-rollback";

        // Main executable name used only for duplicate patcher process detection.
        private const string mainFileName = "FFXIVKoreanPatch";

        // Release builds block patching while these FFXIV processes may have sqpack files open.
        private string[] gameProcessNames = new string[]
        {
            "ffxivboot", "ffxivboot64",
            "ffxivlauncher", "ffxivlauncher64",
            "ffxiv", "ffxiv_dx11"
        };

        // Minimal files that identify a valid FFXIV game folder for this patcher.
        private string[] requiredFiles = new string[]
        {
            "ffxiv_dx11.exe",
            "ffxivgame.ver",
            "sqpack/ffxiv/000000.win32.index",
            "sqpack/ffxiv/000000.win32.index2",
            "sqpack/ffxiv/000000.win32.dat0",
            "sqpack/ffxiv/060000.win32.index",
            "sqpack/ffxiv/060000.win32.index2",
            "sqpack/ffxiv/060000.win32.dat0",
            "sqpack/ffxiv/0a0000.win32.index",
            "sqpack/ffxiv/0a0000.win32.index2",
            "sqpack/ffxiv/0a0000.win32.dat0"
        };

        // Fallback install-location guesses checked under common drive roots when registry detection fails.
        private string[] knownGameDirLocations = new string[]
        {
            "SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "Program Files/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game",
            "SteamLibrary/steamapps/common/FINAL FANTASY XIV Online/game",
            "Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game",
            "Program Files/Steam/steamapps/common/FINAL FANTASY XIV Online/game"
        };

        // Korean client has different publisher/install names, so it needs its own fallback list.
        private string[] knownKoreaGameDirLocations = new string[]
        {
            "FINAL FANTASY XIV - KOREA/game",
            "Program Files (x86)/FINAL FANTASY XIV - KOREA/game",
            "Program Files/FINAL FANTASY XIV - KOREA/game",
            "SquareEnix/FINAL FANTASY XIV - KOREA/game",
            "Program Files (x86)/SquareEnix/FINAL FANTASY XIV - KOREA/game",
            "Program Files/SquareEnix/FINAL FANTASY XIV - KOREA/game"
        };

        // 000000 is the common package; global font resources live under common/font there.
        private string[] fontPatchFiles = new string[]
        {
            "000000.win32.dat1",
            "000000.win32.index",
            "000000.win32.index2"
        };

        // 060000 contains UI layout and icon resources. UI patch output is written to dat4.
        private string[] uiPatchFiles = new string[]
        {
            "060000.win32.dat4",
            "060000.win32.index",
            "060000.win32.index2"
        };

        // 0a0000 is the Excel/EXD text package rewritten for the selected global language slot.
        private string[] textPatchFiles = new string[]
        {
            "0a0000.win32.dat1",
            "0a0000.win32.index",
            "0a0000.win32.index2"
        };

        // Removal restores clean index/index2 files; leftover patch dat files are harmless once unreferenced.
        private string[] restoreFiles = new string[]
        {
            "000000.win32.index",
            "000000.win32.index2",
            "060000.win32.index",
            "060000.win32.index2",
            "0a0000.win32.index",
            "0a0000.win32.index2"
        };

        // Registry Scancode Map: maps Right Alt (E0 38) to Hangul key (00 72) for Korean chat IME toggle.
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

        // Global EXD language suffix to rewrite. Default is ja because the primary target is Japanese client.
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
        private bool initialPreflightStarted;
        private bool lastPreflightPassed;
        private Label debugFontProfileLabel;
        private ComboBox debugFontProfileComboBox;

        // Target client version.
        private string targetVersion = string.Empty;

        #endregion

        public FFXIVKoreanPatch()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

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
            InitializeDebugFontProfileControls();

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

        private void InitializeDebugFontProfileControls()
        {
            debugFontProfileLabel = new Label();
            debugFontProfileLabel.Name = "debugFontProfileLabel";
            debugFontProfileLabel.Text = "테스트 폰트 프로필";
            debugFontProfileLabel.AutoSize = false;
            debugFontProfileLabel.BackColor = Color.Transparent;

            debugFontProfileComboBox = new ComboBox();
            debugFontProfileComboBox.Name = "debugFontProfileComboBox";
            debugFontProfileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            debugFontProfileComboBox.Items.Add(new FontProfileItem("기본", "full"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("TrumpGothic 제외", "no-trumpgothic"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("UI 숫자 보호", "ui-numeric-safe"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("MiedingerMid 제외", "no-miedingermid"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("Jupiter 제외", "no-jupiter"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("AXIS 제외", "no-axis"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("FDT만 적용", "fdt-only"));
            debugFontProfileComboBox.Items.Add(new FontProfileItem("텍스처만 적용", "textures-only"));
            debugFontProfileComboBox.SelectedIndex = 0;

#if !TEST_BUILD
            debugFontProfileLabel.Visible = false;
            debugFontProfileComboBox.Visible = false;
#endif

            Controls.Add(debugFontProfileLabel);
            Controls.Add(debugFontProfileComboBox);
        }

        private sealed class FontProfileItem
        {
            public readonly string DisplayName;
            public readonly string Value;

            public FontProfileItem(string displayName, string value)
            {
                DisplayName = displayName;
                Value = value;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (SolidBrush veil = new SolidBrush(Color.FromArgb(76, 7, 10, 14)))
            {
                e.Graphics.FillRectangle(veil, 0, 0, ClientSize.Width, 210);
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, ClientSize.Width, 280),
                Color.FromArgb(120, 18, 33, 44),
                Color.FromArgb(8, 18, 21, 26),
                90f))
            {
                e.Graphics.FillRectangle(brush, 0, 0, ClientSize.Width, 280);
            }

            DrawSurface(e.Graphics, new Rectangle(24, 240, 852, 285));
            DrawSurface(e.Graphics, new Rectangle(24, 542, 852, 104));
            DrawSurface(e.Graphics, new Rectangle(24, 662, 852, 112));

            using (SolidBrush accent = new SolidBrush(Color.FromArgb(76, 169, 232)))
            {
                e.Graphics.FillRectangle(accent, 42, 255, 4, 22);
                e.Graphics.FillRectangle(accent, 42, 557, 4, 22);
                e.Graphics.FillRectangle(accent, 42, 677, 4, 22);
            }

            DrawModeBadge(e.Graphics);
        }

        private void DrawSurface(Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = CreateRoundedRectangle(bounds, 8))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(232, 24, 28, 34)))
            using (Pen border = new Pen(Color.FromArgb(72, 102, 118, 138)))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
        }

        private void DrawModeBadge(Graphics graphics)
        {
            Rectangle bounds = new Rectangle(758, 38, 118, 32);
            Color fillColor;
            Color borderColor;
#if TEST_BUILD
            fillColor = Color.FromArgb(82, 64, 39, 18);
            borderColor = Color.FromArgb(230, 174, 83);
#else
            fillColor = Color.FromArgb(74, 24, 64, 45);
            borderColor = Color.FromArgb(86, 190, 128);
#endif

            using (GraphicsPath path = CreateRoundedRectangle(bounds, 8))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
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

        private void StyleCheckBox(CheckBox checkBox)
        {
            checkBox.BackColor = Color.Transparent;
            checkBox.ForeColor = Color.FromArgb(220, 227, 236);
            checkBox.Font = new Font("맑은 고딕", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 129);
            checkBox.Cursor = Cursors.Hand;
            checkBox.FlatStyle = FlatStyle.Flat;
        }

        private void StyleButton(Button button, Color backColor, Color borderColor, Color hoverColor)
        {
            button.AutoSize = false;
            button.Dock = DockStyle.None;
            button.BackColor = backColor;
            button.ForeColor = Color.FromArgb(246, 248, 250);
            button.Cursor = Cursors.Hand;
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

            ClientSize = new Size(900, 790);
            BackColor = Color.FromArgb(18, 21, 26);
            ForeColor = Color.FromArgb(240, 243, 247);
            Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

            Font eyebrowFont = new Font("맑은 고딕", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 129);
            Font titleFont = new Font("맑은 고딕", 22F, FontStyle.Bold, GraphicsUnit.Point, 129);
            Font sectionFont = new Font("맑은 고딕", 10F, FontStyle.Bold, GraphicsUnit.Point, 129);
            Font smallFont = new Font("맑은 고딕", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 129);

            CreateLayoutLabel("eyebrowLabel", "GLOBAL CLIENT PATCHER", 34, 30, 260, 22, eyebrowFont, Color.FromArgb(166, 218, 255));
            CreateLayoutLabel("titleLabel", "FFXIV 한글 패치", 32, 56, 480, 52, titleFont, Color.White);
            CreateLayoutLabel("subtitleLabel", "일본어 / 영어 클라이언트", 36, 108, 320, 24, smallFont, Color.FromArgb(220, 227, 236));
            CreateLayoutLabel(
                "modeLabel",
#if TEST_BUILD
                "TEST BUILD",
#else
                "RELEASE",
#endif
                758,
                40,
                118,
                28,
                smallFont,
#if TEST_BUILD
                Color.FromArgb(255, 202, 113)
#else
                Color.FromArgb(125, 220, 151)
#endif
                );
            CreateLayoutLabel("pathsSectionLabel", "클라이언트", 54, 254, 180, 24, sectionFont, Color.FromArgb(236, 241, 247));
            CreateLayoutLabel("actionsSectionLabel", "패치 작업", 54, 556, 180, 24, sectionFont, Color.FromArgb(236, 241, 247));
            CreateLayoutLabel("statusSectionLabel", "상태", 54, 676, 180, 24, sectionFont, Color.FromArgb(236, 241, 247));

            StyleFieldLabel(globalPathLabel);
            StyleFieldLabel(koreaPathLabel);
            StyleFieldLabel(targetLanguageLabel);
            StyleFieldLabel(debugFontProfileLabel);
            StyleTextBox(globalPathTextBox);
            StyleTextBox(koreaPathTextBox);
            StyleComboBox(targetLanguageComboBox);
            StyleComboBox(debugFontProfileComboBox);

            Color neutral = Color.FromArgb(42, 50, 61);
            Color neutralBorder = Color.FromArgb(82, 96, 112);
            Color neutralHover = Color.FromArgb(56, 67, 80);
            Color primary = Color.FromArgb(34, 126, 188);
            Color primaryBorder = Color.FromArgb(90, 184, 235);
            Color primaryHover = Color.FromArgb(42, 145, 214);
            Color success = Color.FromArgb(32, 139, 94);
            Color successBorder = Color.FromArgb(91, 204, 148);
            Color successHover = Color.FromArgb(40, 160, 110);
            Color caution = Color.FromArgb(151, 111, 38);
            Color cautionBorder = Color.FromArgb(218, 169, 70);
            Color cautionHover = Color.FromArgb(174, 129, 46);
            Color danger = Color.FromArgb(147, 59, 67);
            Color dangerBorder = Color.FromArgb(218, 101, 110);
            Color dangerHover = Color.FromArgb(171, 70, 79);

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

            PlaceControl(globalPathLabel, 42, 290, 220, 20);
            PlaceControl(globalPathTextBox, 42, 314, 720, 28);
            PlaceControl(globalPathBrowseButton, 778, 313, 80, 30);

            PlaceControl(koreaPathLabel, 42, 354, 220, 20);
            PlaceControl(koreaPathTextBox, 42, 378, 720, 28);
            PlaceControl(koreaPathBrowseButton, 778, 377, 80, 30);

            PlaceControl(targetLanguageLabel, 42, 418, 220, 20);
            PlaceControl(targetLanguageComboBox, 42, 442, 260, 30);
            PlaceControl(detectPathsButton, 318, 441, 170, 32);
            PlaceControl(resetPathsButton, 500, 441, 132, 32);
            PlaceControl(debugFontProfileLabel, 646, 418, 212, 20);
            PlaceControl(debugFontProfileComboBox, 646, 442, 212, 30);

            PlaceControl(openReleaseButton, 42, 484, 194, 32);
            PlaceControl(openLogsButton, 248, 484, 184, 32);
            PlaceControl(cleanupButton, 444, 484, 190, 32);
            PlaceControl(restoreBackupButton, 646, 484, 212, 32);

#if TEST_BUILD
            PlaceControl(preflightCheckButton, 42, 590, 404, 42);
            PlaceControl(debugBuildReleaseButton, 458, 590, 400, 42);
#else
            PlaceControl(preflightCheckButton, 42, 590, 238, 42);
            PlaceControl(installButton, 294, 590, 178, 42);
            PlaceControl(chatOnlyInstallButton, 486, 590, 178, 42);
            PlaceControl(removeButton, 678, 590, 180, 42);
#endif

            statusLabel.AutoSize = false;
            statusLabel.Dock = DockStyle.None;
            statusLabel.BackColor = Color.FromArgb(30, 36, 44);
            statusLabel.BorderStyle = BorderStyle.None;
            statusLabel.ForeColor = Color.White;
            statusLabel.Padding = new Padding(12, 0, 12, 0);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            PlaceControl(statusLabel, 42, 706, 816, 34);

            downloadLabel.AutoSize = false;
            downloadLabel.Dock = DockStyle.None;
            downloadLabel.BackColor = Color.Transparent;
            downloadLabel.ForeColor = Color.FromArgb(198, 205, 214);
            downloadLabel.Padding = new Padding(0);
            downloadLabel.TextAlign = ContentAlignment.MiddleLeft;
            PlaceControl(downloadLabel, 42, 741, 816, 18);

            progressBar.Dock = DockStyle.None;
            PlaceControl(progressBar, 42, 764, 816, 8);

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

        private void ShowPreflightResultDialog(string summary, int failures, int warnings, string logPath, IList<string> lines)
        {
            Invoke(new Action(() =>
            {
                using (Form dialog = new Form())
                {
                    dialog.Text = "사전 점검 결과";
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = false;
                    dialog.ClientSize = new Size(760, 560);
                    dialog.BackColor = Color.FromArgb(20, 24, 30);
                    dialog.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

                    Color accent = failures > 0
                        ? Color.FromArgb(235, 92, 92)
                        : (warnings > 0 ? Color.FromArgb(232, 184, 92) : Color.FromArgb(86, 190, 128));

                    TableLayoutPanel root = new TableLayoutPanel();
                    root.Dock = DockStyle.Fill;
                    root.Padding = new Padding(18);
                    root.BackColor = dialog.BackColor;
                    root.ColumnCount = 1;
                    root.RowCount = 5;
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                    dialog.Controls.Add(root);

                    Label titleLabel = new Label();
                    titleLabel.Dock = DockStyle.Fill;
                    titleLabel.AutoSize = false;
                    titleLabel.Text = summary;
                    titleLabel.ForeColor = accent;
                    titleLabel.Font = new Font("맑은 고딕", 15F, FontStyle.Bold, GraphicsUnit.Point, 129);
                    titleLabel.TextAlign = ContentAlignment.MiddleLeft;
                    root.Controls.Add(titleLabel, 0, 0);

                    FlowLayoutPanel statPanel = new FlowLayoutPanel();
                    statPanel.Dock = DockStyle.Fill;
                    statPanel.FlowDirection = FlowDirection.LeftToRight;
                    statPanel.WrapContents = false;
                    statPanel.BackColor = dialog.BackColor;
                    root.Controls.Add(statPanel, 0, 1);

                    AddPreflightStatLabel(statPanel, "실패 " + failures.ToString(), failures > 0 ? Color.FromArgb(94, 36, 40) : Color.FromArgb(32, 38, 46), failures > 0 ? Color.FromArgb(255, 185, 185) : Color.FromArgb(190, 198, 208));
                    AddPreflightStatLabel(statPanel, "주의 " + warnings.ToString(), warnings > 0 ? Color.FromArgb(86, 66, 28) : Color.FromArgb(32, 38, 46), warnings > 0 ? Color.FromArgb(255, 222, 150) : Color.FromArgb(190, 198, 208));
                    AddPreflightStatLabel(statPanel, "전체 " + (lines == null ? 0 : lines.Count).ToString(), Color.FromArgb(32, 38, 46), Color.FromArgb(220, 226, 234));

                    ListView listView = new ListView();
                    listView.Dock = DockStyle.Fill;
                    listView.View = View.Details;
                    listView.FullRowSelect = true;
                    listView.GridLines = false;
                    listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
                    listView.BackColor = Color.FromArgb(26, 31, 38);
                    listView.ForeColor = Color.FromArgb(230, 235, 242);
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    listView.Columns.Add("상태", 76);
                    listView.Columns.Add("점검 항목", 245);
                    listView.Columns.Add("상세", 395);

                    foreach (string rawLine in lines ?? new string[0])
                    {
                        string state = "정보";
                        string text = rawLine ?? string.Empty;
                        Color rowColor = Color.FromArgb(215, 222, 230);

                        if (text.StartsWith("[OK]", StringComparison.OrdinalIgnoreCase))
                        {
                            state = "OK";
                            text = text.Substring(4).Trim();
                            rowColor = Color.FromArgb(140, 220, 165);
                        }
                        else if (text.StartsWith("[주의]", StringComparison.OrdinalIgnoreCase))
                        {
                            state = "주의";
                            text = text.Substring(4).Trim();
                            rowColor = Color.FromArgb(255, 218, 135);
                        }
                        else if (text.StartsWith("[실패]", StringComparison.OrdinalIgnoreCase))
                        {
                            state = "실패";
                            text = text.Substring(4).Trim();
                            rowColor = Color.FromArgb(255, 170, 170);
                        }

                        string item = text;
                        string detail = string.Empty;
                        int colon = text.IndexOf(':');
                        if (colon > 0 && colon < text.Length - 1)
                        {
                            item = text.Substring(0, colon).Trim();
                            detail = text.Substring(colon + 1).Trim();
                        }

                        ListViewItem listItem = new ListViewItem(state);
                        listItem.SubItems.Add(item);
                        listItem.SubItems.Add(detail);
                        listItem.ForeColor = rowColor;
                        listView.Items.Add(listItem);
                    }

                    root.Controls.Add(listView, 0, 2);

                    Label logLabel = new Label();
                    logLabel.Dock = DockStyle.Fill;
                    logLabel.AutoSize = false;
                    logLabel.Text = "로그: " + logPath;
                    logLabel.ForeColor = Color.FromArgb(178, 188, 200);
                    logLabel.TextAlign = ContentAlignment.MiddleLeft;
                    root.Controls.Add(logLabel, 0, 3);

                    FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
                    buttonPanel.Dock = DockStyle.Fill;
                    buttonPanel.FlowDirection = FlowDirection.RightToLeft;
                    buttonPanel.WrapContents = false;
                    buttonPanel.BackColor = dialog.BackColor;
                    root.Controls.Add(buttonPanel, 0, 4);

                    Button closeButton = CreateDialogButton("닫기", Color.FromArgb(64, 72, 84), Color.White);
                    closeButton.DialogResult = DialogResult.OK;
                    buttonPanel.Controls.Add(closeButton);

                    Button openLogButton = CreateDialogButton("로그 열기", Color.FromArgb(58, 90, 142), Color.White);
                    openLogButton.Click += (sender, args) =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                            {
                                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
                            }
                        }
                        catch
                        {
                            MessageBox.Show("로그 파일을 열 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    };
                    buttonPanel.Controls.Add(openLogButton);

                    dialog.AcceptButton = closeButton;
                    dialog.ShowDialog(this);
                }
            }));
        }

        private void ShowOperationResultDialog(string title, string summary, string logPath, IList<KeyValuePair<string, string>> details)
        {
            Invoke(new Action(() =>
            {
                using (Form dialog = new Form())
                {
                    dialog.Text = title;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = false;
                    dialog.ClientSize = new Size(720, 440);
                    dialog.BackColor = Color.FromArgb(20, 24, 30);
                    dialog.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);

                    TableLayoutPanel root = new TableLayoutPanel();
                    root.Dock = DockStyle.Fill;
                    root.Padding = new Padding(18);
                    root.BackColor = dialog.BackColor;
                    root.ColumnCount = 1;
                    root.RowCount = 4;
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                    dialog.Controls.Add(root);

                    Label titleLabel = new Label();
                    titleLabel.Dock = DockStyle.Fill;
                    titleLabel.AutoSize = false;
                    titleLabel.Text = title + Environment.NewLine + summary;
                    titleLabel.ForeColor = Color.FromArgb(126, 208, 154);
                    titleLabel.Font = new Font("맑은 고딕", 13F, FontStyle.Bold, GraphicsUnit.Point, 129);
                    titleLabel.TextAlign = ContentAlignment.MiddleLeft;
                    root.Controls.Add(titleLabel, 0, 0);

                    ListView listView = new ListView();
                    listView.Dock = DockStyle.Fill;
                    listView.View = View.Details;
                    listView.FullRowSelect = true;
                    listView.GridLines = false;
                    listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
                    listView.BackColor = Color.FromArgb(26, 31, 38);
                    listView.ForeColor = Color.FromArgb(230, 235, 242);
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    listView.Columns.Add("항목", 170);
                    listView.Columns.Add("내용", 500);

                    foreach (KeyValuePair<string, string> detail in details ?? new KeyValuePair<string, string>[0])
                    {
                        ListViewItem item = new ListViewItem(detail.Key ?? string.Empty);
                        item.SubItems.Add(detail.Value ?? string.Empty);
                        item.ForeColor = Color.FromArgb(224, 231, 240);
                        listView.Items.Add(item);
                    }

                    root.Controls.Add(listView, 0, 1);

                    Label logLabel = new Label();
                    logLabel.Dock = DockStyle.Fill;
                    logLabel.AutoSize = false;
                    logLabel.Text = "로그: " + logPath;
                    logLabel.ForeColor = Color.FromArgb(178, 188, 200);
                    logLabel.TextAlign = ContentAlignment.MiddleLeft;
                    root.Controls.Add(logLabel, 0, 2);

                    FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
                    buttonPanel.Dock = DockStyle.Fill;
                    buttonPanel.FlowDirection = FlowDirection.RightToLeft;
                    buttonPanel.WrapContents = false;
                    buttonPanel.BackColor = dialog.BackColor;
                    root.Controls.Add(buttonPanel, 0, 3);

                    Button closeButton = CreateDialogButton("닫기", Color.FromArgb(64, 72, 84), Color.White);
                    closeButton.DialogResult = DialogResult.OK;
                    buttonPanel.Controls.Add(closeButton);

                    Button openLogButton = CreateDialogButton("로그 열기", Color.FromArgb(58, 90, 142), Color.White);
                    openLogButton.Click += (sender, args) =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                            {
                                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
                            }
                        }
                        catch
                        {
                            MessageBox.Show("로그 파일을 열 수 없어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    };
                    buttonPanel.Controls.Add(openLogButton);

                    dialog.AcceptButton = closeButton;
                    dialog.ShowDialog(this);
                }
            }));
        }

        private void AddPreflightStatLabel(FlowLayoutPanel panel, string text, Color backColor, Color foreColor)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Width = 108;
            label.Height = 28;
            label.Margin = new Padding(0, 4, 10, 4);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(label);
        }

        private Button CreateDialogButton(string text, Color backColor, Color foreColor)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 104;
            button.Height = 32;
            button.Margin = new Padding(8, 7, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(92, 104, 120);
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.12f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.12f);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            button.UseVisualStyleBackColor = false;
            return button;
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
                debugFontProfileComboBox.Enabled = enabled;
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

        private bool HasValidGlobalClient()
        {
            return !string.IsNullOrEmpty(targetDir) && CheckTargetDir(targetDir) != null;
        }

        private bool HasValidKoreaClient()
        {
            return !string.IsNullOrEmpty(koreaSourceDir) && CheckTargetDir(koreaSourceDir) != null;
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

        // Get the SHA1 checksum from a file.
        private string ComputeSHA1(string filePath)
        {
            using (SHA1CryptoServiceProvider cryptoProvider = new SHA1CryptoServiceProvider())
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return BitConverter.ToString(cryptoProvider.ComputeHash(stream)).Replace("-", "");
            }
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            Action action = () =>
            {
                bool hasGlobalClient = HasValidGlobalClient();
                bool hasKoreaClient = HasValidKoreaClient();
                bool targetAlreadyPatched = hasGlobalClient && HasPatchedTargetIndexes();
                bool canApplyToRealClient = enabled && hasGlobalClient && hasKoreaClient && lastPreflightPassed && !targetAlreadyPatched;

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
                debugBuildReleaseButton.Enabled = enabled && hasGlobalClient && hasKoreaClient;
                buildReleaseButton.Enabled = false;
                installButton.Enabled = false;
                chatOnlyInstallButton.Enabled = false;
                removeButton.Enabled = false;
#else
                debugBuildReleaseButton.Enabled = false;
                buildReleaseButton.Enabled = false;
                installButton.Enabled = canApplyToRealClient;
                chatOnlyInstallButton.Enabled = canApplyToRealClient;
                removeButton.Enabled = enabled && hasGlobalClient;
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

        private void MarkPreflightRequired()
        {
            lastPreflightPassed = false;
        }

        private string GetRuntimeDataRootDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string rootDir = string.IsNullOrEmpty(localAppData)
                ? Path.Combine(Application.CommonAppDataPath, "runtime-data")
                : Path.Combine(localAppData, "FFXIVKoreanPatch");

            Directory.CreateDirectory(rootDir);
            return rootDir;
        }

        private string GetRuntimeDataChildDir(string childName)
        {
            string childDir = Path.Combine(GetRuntimeDataRootDir(), childName);
            Directory.CreateDirectory(childDir);
            return childDir;
        }

        private string GetEmbeddedToolDir()
        {
            string versionKey = "unknown";
            try
            {
                versionKey = File.GetLastWriteTimeUtc(Application.ExecutablePath).Ticks.ToString("x");
            }
            catch
            {
                versionKey = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }

            return GetRuntimeDataChildDir(Path.Combine("embedded-tools", versionKey));
        }

        private string EnsureEmbeddedPatchGeneratorPath()
        {
            string toolDir = GetEmbeddedToolDir();
            string generatorPath = Path.Combine(toolDir, "FFXIVPatchGenerator.exe");
            bool hasGenerator = ExtractEmbeddedPayload(embeddedGeneratorResourceName, generatorPath);
            if (!hasGenerator)
            {
                return null;
            }

            ExtractEmbeddedPayload(embeddedTtmpMpdResourceName, Path.Combine(toolDir, "TTMPD.mpd"));
            ExtractEmbeddedPayload(embeddedTtmpMplResourceName, Path.Combine(toolDir, "TTMPL.mpl"));
            return generatorPath;
        }

        private static bool ExtractEmbeddedPayload(string resourceName, string destinationPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                FileInfo destinationInfo = new FileInfo(destinationPath);
                if (destinationInfo.Exists && destinationInfo.Length == resourceStream.Length)
                {
                    return true;
                }

                string tempPath = destinationPath + ".tmp";
                using (FileStream outputStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    resourceStream.CopyTo(outputStream);
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(tempPath, destinationPath);
                return true;
            }
        }

        private string FindPatchGeneratorPath()
        {
            string embeddedGeneratorPath = EnsureEmbeddedPatchGeneratorPath();
            if (!string.IsNullOrEmpty(embeddedGeneratorPath) && File.Exists(embeddedGeneratorPath))
            {
                return embeddedGeneratorPath;
            }

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

        private string FindFontPackageDir(string patchGeneratorPath)
        {
            if (string.IsNullOrEmpty(patchGeneratorPath))
            {
                return null;
            }

            string generatorDir = Path.GetDirectoryName(patchGeneratorPath);
            string[] candidateDirs = new string[]
            {
                generatorDir,
                Path.Combine(generatorDir, "FontPatchAssets")
            };

            foreach (string candidateDir in candidateDirs)
            {
                if (string.IsNullOrEmpty(candidateDir))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(candidateDir, "TTMPD.mpd")) &&
                    File.Exists(Path.Combine(candidateDir, "TTMPL.mpl")))
                {
                    return candidateDir;
                }
            }

            return null;
        }

        private string FindPatchPolicyPath(string patchGeneratorPath)
        {
            string generatorDir = string.IsNullOrEmpty(patchGeneratorPath) ? null : Path.GetDirectoryName(patchGeneratorPath);
            string[] candidatePaths = new string[]
            {
                string.IsNullOrEmpty(generatorDir) ? null : Path.Combine(generatorDir, "patch-policy.json"),
                Path.Combine(Application.StartupPath, "patch-policy.json")
            };

            foreach (string candidatePath in candidatePaths)
            {
                if (!string.IsNullOrEmpty(candidatePath) && File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }

        private string GetManagedReleaseBaseDir()
        {
            string version = string.IsNullOrWhiteSpace(targetVersion) ? "unknown" : targetVersion.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                version = version.Replace(invalidChar, '_');
            }

            return Path.Combine(GetRuntimeDataChildDir("generated-release"), targetLanguageCode, version);
        }

        private string GetRestoreBaselineBaseDir()
        {
            string version = string.IsNullOrWhiteSpace(targetVersion) ? "unknown" : targetVersion.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                version = version.Replace(invalidChar, '_');
            }

            return Path.Combine(GetRuntimeDataChildDir("restore-baseline"), targetLanguageCode, version);
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

        private string GetRestoreBaselineOrigPath(string baselineDir, string fileName)
        {
            if (string.IsNullOrEmpty(baselineDir))
            {
                return null;
            }

            string path = Path.Combine(baselineDir, "orig." + fileName);
            return File.Exists(path) ? path : null;
        }

        private static bool HasAnyPatchFile(IEnumerable<string> selectedPatchFiles, IEnumerable<string> candidatePatchFiles)
        {
            return selectedPatchFiles.Any(fileName => candidatePatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase));
        }

        private void AppendCleanBaseIndexArguments(
            ref string arguments,
            string baselineDir,
            IEnumerable<string> selectedPatchFiles,
            IList<string> logLines)
        {
            string[] selected = selectedPatchFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            bool needsText = HasAnyPatchFile(selected, textPatchFiles);
            bool needsFont = HasAnyPatchFile(selected, fontPatchFiles);
            bool needsUi = HasAnyPatchFile(selected, uiPatchFiles);

            if (!needsText && !needsFont && !needsUi)
            {
                return;
            }

            if (string.IsNullOrEmpty(baselineDir))
            {
#if !TEST_BUILD
                throw new InvalidOperationException(
                    "Clean restore-baseline indexes were not found. Run preflight or remove the existing patch first, then try again.");
#else
                logLines.Add("Clean base index arguments: skipped because restore-baseline is not available.");
                return;
#endif
            }

            AppendCleanBaseIndexArgumentPair(ref arguments, logLines, needsText, baselineDir, "0a0000.win32.index", "--base-index", "--base-index2");
            AppendCleanBaseIndexArgumentPair(ref arguments, logLines, needsFont, baselineDir, "000000.win32.index", "--base-font-index", "--base-font-index2");
            AppendCleanBaseIndexArgumentPair(ref arguments, logLines, needsUi, baselineDir, "060000.win32.index", "--base-ui-index", "--base-ui-index2");
        }

        private void AppendCleanBaseIndexArgumentPair(
            ref string arguments,
            IList<string> logLines,
            bool enabled,
            string baselineDir,
            string indexFileName,
            string indexArgument,
            string index2Argument)
        {
            if (!enabled)
            {
                return;
            }

            string indexPath = GetRestoreBaselineOrigPath(baselineDir, indexFileName);
            string index2Path = GetRestoreBaselineOrigPath(baselineDir, indexFileName + "2");
            if (string.IsNullOrEmpty(indexPath) || string.IsNullOrEmpty(index2Path))
            {
                throw new FileNotFoundException(
                    "Clean restore-baseline index pair is missing.",
                    string.IsNullOrEmpty(indexPath) ? Path.Combine(baselineDir, "orig." + indexFileName) : Path.Combine(baselineDir, "orig." + indexFileName + "2"));
            }

            arguments += " " + indexArgument + " " + QuoteArgument(indexPath);
            arguments += " " + index2Argument + " " + QuoteArgument(index2Path);
            logLines.Add("Clean base index: " + indexPath);
            logLines.Add("Clean base index2: " + index2Path);
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
                output.IndexOf("Global and Korean client versions do not match", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    "글로벌 서버 클라이언트와 한국 서버 클라이언트 버전이 달라서 패치를 중단했습니다." + Environment.NewLine + Environment.NewLine +
                    "row-id fallback이 필요한 시트는 두 클라이언트 버전이 다르면 엉뚱한 행에 한글을 주입할 수 있습니다." + Environment.NewLine +
                    "두 클라이언트를 같은 패치 버전으로 맞춘 뒤 다시 사전 점검과 패치를 실행해주세요." + Environment.NewLine + Environment.NewLine +
                    Tail(output, 2000);
            }

            if (!string.IsNullOrEmpty(output) &&
                (output.IndexOf("already contains", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 output.IndexOf("already containes", StringComparison.OrdinalIgnoreCase) >= 0) &&
                (output.IndexOf("dat1 entries", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 output.IndexOf("dat4 entries", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                string pollutedIndex = "0a0000/000000/060000.win32.index";
                if (output.IndexOf("0a0000.win32.index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollutedIndex = "0a0000.win32.index";
                }
                else if (output.IndexOf("000000.win32.index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollutedIndex = "000000.win32.index";
                }
                else if (output.IndexOf("060000.win32.index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollutedIndex = "060000.win32.index";
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

        private static void AddGeneratorSummaryDetails(StringBuilder processOutput, IList<KeyValuePair<string, string>> resultDetails)
        {
            if (processOutput == null || resultDetails == null)
            {
                return;
            }

            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "EXD pages patched", "EXD 패치 페이지" },
                { "EXD rows patched", "EXD 패치 행" },
                { "String-key rows", "문자열 키 매칭 행" },
                { "Row-key rows", "row-id 매칭 행" },
                { "Protected UI tokens", "보호된 UI glyph" },
                { "RSV rows", "RSV 포함 행" },
                { "RSV strings", "RSV 포함 문자열" },
                { "Pages without mapping", "매핑 없음 페이지" },
                { "Unsupported sheets", "미지원 시트" },
                { "Font files patched", "폰트 패치 파일" },
                { "Diagnostics", "진단 파일" }
            };

            using (StringReader reader = new StringReader(processOutput.ToString()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    int separator = trimmed.IndexOf(':');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = trimmed.Substring(0, separator).Trim();
                    string value = trimmed.Substring(separator + 1).Trim();
                    string label;
                    if (labels.TryGetValue(key, out label) && !string.IsNullOrEmpty(value))
                    {
                        resultDetails.Add(new KeyValuePair<string, string>(label, value));
                    }
                }
            }
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
            return CountIndexDataFileEntries(indexPath, 1);
        }

        private int CountPatchedDataFileEntries(string indexPath)
        {
            return CountIndexDataFileEntries(indexPath, GetPatchDataFileId(indexPath));
        }

        private int GetPatchDataFileId(string indexPathOrFileName)
        {
            string fileName = Path.GetFileName(indexPathOrFileName);
            if (fileName.StartsWith("060000.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("orig.060000.", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 1;
        }

        private int CountIndexDataFileEntries(string indexPath, int dataFileId)
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

            bool index2 = indexPath.EndsWith(".index2", StringComparison.OrdinalIgnoreCase);
            int entrySize = index2 ? 8 : 16;
            int dataOffsetInEntry = index2 ? 4 : 8;
            int entryCount = indexDataSize / entrySize;
            int count = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = indexDataOffset + i * entrySize;
                uint data = ReadUInt32LittleEndian(bytes, entryOffset + dataOffsetInEntry);
                byte datId = (byte)((data & 0xEu) >> 1);
                if (datId == dataFileId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsCleanIndexFile(string indexPath, out int dat1Count, out string error)
        {
            dat1Count = 0;
            error = null;

            try
            {
                dat1Count = CountPatchedDataFileEntries(indexPath);
                return dat1Count == 0;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool IsIndex2FileName(string fileName)
        {
            return fileName.EndsWith(".index2", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanSkipMissingRestoreFile(string fileName)
        {
            if (!fileName.EndsWith(".index", StringComparison.OrdinalIgnoreCase) &&
                !IsIndex2FileName(fileName))
            {
                return false;
            }

            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir))
            {
                return false;
            }

            string currentPath = Path.Combine(sqpackDir, fileName);
            if (!File.Exists(currentPath))
            {
                return false;
            }

            int dat1Count;
            string error;
            return IsCleanIndexFile(currentPath, out dat1Count, out error);
        }

        private bool HasPatchedTargetIndexes()
        {
            string sqpackDir = GetTargetSqpackDir();
            if (string.IsNullOrEmpty(sqpackDir) || !Directory.Exists(sqpackDir))
            {
                return false;
            }

            foreach (string fileName in restoreFiles)
            {
                string indexPath = Path.Combine(sqpackDir, fileName);
                if (!File.Exists(indexPath))
                {
                    continue;
                }

                try
                {
                    if (CountPatchedDataFileEntries(indexPath) > 0)
                    {
                        return true;
                    }
                }
                catch
                {
                    // If index state cannot be read, keep real-client patch buttons locked.
                    return true;
                }
            }

            return false;
        }

        private bool TryUnlockPatchAfterRestore(List<string> logLines)
        {
            bool hasGlobalClient = HasValidGlobalClient();
            bool hasKoreaClient = HasValidKoreaClient();
            bool hasGlobalVersion = RefreshTargetVersion();
            string koreaVersion = GetKoreaVersion();
            bool versionsMatch = hasGlobalVersion &&
                !string.IsNullOrEmpty(koreaVersion) &&
                string.Equals(targetVersion, koreaVersion, StringComparison.OrdinalIgnoreCase);
            bool targetAlreadyPatched = hasGlobalClient && HasPatchedTargetIndexes();

            if (hasGlobalClient && hasKoreaClient && versionsMatch && !targetAlreadyPatched)
            {
                lastPreflightPassed = true;
                if (logLines != null)
                {
                    logLines.Add("Post-remove unlock: clean indexes verified; next patch can start without another preflight.");
                }

                return true;
            }

            lastPreflightPassed = false;
            if (logLines != null)
            {
                logLines.Add(
                    "Post-remove unlock skipped: globalClient=" + hasGlobalClient +
                    ", koreaClient=" + hasKoreaClient +
                    ", versionsMatch=" + versionsMatch +
                    ", patchedIndex=" + targetAlreadyPatched + ".");
            }

            return false;
        }

        private bool HasCleanOrigIndexes(string candidateDir)
        {
            foreach (string fileName in restoreFiles)
            {
                string origIndexPath = Path.Combine(candidateDir, "orig." + fileName);
                if (!File.Exists(origIndexPath))
                {
                    if (CanSkipMissingRestoreFile(fileName))
                    {
                        continue;
                    }

                    return false;
                }

                int dat1Count;
                string error;
                if (!IsCleanIndexFile(origIndexPath, out dat1Count, out error))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class RestoreFileCopy
        {
            public string SourcePath;
            public string DestinationFileName;

            public RestoreFileCopy(string sourcePath, string destinationFileName)
            {
                SourcePath = sourcePath;
                DestinationFileName = destinationFileName;
            }
        }

        private void ValidateDat1References(string indexPath, string dat1Path, string context)
        {
            byte[] bytes = File.ReadAllBytes(indexPath);
            long dat1Length = new FileInfo(dat1Path).Length;
            int sqpackHeaderSize = checked((int)ReadUInt32LittleEndian(bytes, 0x0C));
            int indexHeaderOffset = sqpackHeaderSize;
            int indexDataOffset = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x08));
            int indexDataSize = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x0C));
            bool index2 = indexPath.EndsWith(".index2", StringComparison.OrdinalIgnoreCase);
            int entrySize = index2 ? 8 : 16;
            int dataOffsetInEntry = index2 ? 4 : 8;
            int entryCount = indexDataSize / entrySize;
            int dat1Count = 0;

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = indexDataOffset + i * entrySize;
                uint data = ReadUInt32LittleEndian(bytes, entryOffset + dataOffsetInEntry);
                byte datId = (byte)((data & 0xEu) >> 1);
                if (datId != 1)
                {
                    continue;
                }

                dat1Count++;
                long fileOffset = (long)(data & 0xFFFFFFF0u) * 8L;
                if ((fileOffset % 0x80) != 0 || fileOffset < 0 || fileOffset + 24 > dat1Length)
                {
                    throw new InvalidDataException(context + " index가 dat1 범위 밖의 파일 오프셋을 가리킵니다: " + fileOffset.ToString());
                }

                ValidateDat1FileHeader(dat1Path, dat1Length, fileOffset, context);
            }

            if (dat1Count <= 0)
            {
                throw new InvalidDataException(context + " index에 dat1 엔트리가 없습니다.");
            }
        }

        private void ValidateDatReferences(string indexPath, string datPath, int dataFileId, string context)
        {
            byte[] bytes = File.ReadAllBytes(indexPath);
            long datLength = new FileInfo(datPath).Length;
            int sqpackHeaderSize = checked((int)ReadUInt32LittleEndian(bytes, 0x0C));
            int indexHeaderOffset = sqpackHeaderSize;
            int indexDataOffset = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x08));
            int indexDataSize = checked((int)ReadUInt32LittleEndian(bytes, indexHeaderOffset + 0x0C));
            bool index2 = indexPath.EndsWith(".index2", StringComparison.OrdinalIgnoreCase);
            int entrySize = index2 ? 8 : 16;
            int dataOffsetInEntry = index2 ? 4 : 8;
            int entryCount = indexDataSize / entrySize;
            int dataFileEntryCount = 0;

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = indexDataOffset + i * entrySize;
                uint data = ReadUInt32LittleEndian(bytes, entryOffset + dataOffsetInEntry);
                byte datId = (byte)((data & 0xEu) >> 1);
                if (datId != dataFileId)
                {
                    continue;
                }

                dataFileEntryCount++;
                long fileOffset = (long)(data & 0xFFFFFFF0u) * 8L;
                if ((fileOffset % 0x80) != 0 || fileOffset < 0 || fileOffset + 24 > datLength)
                {
                    throw new InvalidDataException(context + " index points outside dat" + dataFileId.ToString() + ": " + fileOffset.ToString());
                }

                ValidateDat1FileHeader(datPath, datLength, fileOffset, context);
            }

            if (dataFileEntryCount <= 0)
            {
                throw new InvalidDataException(context + " index has no dat" + dataFileId.ToString() + " entries.");
            }
        }

        private void ValidateDat1FileHeader(string dat1Path, long dat1Length, long fileOffset, string context)
        {
            byte[] header = new byte[24];
            using (FileStream stream = new FileStream(dat1Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Position = fileOffset;
                int read = stream.Read(header, 0, header.Length);
                if (read != header.Length)
                {
                    throw new InvalidDataException(context + " dat1 파일 헤더를 읽을 수 없습니다: " + fileOffset.ToString());
                }
            }

            uint headerSize = ReadUInt32LittleEndian(header, 0x00);
            uint fileType = ReadUInt32LittleEndian(header, 0x04);
            if (fileType != 2)
            {
                return;
            }

            uint rawFileSize = ReadUInt32LittleEndian(header, 0x08);
            uint blockDataUnits = ReadUInt32LittleEndian(header, 0x10);
            uint blockCount = ReadUInt32LittleEndian(header, 0x14);

            if (headerSize < 24 || (headerSize % 0x80) != 0)
            {
                throw new InvalidDataException(context + " dat1 표준 파일 헤더 크기가 올바르지 않습니다: " + headerSize.ToString());
            }

            if (blockCount == 0)
            {
                throw new InvalidDataException(context + " dat1 표준 파일 block count가 0입니다: " + fileOffset.ToString());
            }

            if (blockDataUnits == 0)
            {
                throw new InvalidDataException(context + " dat1 표준 파일 block data 길이가 0입니다: " + fileOffset.ToString());
            }

            long packedEnd = checked(fileOffset + headerSize + (long)blockDataUnits * 0x80L);
            if (packedEnd > dat1Length)
            {
                throw new InvalidDataException(context + " dat1 표준 파일이 dat1 범위를 벗어납니다: " + fileOffset.ToString());
            }

            if (rawFileSize == 0)
            {
                throw new InvalidDataException(context + " dat1 표준 파일 원본 크기가 0입니다: " + fileOffset.ToString());
            }
        }

        private void ValidateSelectedPatchReferences(string directory, IEnumerable<string> patchFiles, string context)
        {
            if (patchFiles.Any(fileName => string.Equals(fileName, "0a0000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDat1References(
                    Path.Combine(directory, "0a0000.win32.index"),
                    Path.Combine(directory, "0a0000.win32.dat1"),
                    context + " 0a0000");
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "0a0000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDat1References(
                    Path.Combine(directory, "0a0000.win32.index2"),
                    Path.Combine(directory, "0a0000.win32.dat1"),
                    context + " 0a0000 index2");
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "000000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDat1References(
                    Path.Combine(directory, "000000.win32.index"),
                    Path.Combine(directory, "000000.win32.dat1"),
                    context + " 000000");
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "000000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDat1References(
                    Path.Combine(directory, "000000.win32.index2"),
                    Path.Combine(directory, "000000.win32.dat1"),
                    context + " 000000 index2");
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "060000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDatReferences(
                    Path.Combine(directory, "060000.win32.index"),
                    Path.Combine(directory, "060000.win32.dat4"),
                    4,
                    context + " 060000");
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "060000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateDatReferences(
                    Path.Combine(directory, "060000.win32.index2"),
                    Path.Combine(directory, "060000.win32.dat4"),
                    4,
                    context + " 060000 index2");
            }
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
            return GetRuntimeDataChildDir("backups");
        }

        private IEnumerable<string> GetBackupRootDirs()
        {
            return new string[]
            {
                GetBackupRootDir(),
                Path.Combine(Application.StartupPath, "backups"),
                Path.Combine(Application.CommonAppDataPath, "backups")
            }.Distinct(StringComparer.OrdinalIgnoreCase);
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

            List<string> copiedFiles = new List<string>();
            foreach (string fileName in fileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string sourcePath = Path.Combine(sqpackDir, fileName);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                File.Copy(sourcePath, Path.Combine(backupDir, fileName), true);
                copiedFiles.Add(fileName);
            }

            if (copiedFiles.Count == 0)
            {
                return string.Empty;
            }

            WriteBackupInfoFile(backupDir, reason, copiedFiles);
            CreateManualRollbackPackage(backupDir, copiedFiles);
            return backupDir;
        }

        private void WriteBackupInfoFile(string backupDir, string reason, IEnumerable<string> copiedFiles)
        {
            string infoPath = Path.Combine(backupDir, "backup-info.txt");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("FFXIV Korean Patch backup");
            builder.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Reason: " + reason);
            builder.AppendLine("Global game: " + targetDir);
            builder.AppendLine("Sqpack: " + GetTargetSqpackDir());
            builder.AppendLine("Files:");
            foreach (string fileName in copiedFiles)
            {
                builder.AppendLine("  " + fileName);
            }

            builder.AppendLine();
            builder.AppendLine("Manual rollback:");
            builder.AppendLine("If this folder contains " + manualRollbackDirName + ", copy the files in that folder into the global client sqpack\\ffxiv folder.");
            builder.AppendLine("Clean index files stop the client from referencing patched dat entries. Unused patch dat files may remain on disk.");
            File.WriteAllText(infoPath, builder.ToString(), Encoding.UTF8);
        }

        private void CreateManualRollbackPackage(string backupDir, IEnumerable<string> copiedFiles)
        {
            string rollbackDir = Path.Combine(backupDir, manualRollbackDirName);
            bool wroteAny = false;

            foreach (string fileName in restoreFiles)
            {
                if (!copiedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string indexPath = Path.Combine(backupDir, fileName);
                if (!File.Exists(indexPath))
                {
                    continue;
                }

                int dat1Count;
                string error;
                if (!IsCleanIndexFile(indexPath, out dat1Count, out error))
                {
                    continue;
                }

                string origPath = Path.Combine(backupDir, "orig." + fileName);
                if (!File.Exists(origPath))
                {
                    File.Copy(indexPath, origPath, false);
                }

                Directory.CreateDirectory(rollbackDir);
                File.Copy(indexPath, Path.Combine(rollbackDir, fileName), true);
                wroteAny = true;
            }

            if (wroteAny)
            {
                File.WriteAllText(
                    Path.Combine(rollbackDir, "README.txt"),
                    "Copy these index files into the global client sqpack\\ffxiv folder to roll back manually." + Environment.NewLine +
                    "This restores the clean index references captured before patch apply." + Environment.NewLine,
                    Encoding.UTF8);
            }
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
                    int origDat1Count;
                    string origError;
                    if (IsCleanIndexFile(installedOrigPath, out origDat1Count, out origError))
                    {
                        sourcePath = installedOrigPath;
                    }
                    else if (lines != null)
                    {
                        lines.Add("[주의] " + Path.GetFileName(installedOrigPath) + "를 clean 복구 기준으로 저장하지 않았습니다. dat1 엔트리: " + origDat1Count.ToString() + (string.IsNullOrEmpty(origError) ? "" : ", 오류: " + origError));
                    }
                }
                if (string.IsNullOrEmpty(sourcePath) && File.Exists(currentIndexPath))
                {
                    int dat1Count = CountPatchedDataFileEntries(currentIndexPath);
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
            return GetRuntimeDataChildDir("logs");
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
                .Concat(uiPatchFiles)
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
                requiredOrigFiles.Add("orig.0a0000.win32.index2");
            }

            if (patchFiles.Any(fileName => fontPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)))
            {
                requiredOrigFiles.Add("orig.000000.win32.index");
                requiredOrigFiles.Add("orig.000000.win32.index2");
            }

            if (patchFiles.Any(fileName => uiPatchFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)))
            {
                requiredOrigFiles.Add("orig.060000.win32.index");
                requiredOrigFiles.Add("orig.060000.win32.index2");
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

            if (patchFiles.Any(fileName => string.Equals(fileName, "0a0000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                int textIndex2Dat1Count = CountIndexDat1Entries(Path.Combine(outputDir, "0a0000.win32.index2"));
                if (textIndex2Dat1Count <= 0)
                {
                    throw new InvalidDataException("?앹꽦??0a0000.win32.index2??dat1 ?뷀듃由ш? ?놁뒿?덈떎.");
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

            if (patchFiles.Any(fileName => string.Equals(fileName, "000000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                int fontIndex2Dat1Count = CountIndexDat1Entries(Path.Combine(outputDir, "000000.win32.index2"));
                if (fontIndex2Dat1Count <= 0)
                {
                    throw new InvalidDataException("?앹꽦??000000.win32.index2??dat1 ?뷀듃由ш? ?놁뒿?덈떎.");
                }
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "060000.win32.index", StringComparison.OrdinalIgnoreCase)))
            {
                int uiDat1Count = CountIndexDataFileEntries(Path.Combine(outputDir, "060000.win32.index"), 4);
                if (uiDat1Count <= 0)
                {
                    throw new InvalidDataException("생성된 060000.win32.index에 dat4 엔트리가 없습니다.");
                }
            }

            if (patchFiles.Any(fileName => string.Equals(fileName, "060000.win32.index2", StringComparison.OrdinalIgnoreCase)))
            {
                int uiIndex2Dat1Count = CountIndexDataFileEntries(Path.Combine(outputDir, "060000.win32.index2"), 4);
                if (uiIndex2Dat1Count <= 0)
                {
                    throw new InvalidDataException("생성된 060000.win32.index2에 dat4 엔트리가 없습니다.");
                }
            }

#if !TEST_BUILD
            foreach (string origFileName in requiredOrigFiles)
            {
                int origDat1Count = CountPatchedDataFileEntries(Path.Combine(outputDir, origFileName));
                if (origDat1Count > 0)
                {
                    throw new InvalidDataException("생성된 " + origFileName + "가 clean index가 아닙니다. dat1 엔트리 " + origDat1Count.ToString() + "개가 포함되어 있습니다.");
                }
            }
#endif

            ValidateSelectedPatchReferences(outputDir, patchFiles, "생성된 release");
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

        private void ValidateAppliedFilesMatchRelease(string releaseDir, string applySqpackDir, IEnumerable<string> patchFiles)
        {
            foreach (string fileName in patchFiles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string releasePath = Path.Combine(releaseDir, fileName);
                string appliedPath = Path.Combine(applySqpackDir, fileName);

                FileInfo releaseInfo = new FileInfo(releasePath);
                FileInfo appliedInfo = new FileInfo(appliedPath);
                if (!releaseInfo.Exists || !appliedInfo.Exists)
                {
                    throw new FileNotFoundException("적용 검증 중 패치 파일을 찾을 수 없습니다.", releaseInfo.Exists ? appliedPath : releasePath);
                }

                if (releaseInfo.Length != appliedInfo.Length)
                {
                    throw new InvalidDataException(
                        "적용 검증 실패: " + fileName + " 크기가 생성물과 다릅니다. " +
                        "게임 런처, 백신, 권한 문제로 파일 복사가 되돌려졌을 수 있습니다. 패치 제거 후 다시 시도해 주세요.");
                }

                string releaseHash = ComputeSHA1(releasePath);
                string appliedHash = ComputeSHA1(appliedPath);
                if (!string.Equals(releaseHash, appliedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "적용 검증 실패: " + fileName + " 해시가 생성물과 다릅니다. " +
                        "특히 index가 원본으로 남으면 새 dat 파일이 있어도 게임은 일본어 리소스를 읽습니다. 패치 제거 후 다시 시도해 주세요.");
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

            string languageRoot = Path.Combine(GetRuntimeDataRootDir(), "generated-release", targetLanguageCode);
            string legacyStartupLanguageRoot = Path.Combine(Application.StartupPath, "generated-release", targetLanguageCode);
            string commonLanguageRoot = Path.Combine(Application.CommonAppDataPath, "generated-release", targetLanguageCode);
            string latest = FindLatestDirectory(languageRoot, legacyStartupLanguageRoot, commonLanguageRoot);
            if (!string.IsNullOrEmpty(latest))
            {
                return latest;
            }

            string appRoot = Path.Combine(GetRuntimeDataRootDir(), "generated-release");
            string legacyStartupRoot = Path.Combine(Application.StartupPath, "generated-release");
            string commonRoot = Path.Combine(Application.CommonAppDataPath, "generated-release");
            return FindLatestDirectory(appRoot, legacyStartupRoot, commonRoot);
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
                Path.Combine(GetRuntimeDataRootDir(), "generated-release"),
                Path.Combine(Application.CommonAppDataPath, "generated-release"),
                Path.Combine(Application.StartupPath, "generated-release"),
                GetLogRootDir(),
                Path.Combine(Application.StartupPath, "logs"),
                Path.Combine(Application.CommonAppDataPath, "logs")
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

        private IEnumerable<string> EnumerateRestoreCandidateDirs()
        {
            HashSet<string> dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in GetBackupRootDirs())
            {
                AddRestoreCandidateDir(root, dirs, true);
            }

            foreach (string candidateDir in GetLocalOrigIndexCandidateDirs())
            {
                AddRestoreCandidateDir(candidateDir, dirs, false);
            }

            return dirs
                .Where(Directory.Exists)
                .OrderByDescending(GetDirectoryLastWriteTimeUtcSafe)
                .ToArray();
        }

        private IEnumerable<string> EnumerateBackupCandidateDirs()
        {
            HashSet<string> dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in GetBackupRootDirs())
            {
                AddRestoreCandidateDir(root, dirs, true);
            }

            return dirs
                .Where(Directory.Exists)
                .OrderByDescending(GetDirectoryLastWriteTimeUtcSafe)
                .ToArray();
        }

        private void AddRestoreCandidateDir(string dir, HashSet<string> dirs, bool includeChildren)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            dirs.Add(dir);

            if (!includeChildren)
            {
                return;
            }

            try
            {
                foreach (string childDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                {
                    dirs.Add(childDir);
                }
            }
            catch
            {
                // Keep any candidates already discovered.
            }
        }

        private static DateTime GetDirectoryLastWriteTimeUtcSafe(string dir)
        {
            try
            {
                return Directory.GetLastWriteTimeUtc(dir);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private bool TryBuildCleanIndexRestorePlan(string candidateDir, out List<RestoreFileCopy> plan)
        {
            plan = new List<RestoreFileCopy>();

            string manualDir = Path.Combine(candidateDir, manualRollbackDirName);
            string[] sourceDirs = new string[]
            {
                candidateDir,
                manualDir
            };

            foreach (string sourceDir in sourceDirs)
            {
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                plan.Clear();
                bool ok = true;
                foreach (string fileName in restoreFiles)
                {
                    string sourcePath = Path.Combine(sourceDir, "orig." + fileName);
                    if (!File.Exists(sourcePath))
                    {
                        sourcePath = Path.Combine(sourceDir, fileName);
                    }

                    if (!File.Exists(sourcePath))
                    {
                        if (CanSkipMissingRestoreFile(fileName))
                        {
                            continue;
                        }

                        ok = false;
                        break;
                    }

                    int dat1Count;
                    string error;
                    if (!IsCleanIndexFile(sourcePath, out dat1Count, out error))
                    {
                        ok = false;
                        break;
                    }

                    plan.Add(new RestoreFileCopy(sourcePath, fileName));
                }

                if (ok && plan.Count > 0)
                {
                    return true;
                }
            }

            plan.Clear();
            return false;
        }

        private List<RestoreFileCopy> BuildBackupRestorePlan(string backupDir)
        {
            List<RestoreFileCopy> plan;
            if (TryBuildCleanIndexRestorePlan(backupDir, out plan))
            {
                return plan;
            }

            plan = new List<RestoreFileCopy>();
            AddExactBackupFiles(backupDir, plan);
            return plan;
        }

        private void AddExactBackupFiles(string backupDir, List<RestoreFileCopy> plan)
        {
            string[] restorableFiles = textPatchFiles
                .Concat(fontPatchFiles)
                .Concat(uiPatchFiles)
                .Concat(restoreFiles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string fileName in restorableFiles)
            {
                if (plan.Any(item => string.Equals(item.DestinationFileName, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string sourcePath = Path.Combine(backupDir, fileName);
                if (!File.Exists(sourcePath))
                {
                    sourcePath = Path.Combine(backupDir, manualRollbackDirName, fileName);
                }

                if (File.Exists(sourcePath))
                {
                    plan.Add(new RestoreFileCopy(sourcePath, fileName));
                }
            }
        }

        private bool HasRestorableBackupFiles(string backupDir)
        {
            List<RestoreFileCopy> plan = BuildBackupRestorePlan(backupDir);
            return plan.Count > 0;
        }

        private string GetRestoreSearchRootsText()
        {
            return string.Join(
                Environment.NewLine,
                GetBackupRootDirs()
                    .Concat(new string[]
                    {
                        Path.Combine(GetRuntimeDataRootDir(), "restore-baseline"),
                        Path.Combine(Application.StartupPath, "restore-baseline"),
                        Path.Combine(Application.CommonAppDataPath, "restore-baseline")
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        private string GetBackupSearchRootsText()
        {
            return string.Join(
                Environment.NewLine,
                GetBackupRootDirs()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        private static bool IsSamePath(string firstPath, string secondPath)
        {
            return string.Equals(
                Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private string SelectBackupDirectory()
        {
            DirectoryInfo[] backups = EnumerateBackupCandidateDirs()
                .Where(HasRestorableBackupFiles)
                .Select(path => new DirectoryInfo(path))
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
                listBox.DisplayMember = "FullName";

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

            List<RestoreFileCopy> restorePlan = BuildBackupRestorePlan(backupDir);

            if (restorePlan.Count == 0)
            {
                throw new FileNotFoundException("선택한 백업 폴더에 복구 가능한 index/dat 파일이 없습니다.", backupDir);
            }

            string[] destinationFiles = restorePlan
                .Select(item => item.DestinationFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string beforeRestoreBackup = BackupTargetFiles(destinationFiles, "before-backup-restore");
            foreach (RestoreFileCopy item in restorePlan)
            {
                string destinationPath = Path.Combine(sqpackDir, item.DestinationFileName);
                if (IsSamePath(item.SourcePath, destinationPath))
                {
                    continue;
                }

                File.Copy(item.SourcePath, destinationPath, true);
            }

            string logPath = WriteOperationLog("backup-restore", new string[]
            {
                "Restored backup: " + backupDir,
                "Before restore backup: " + beforeRestoreBackup,
                "Files: " + string.Join(", ", destinationFiles)
            });

            List<KeyValuePair<string, string>> resultDetails = new List<KeyValuePair<string, string>>();
            resultDetails.Add(new KeyValuePair<string, string>("작업", "백업으로 복구"));
            resultDetails.Add(new KeyValuePair<string, string>("복구한 백업", backupDir));
            resultDetails.Add(new KeyValuePair<string, string>("복구 전 백업", beforeRestoreBackup));
            resultDetails.Add(new KeyValuePair<string, string>("복구 파일", string.Join(", ", destinationFiles)));

            ShowOperationResultDialog(
                "백업 복구 완료",
                "선택한 백업으로 글로벌 서버 클라이언트 파일을 되돌렸습니다.",
                logPath,
                resultDetails);
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
                Path.Combine(GetRuntimeDataRootDir(), "generated-release"),
                Path.Combine(GetRuntimeDataRootDir(), "restore-baseline"),
                Path.Combine(Application.StartupPath, "generated-release"),
                Path.Combine(Application.StartupPath, "restore-baseline"),
                Path.Combine(Application.CommonAppDataPath, "generated-release"),
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
            List<RestoreFileCopy> plan;
            return TryFindCleanIndexRestorePlan(out sourceDir, out plan);
        }

        private bool TryFindCleanIndexRestorePlan(out string sourceDir, out List<RestoreFileCopy> plan)
        {
            foreach (string candidateDir in EnumerateRestoreCandidateDirs())
            {
                if (!TryBuildCleanIndexRestorePlan(candidateDir, out plan))
                {
                    continue;
                }

                sourceDir = candidateDir;
                return true;
            }

            sourceDir = null;
            plan = new List<RestoreFileCopy>();
            return false;
        }

        private bool RestoreFromLocalOrigIndexes(out string restoreSourceDir, out string backupDir)
        {
            restoreSourceDir = null;
            backupDir = null;

            string sourceDir;
            List<RestoreFileCopy> restorePlan;
            if (!TryFindCleanIndexRestorePlan(out sourceDir, out restorePlan))
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

            string[] destinationFiles = restorePlan
                .Select(item => item.DestinationFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            backupDir = BackupTargetFiles(destinationFiles, "local-restore");

            for (int i = 0; i < restorePlan.Count; i++)
            {
                RestoreFileCopy item = restorePlan[i];
                string destinationPath = Path.Combine(sqpackDir, item.DestinationFileName);
                if (!IsSamePath(item.SourcePath, destinationPath))
                {
                    File.Copy(item.SourcePath, destinationPath, true);
                }

                SetProgressValue((i + 1) * 100 / restorePlan.Count);
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
                int dat1Count = CountPatchedDataFileEntries(indexPath);
                if (dat1Count <= 0)
                {
                    lines.Add("[OK] 글로벌 " + indexFileName + "는 clean index로 보입니다.");
                    return;
                }

                if (File.Exists(origIndexPath))
                {
                    int origDat1Count;
                    string origError;
                    if (IsCleanIndexFile(origIndexPath, out origDat1Count, out origError))
                    {
                        warnings++;
                        lines.Add("[주의] 글로벌 " + indexFileName + "에 dat1 엔트리 " + dat1Count + "개가 있지만, 같은 폴더에 clean orig index가 있어 복구 또는 빌더 기준 파일로 사용할 수 있습니다.");
                    }
                    else
                    {
#if TEST_BUILD
                        warnings++;
                        lines.Add("[주의] 글로벌 " + indexFileName + "와 orig." + indexFileName + " 모두 clean index가 아닙니다. 테스트 빌드는 진행 가능하지만 실제 적용에 쓰면 안 됩니다.");
#else
                        failures++;
                        lines.Add("[실패] 글로벌 " + indexFileName + "가 패치된 상태이고 orig." + indexFileName + "도 clean index가 아닙니다. orig dat1 엔트리: " + origDat1Count.ToString() + ". 런처 복구 또는 clean index가 필요합니다.");
#endif
                    }
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

                string globalVersion = string.Empty;
                string koreaVersion = string.Empty;

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
                        globalVersion = File.ReadAllText(versionPath).Trim();
                        lines.Add("[OK] 글로벌 서버 버전: " + globalVersion);
                    }
                    else
                    {
                        failures++;
                        lines.Add("[실패] 글로벌 서버 ffxivgame.ver를 찾을 수 없습니다.");
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
                        koreaVersion = File.ReadAllText(versionPath).Trim();
                        lines.Add("[OK] 한국 서버 버전: " + koreaVersion);
                    }
                    else
                    {
                        failures++;
                        lines.Add("[실패] 한국 서버 ffxivgame.ver를 찾을 수 없습니다.");
                    }
                }

                if (!string.IsNullOrEmpty(globalVersion) &&
                    !string.IsNullOrEmpty(koreaVersion) &&
                    !string.Equals(globalVersion, koreaVersion, StringComparison.OrdinalIgnoreCase))
                {
#if TEST_BUILD
                    warnings++;
                    lines.Add("[주의] 글로벌/한국 서버 버전이 다릅니다. 테스트 빌드는 진행 가능하지만 row-id fallback 결과는 신뢰하면 안 됩니다.");
#else
                    failures++;
                    lines.Add("[실패] 글로벌/한국 서버 버전이 다릅니다. row-id fallback 패치가 위험하므로 실제 패치를 차단합니다.");
#endif
                }

                string patchGeneratorPath = FindPatchGeneratorPath();
                if (string.IsNullOrEmpty(patchGeneratorPath))
                {
                    failures++;
                    lines.Add("[실패] 내장 FFXIVPatchGenerator.exe를 추출할 수 없습니다.");
                }
                else
                {
                    lines.Add("[OK] FFXIVPatchGenerator.exe: " + patchGeneratorPath);
                    string patchPolicyPath = FindPatchPolicyPath(patchGeneratorPath);
                    if (string.IsNullOrEmpty(patchPolicyPath))
                    {
                        lines.Add("[OK] 외부 JSON 패치 정책 없음, 기본 내장 정책 사용");
                    }
                    else
                    {
                        lines.Add("[OK] JSON 패치 정책: " + patchPolicyPath);
                    }

                    string fontPackageDir = FindFontPackageDir(patchGeneratorPath);
                    if (string.IsNullOrEmpty(fontPackageDir))
                    {
                        failures++;
                        lines.Add("[실패] 내장 TTMP font package를 추출할 수 없습니다. 릴리즈 빌드를 다시 생성해야 합니다.");
                    }
                    else
                    {
                        lines.Add("[OK] TTMP font package: " + fontPackageDir);
                    }
                }

                if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                {
                    string sqpackDir = GetTargetSqpackDir();
                    string[] requiredSqpackFiles = new string[]
                    {
                        "000000.win32.index",
                        "000000.win32.dat0",
                        "060000.win32.index",
                        "060000.win32.dat0",
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
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "0a0000.win32.index2");
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "000000.win32.index");
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "000000.win32.index2");
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "060000.win32.index");
                    AddPreflightIndexStatus(lines, ref failures, ref warnings, "060000.win32.index2");
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

                lastPreflightPassed = failures == 0;
                UpdateStatusLabel(summary, failures > 0);
                SetProgressValue(failures == 0 ? 100 : 0);

                ShowPreflightResultDialog(summary, failures, warnings, logPath, lines);
            }
            catch (Exception exception)
            {
                lastPreflightPassed = false;
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
                "작업 전에 현재 파일은 아래 백업 폴더에 저장됩니다." + Environment.NewLine +
                GetBackupRootDir() + Environment.NewLine + Environment.NewLine +
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
                    Invoke(new Action(StartInitialPreflightCheck));
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
                UpdateDownloadLabel("");

                // Check all done!
                UpdateStatusLabel($"버전 {targetVersion}, 로컬 생성 모드", false);

                Invoke(new Action(() =>
                {
                    bool hasGlobalClient = HasValidGlobalClient();
                    bool hasKoreaClient = HasValidKoreaClient();
                    bool targetAlreadyPatched = hasGlobalClient && HasPatchedTargetIndexes();

                    globalPathBrowseButton.Enabled = true;
                    koreaPathBrowseButton.Enabled = true;
                    targetLanguageComboBox.Enabled = true;
                    detectPathsButton.Enabled = true;
                    resetPathsButton.Enabled = true;
                    openReleaseButton.Enabled = true;
                    openLogsButton.Enabled = true;
                    cleanupButton.Enabled = true;
#if TEST_BUILD
                    debugBuildReleaseButton.Enabled = hasGlobalClient && hasKoreaClient;
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
                    installButton.Enabled = hasGlobalClient && hasKoreaClient && lastPreflightPassed && !targetAlreadyPatched;
                    chatOnlyInstallButton.Enabled = hasGlobalClient && hasKoreaClient && lastPreflightPassed && !targetAlreadyPatched;
                    removeButton.Enabled = hasGlobalClient;
#endif
                    progressBar.Value = 0;
                    downloadLabel.Text = "";
                }));
                Invoke(new Action(StartInitialPreflightCheck));
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
            MarkPreflightRequired();
            RefreshTargetVersion();
            UpdatePathTextBoxes();
            UpdateStatusLabel(string.IsNullOrEmpty(koreaSourceDir)
                ? "한국 서버 클라이언트 경로를 확인하거나 수동으로 지정해주세요."
                : $"버전 {targetVersion}, 글로벌/한국 서버 클라이언트 경로 설정 완료. 사전 점검을 실행해주세요.");
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
            MarkPreflightRequired();
            UpdatePathTextBoxes();
            UpdateStatusLabel(string.IsNullOrEmpty(targetDir)
                ? "글로벌 서버 클라이언트 경로를 확인하거나 수동으로 지정해주세요."
                : $"버전 {targetVersion}, 글로벌/한국 서버 클라이언트 경로 설정 완료. 사전 점검을 실행해주세요.");
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

            MarkPreflightRequired();
            if (statusLabel != null && statusLabel.IsHandleCreated)
            {
                UpdateStatusLabel("베이스 클라이언트 언어: " + targetLanguageDisplayName + " (" + targetLanguageCode + "), 사전 점검을 다시 실행해주세요.");
            }
        }

        private void detectPathsButton_Click(object sender, EventArgs e)
        {
            targetDir = string.Empty;
            koreaSourceDir = string.Empty;
            targetVersion = string.Empty;
            releaseOutputDir = string.Empty;
            MarkPreflightRequired();

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
            releaseOutputDir = string.Empty;
            useDebugApplyPath = false;
            MarkPreflightRequired();

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
                "generated-release와 logs에서 30일 지난 파일과 빈 폴더를 정리할까요?\r\n\r\n백업과 로컬 복구 기준은 자동 정리하지 않습니다.",
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
            string backupDir = SelectBackupDirectory();
            if (string.IsNullOrEmpty(backupDir))
            {
                MessageBox.Show(
                    "복구할 수 있는 백업을 찾지 못했습니다.\r\n\r\n패치 제거는 [한글 패치 제거] 버튼을 사용해주세요.\r\n\r\n백업 검색 위치:\r\n" + GetBackupSearchRootsText(),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!HasRestorableBackupFiles(backupDir))
            {
                MessageBox.Show(
                    "선택한 백업 폴더에 복구 가능한 index/dat 파일이 없습니다.\r\n\r\n백업 폴더:\r\n" + backupDir,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!ConfirmActualClientWrite("백업으로 복구"))
            {
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

#if !TEST_BUILD
            if (!debugApply && !lastPreflightPassed)
            {
                MessageBox.Show("사전 점검을 통과한 뒤에 실제 글로벌 서버 클라이언트에 적용할 수 있어요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetActionButtonsEnabled(true);
                return;
            }

            if (!debugApply && HasPatchedTargetIndexes())
            {
                MessageBox.Show("이미 한글 패치가 적용된 index 상태입니다. 먼저 [한글 패치 제거]로 clean index를 복구한 뒤 다시 진행해주세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetActionButtonsEnabled(true);
                return;
            }
#endif

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

        private void StartPreflightCheck()
        {
            if (preflightWorker.IsBusy)
            {
                return;
            }

            SetActionButtonsEnabled(false);
            preflightWorker.RunWorkerAsync();
        }

        private void StartInitialPreflightCheck()
        {
            if (initialPreflightStarted)
            {
                return;
            }

            initialPreflightStarted = true;
            StartPreflightCheck();
        }

        private void preflightCheckButton_Click(object sender, EventArgs e)
        {
            StartPreflightCheck();
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
                files.AddRange(uiPatchFiles);
            }

            return files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private string GetSelectedFontPatchProfile()
        {
            if (debugFontProfileComboBox == null)
            {
                return "full";
            }

            FontProfileItem item = debugFontProfileComboBox.SelectedItem as FontProfileItem;
            return item == null || string.IsNullOrEmpty(item.Value) ? "full" : item.Value;
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
                    throw new FileNotFoundException("내장 FFXIVPatchGenerator.exe를 추출할 수 없어요. 릴리즈 빌드를 다시 생성해주세요.");
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

                string patchPolicyPath = FindPatchPolicyPath(patchGeneratorPath);
                if (!string.IsNullOrEmpty(patchPolicyPath))
                {
                    arguments += " --policy " + QuoteArgument(patchPolicyPath);
                    logLines.Add("Patch policy: " + patchPolicyPath);
                }

                AppendCleanBaseIndexArguments(ref arguments, baselineDir, selectedPatchFiles, logLines);

#if TEST_BUILD
                arguments += " --allow-patched-global";
                arguments += " --allow-version-mismatch";
                if (buildFontPatch)
                {
                    arguments += " --font-profile " + GetSelectedFontPatchProfile();
                    logLines.Add("Font profile: " + GetSelectedFontPatchProfile());
                }
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
                ValidateSelectedPatchReferences(applySqpackDir, selectedPatchFiles, "적용 대상");
                ValidateAppliedFilesMatchRelease(releaseOutputDir, applySqpackDir, selectedPatchFiles);
                logLines.Add("Apply byte validation: OK");
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
                string operationName = buildTextPatch && buildFontPatch ? "전체 한글 패치" : "한글 폰트 패치";
                string completionSummary = useDebugApplyPath
                    ? "원본 글로벌 서버 클라이언트는 변경하지 않고 테스트 경로에 적용했습니다."
                    : "생성된 패치 파일을 글로벌 서버 클라이언트에 적용했습니다.";
                UpdateStatusLabel(useDebugApplyPath ? "패치 생성과 테스트 적용 완료" : "패치 생성과 설치 완료");

                List<KeyValuePair<string, string>> resultDetails = new List<KeyValuePair<string, string>>();
                resultDetails.Add(new KeyValuePair<string, string>("작업", operationName));
                resultDetails.Add(new KeyValuePair<string, string>("베이스 언어", targetLanguageDisplayName + " (" + targetLanguageCode + ")"));
                resultDetails.Add(new KeyValuePair<string, string>("생성 폴더", releaseOutputDir));
                resultDetails.Add(new KeyValuePair<string, string>("적용 위치", applyGameDir));
                resultDetails.Add(new KeyValuePair<string, string>("Manifest", manifestPath));
                resultDetails.Add(new KeyValuePair<string, string>("적용 파일", string.Join(", ", selectedPatchFiles)));
                AddGeneratorSummaryDetails(processOutput, resultDetails);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    resultDetails.Add(new KeyValuePair<string, string>("백업 위치", backupDir));
                }

                ShowOperationResultDialog("패치 작업 완료", completionSummary, logPath, resultDetails);
            }
            catch (Exception exception)
            {
                UpdateDownloadLabel("");
                UpdateStatusLabel("패치 release 생성 실패", true);
                SetProgressValue(0);
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
            }
            finally
            {
                SetProgressMarquee(false);
                SetActionButtonsEnabled(true);
            }
        }

        private void LegacyWorkerRestoreWork(bool isRemove)
        {
            List<string> logLines = new List<string>();

            try
            {
#if TEST_BUILD
                throw new InvalidOperationException("테스트 빌드에서는 실제 글로벌 서버 클라이언트 폴더에 파일을 복사할 수 없어요.");
#else
                if (!isRemove)
                {
                    throw new InvalidOperationException("기존 원격 설치 모드는 더 이상 사용하지 않아요. 전체 한글 패치 또는 한글 폰트 패치 버튼은 로컬 생성기를 통해 처리됩니다.");
                }

                string restoreSourceDir;
                string localBackupDir;
                if (RestoreFromLocalOrigIndexes(out restoreSourceDir, out localBackupDir))
                {
                    logLines.Add("Local restore source: " + restoreSourceDir);
                    logLines.Add("Backup before restore: " + localBackupDir);
                    bool canPatchImmediately = TryUnlockPatchAfterRestore(logLines);
                    string localRestoreLogPath = WriteOperationLog("remove-local-restore", logLines);
                    UpdateStatusLabel("한글 패치 제거 완료");
                    SetProgressValue(100);

                    List<KeyValuePair<string, string>> resultDetails = new List<KeyValuePair<string, string>>();
                    resultDetails.Add(new KeyValuePair<string, string>("작업", "한글 패치 제거"));
                    resultDetails.Add(new KeyValuePair<string, string>("복구 원본", restoreSourceDir));
                    if (!string.IsNullOrEmpty(localBackupDir))
                    {
                        resultDetails.Add(new KeyValuePair<string, string>("복구 전 백업", localBackupDir));
                    }
                    resultDetails.Add(new KeyValuePair<string, string>("복구 파일", string.Join(", ", restoreFiles)));
                    resultDetails.Add(new KeyValuePair<string, string>(
                        "다음 패치",
                        canPatchImmediately ? "사전 점검 없이 바로 실행 가능" : "사전 점검 후 실행 필요"));

                    ShowOperationResultDialog(
                        "패치 제거 완료",
                        "로컬 orig index로 글로벌 서버 클라이언트를 복구했습니다.",
                        localRestoreLogPath,
                        resultDetails);
                    return;
                }

                throw new InvalidOperationException(
                    "로컬 복구용 clean index를 찾지 못했습니다." + Environment.NewLine +
                    "검색 대상은 백업 폴더, manual-sqpack-rollback 폴더, 로컬 restore-baseline, 생성된 release의 orig index입니다." + Environment.NewLine +
                    "백업을 직접 갖고 있다면 sqpack\\ffxiv 폴더에 clean index를 수동으로 붙여넣거나, FFXIV 런처 파일 검사/복구로 글로벌 서버 클라이언트를 원본 상태로 되돌려주세요.");
#endif
            }
            catch (Exception exception)
            {
                UpdateStatusLabel(isRemove ? "한글 패치 제거 실패" : "패치 설치 실패", true);
                SetProgressValue(0);
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
                return;
            }
            finally
            {
                SetActionButtonsEnabled(true);
            }
        }

        private void installWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            LegacyWorkerRestoreWork(false);
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
            LegacyWorkerRestoreWork(false);
        }

        private void removeWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            LegacyWorkerRestoreWork(true);
        }

        #endregion
    }

}
