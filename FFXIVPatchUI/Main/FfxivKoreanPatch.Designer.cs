namespace FFXIVKoreanPatch.Main
{
    partial class FFXIVKoreanPatch
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FFXIVKoreanPatch));
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.downloadLabel = new System.Windows.Forms.Label();
            this.statusLabel = new System.Windows.Forms.Label();
            this.removeButton = new System.Windows.Forms.Button();
            this.chatOnlyInstallButton = new System.Windows.Forms.Button();
            this.installButton = new System.Windows.Forms.Button();
            this.buildReleaseButton = new System.Windows.Forms.Button();
            this.debugBuildReleaseButton = new System.Windows.Forms.Button();
            this.preflightCheckButton = new System.Windows.Forms.Button();
            this.globalPathLabel = new System.Windows.Forms.Label();
            this.globalPathTextBox = new System.Windows.Forms.TextBox();
            this.globalPathBrowseButton = new System.Windows.Forms.Button();
            this.koreaPathLabel = new System.Windows.Forms.Label();
            this.koreaPathTextBox = new System.Windows.Forms.TextBox();
            this.koreaPathBrowseButton = new System.Windows.Forms.Button();
            this.targetLanguageLabel = new System.Windows.Forms.Label();
            this.targetLanguageComboBox = new System.Windows.Forms.ComboBox();
            this.detectPathsButton = new System.Windows.Forms.Button();
            this.resetPathsButton = new System.Windows.Forms.Button();
            this.openReleaseButton = new System.Windows.Forms.Button();
            this.openLogsButton = new System.Windows.Forms.Button();
            this.cleanupButton = new System.Windows.Forms.Button();
            this.restoreBackupButton = new System.Windows.Forms.Button();
            this.initialChecker = new System.ComponentModel.BackgroundWorker();
            this.installWorker = new System.ComponentModel.BackgroundWorker();
            this.chatOnlyWorker = new System.ComponentModel.BackgroundWorker();
            this.removeWorker = new System.ComponentModel.BackgroundWorker();
            this.buildReleaseWorker = new System.ComponentModel.BackgroundWorker();
            this.preflightWorker = new System.ComponentModel.BackgroundWorker();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.progressBar.Location = new System.Drawing.Point(0, 810);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(640, 10);
            this.progressBar.TabIndex = 0;
            // 
            // downloadLabel
            // 
            this.downloadLabel.AutoSize = true;
            this.downloadLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.downloadLabel.Location = new System.Drawing.Point(0, 785);
            this.downloadLabel.Name = "downloadLabel";
            this.downloadLabel.Padding = new System.Windows.Forms.Padding(10, 0, 0, 10);
            this.downloadLabel.Size = new System.Drawing.Size(98, 25);
            this.downloadLabel.TabIndex = 0;
            this.downloadLabel.Text = "downloadLabel";
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusLabel.Location = new System.Drawing.Point(0, 740);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Padding = new System.Windows.Forms.Padding(10, 20, 0, 10);
            this.statusLabel.Size = new System.Drawing.Size(76, 45);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.Text = "statusLabel";
            // 
            // removeButton
            // 
            this.removeButton.AutoSize = true;
            this.removeButton.BackColor = System.Drawing.Color.Transparent;
            this.removeButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.removeButton.Enabled = false;
            this.removeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.removeButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.removeButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.removeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.removeButton.Location = new System.Drawing.Point(0, 693);
            this.removeButton.Margin = new System.Windows.Forms.Padding(10);
            this.removeButton.Name = "removeButton";
            this.removeButton.Padding = new System.Windows.Forms.Padding(10);
            this.removeButton.Size = new System.Drawing.Size(640, 47);
            this.removeButton.TabIndex = 0;
            this.removeButton.TabStop = false;
            this.removeButton.Text = "한글 패치 제거";
            this.removeButton.UseVisualStyleBackColor = false;
            this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
            // 
            // chatOnlyInstallButton
            // 
            this.chatOnlyInstallButton.AutoSize = true;
            this.chatOnlyInstallButton.BackColor = System.Drawing.Color.Transparent;
            this.chatOnlyInstallButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.chatOnlyInstallButton.Enabled = false;
            this.chatOnlyInstallButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.chatOnlyInstallButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.chatOnlyInstallButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.chatOnlyInstallButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chatOnlyInstallButton.Location = new System.Drawing.Point(0, 646);
            this.chatOnlyInstallButton.Margin = new System.Windows.Forms.Padding(10);
            this.chatOnlyInstallButton.Name = "chatOnlyInstallButton";
            this.chatOnlyInstallButton.Padding = new System.Windows.Forms.Padding(10);
            this.chatOnlyInstallButton.Size = new System.Drawing.Size(640, 47);
            this.chatOnlyInstallButton.TabIndex = 0;
            this.chatOnlyInstallButton.TabStop = false;
            this.chatOnlyInstallButton.Text = "한글 폰트 패치";
            this.chatOnlyInstallButton.UseVisualStyleBackColor = false;
            this.chatOnlyInstallButton.Click += new System.EventHandler(this.chatOnlyInstallButton_Click);
            // 
            // installButton
            // 
            this.installButton.AutoSize = true;
            this.installButton.BackColor = System.Drawing.Color.Transparent;
            this.installButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.installButton.Enabled = false;
            this.installButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.installButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.installButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.installButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.installButton.Location = new System.Drawing.Point(0, 599);
            this.installButton.Margin = new System.Windows.Forms.Padding(10);
            this.installButton.Name = "installButton";
            this.installButton.Padding = new System.Windows.Forms.Padding(10);
            this.installButton.Size = new System.Drawing.Size(640, 47);
            this.installButton.TabIndex = 0;
            this.installButton.TabStop = false;
            this.installButton.Text = "전체 한글 패치";
            this.installButton.UseVisualStyleBackColor = false;
            this.installButton.Click += new System.EventHandler(this.installButton_Click);
            // 
            // buildReleaseButton
            // 
            this.buildReleaseButton.AutoSize = true;
            this.buildReleaseButton.BackColor = System.Drawing.Color.Transparent;
            this.buildReleaseButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buildReleaseButton.Enabled = false;
            this.buildReleaseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.buildReleaseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.buildReleaseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.buildReleaseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buildReleaseButton.Location = new System.Drawing.Point(0, 552);
            this.buildReleaseButton.Margin = new System.Windows.Forms.Padding(10);
            this.buildReleaseButton.Name = "buildReleaseButton";
            this.buildReleaseButton.Padding = new System.Windows.Forms.Padding(10);
            this.buildReleaseButton.Size = new System.Drawing.Size(640, 47);
            this.buildReleaseButton.TabIndex = 0;
            this.buildReleaseButton.TabStop = false;
            this.buildReleaseButton.Text = "한국 서버 클라이언트로 자동 패치";
            this.buildReleaseButton.UseVisualStyleBackColor = false;
            this.buildReleaseButton.Click += new System.EventHandler(this.buildReleaseButton_Click);
            // 
            // debugBuildReleaseButton
            // 
            this.debugBuildReleaseButton.AutoSize = true;
            this.debugBuildReleaseButton.BackColor = System.Drawing.Color.Transparent;
            this.debugBuildReleaseButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.debugBuildReleaseButton.Enabled = false;
            this.debugBuildReleaseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.debugBuildReleaseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.debugBuildReleaseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.debugBuildReleaseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.debugBuildReleaseButton.Location = new System.Drawing.Point(0, 505);
            this.debugBuildReleaseButton.Margin = new System.Windows.Forms.Padding(10);
            this.debugBuildReleaseButton.Name = "debugBuildReleaseButton";
            this.debugBuildReleaseButton.Padding = new System.Windows.Forms.Padding(10);
            this.debugBuildReleaseButton.Size = new System.Drawing.Size(640, 47);
            this.debugBuildReleaseButton.TabIndex = 0;
            this.debugBuildReleaseButton.TabStop = false;
            this.debugBuildReleaseButton.Text = "테스트 자동 패치 (원본 변경 없음)";
            this.debugBuildReleaseButton.UseVisualStyleBackColor = false;
            this.debugBuildReleaseButton.Click += new System.EventHandler(this.debugBuildReleaseButton_Click);
            // 
            // preflightCheckButton
            // 
            this.preflightCheckButton.AutoSize = true;
            this.preflightCheckButton.BackColor = System.Drawing.Color.Transparent;
            this.preflightCheckButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.preflightCheckButton.Enabled = false;
            this.preflightCheckButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.preflightCheckButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.preflightCheckButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.preflightCheckButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.preflightCheckButton.Location = new System.Drawing.Point(0, 458);
            this.preflightCheckButton.Margin = new System.Windows.Forms.Padding(10);
            this.preflightCheckButton.Name = "preflightCheckButton";
            this.preflightCheckButton.Padding = new System.Windows.Forms.Padding(10);
            this.preflightCheckButton.Size = new System.Drawing.Size(640, 47);
            this.preflightCheckButton.TabIndex = 0;
            this.preflightCheckButton.TabStop = false;
            this.preflightCheckButton.Text = "사전 점검";
            this.preflightCheckButton.UseVisualStyleBackColor = false;
            this.preflightCheckButton.Click += new System.EventHandler(this.preflightCheckButton_Click);
            // 
            // globalPathLabel
            // 
            this.globalPathLabel.AutoSize = true;
            this.globalPathLabel.BackColor = System.Drawing.Color.Transparent;
            this.globalPathLabel.Location = new System.Drawing.Point(16, 195);
            this.globalPathLabel.Name = "globalPathLabel";
            this.globalPathLabel.Size = new System.Drawing.Size(132, 15);
            this.globalPathLabel.TabIndex = 0;
            this.globalPathLabel.Text = "글로벌 서버 클라이언트";
            // 
            // globalPathTextBox
            // 
            this.globalPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.globalPathTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.globalPathTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.globalPathTextBox.ForeColor = System.Drawing.Color.White;
            this.globalPathTextBox.Location = new System.Drawing.Point(16, 216);
            this.globalPathTextBox.Name = "globalPathTextBox";
            this.globalPathTextBox.ReadOnly = true;
            this.globalPathTextBox.Size = new System.Drawing.Size(522, 23);
            this.globalPathTextBox.TabIndex = 0;
            this.globalPathTextBox.TabStop = false;
            this.globalPathTextBox.Text = "(미설정)";
            // 
            // globalPathBrowseButton
            // 
            this.globalPathBrowseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.globalPathBrowseButton.Enabled = false;
            this.globalPathBrowseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.globalPathBrowseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.globalPathBrowseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.globalPathBrowseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.globalPathBrowseButton.Location = new System.Drawing.Point(550, 215);
            this.globalPathBrowseButton.Name = "globalPathBrowseButton";
            this.globalPathBrowseButton.Size = new System.Drawing.Size(74, 25);
            this.globalPathBrowseButton.TabIndex = 0;
            this.globalPathBrowseButton.TabStop = false;
            this.globalPathBrowseButton.Text = "변경";
            this.globalPathBrowseButton.UseVisualStyleBackColor = true;
            this.globalPathBrowseButton.Click += new System.EventHandler(this.globalPathBrowseButton_Click);
            // 
            // koreaPathLabel
            // 
            this.koreaPathLabel.AutoSize = true;
            this.koreaPathLabel.BackColor = System.Drawing.Color.Transparent;
            this.koreaPathLabel.Location = new System.Drawing.Point(16, 250);
            this.koreaPathLabel.Name = "koreaPathLabel";
            this.koreaPathLabel.Size = new System.Drawing.Size(119, 15);
            this.koreaPathLabel.TabIndex = 0;
            this.koreaPathLabel.Text = "한국 서버 클라이언트";
            // 
            // koreaPathTextBox
            // 
            this.koreaPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.koreaPathTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.koreaPathTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.koreaPathTextBox.ForeColor = System.Drawing.Color.White;
            this.koreaPathTextBox.Location = new System.Drawing.Point(16, 271);
            this.koreaPathTextBox.Name = "koreaPathTextBox";
            this.koreaPathTextBox.ReadOnly = true;
            this.koreaPathTextBox.Size = new System.Drawing.Size(522, 23);
            this.koreaPathTextBox.TabIndex = 0;
            this.koreaPathTextBox.TabStop = false;
            this.koreaPathTextBox.Text = "(미설정)";
            // 
            // koreaPathBrowseButton
            // 
            this.koreaPathBrowseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.koreaPathBrowseButton.Enabled = false;
            this.koreaPathBrowseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.koreaPathBrowseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.koreaPathBrowseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.koreaPathBrowseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.koreaPathBrowseButton.Location = new System.Drawing.Point(550, 270);
            this.koreaPathBrowseButton.Name = "koreaPathBrowseButton";
            this.koreaPathBrowseButton.Size = new System.Drawing.Size(74, 25);
            this.koreaPathBrowseButton.TabIndex = 0;
            this.koreaPathBrowseButton.TabStop = false;
            this.koreaPathBrowseButton.Text = "변경";
            this.koreaPathBrowseButton.UseVisualStyleBackColor = true;
            this.koreaPathBrowseButton.Click += new System.EventHandler(this.koreaPathBrowseButton_Click);
            // 
            // targetLanguageLabel
            // 
            this.targetLanguageLabel.AutoSize = true;
            this.targetLanguageLabel.BackColor = System.Drawing.Color.Transparent;
            this.targetLanguageLabel.Location = new System.Drawing.Point(16, 305);
            this.targetLanguageLabel.Name = "targetLanguageLabel";
            this.targetLanguageLabel.Size = new System.Drawing.Size(142, 15);
            this.targetLanguageLabel.TabIndex = 0;
            this.targetLanguageLabel.Text = "베이스 클라이언트 언어";
            // 
            // targetLanguageComboBox
            // 
            this.targetLanguageComboBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.targetLanguageComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.targetLanguageComboBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.targetLanguageComboBox.ForeColor = System.Drawing.Color.White;
            this.targetLanguageComboBox.FormattingEnabled = true;
            this.targetLanguageComboBox.Location = new System.Drawing.Point(16, 326);
            this.targetLanguageComboBox.Name = "targetLanguageComboBox";
            this.targetLanguageComboBox.Size = new System.Drawing.Size(260, 23);
            this.targetLanguageComboBox.TabIndex = 0;
            this.targetLanguageComboBox.TabStop = false;
            this.targetLanguageComboBox.SelectedIndexChanged += new System.EventHandler(this.targetLanguageComboBox_SelectedIndexChanged);
            // 
            // detectPathsButton
            // 
            this.detectPathsButton.Enabled = false;
            this.detectPathsButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.detectPathsButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.detectPathsButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.detectPathsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.detectPathsButton.Location = new System.Drawing.Point(288, 325);
            this.detectPathsButton.Name = "detectPathsButton";
            this.detectPathsButton.Size = new System.Drawing.Size(160, 25);
            this.detectPathsButton.TabIndex = 0;
            this.detectPathsButton.TabStop = false;
            this.detectPathsButton.Text = "경로 자동 탐색";
            this.detectPathsButton.UseVisualStyleBackColor = true;
            this.detectPathsButton.Click += new System.EventHandler(this.detectPathsButton_Click);
            // 
            // resetPathsButton
            // 
            this.resetPathsButton.Enabled = false;
            this.resetPathsButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.resetPathsButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.resetPathsButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.resetPathsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.resetPathsButton.Location = new System.Drawing.Point(456, 325);
            this.resetPathsButton.Name = "resetPathsButton";
            this.resetPathsButton.Size = new System.Drawing.Size(168, 25);
            this.resetPathsButton.TabIndex = 0;
            this.resetPathsButton.TabStop = false;
            this.resetPathsButton.Text = "경로 리셋";
            this.resetPathsButton.UseVisualStyleBackColor = true;
            this.resetPathsButton.Click += new System.EventHandler(this.resetPathsButton_Click);
            // 
            // openReleaseButton
            // 
            this.openReleaseButton.Enabled = false;
            this.openReleaseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.openReleaseButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.openReleaseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.openReleaseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.openReleaseButton.Location = new System.Drawing.Point(16, 365);
            this.openReleaseButton.Name = "openReleaseButton";
            this.openReleaseButton.Size = new System.Drawing.Size(146, 25);
            this.openReleaseButton.TabIndex = 0;
            this.openReleaseButton.TabStop = false;
            this.openReleaseButton.Text = "생성 폴더 열기";
            this.openReleaseButton.UseVisualStyleBackColor = true;
            this.openReleaseButton.Click += new System.EventHandler(this.openReleaseButton_Click);
            // 
            // openLogsButton
            // 
            this.openLogsButton.Enabled = false;
            this.openLogsButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.openLogsButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.openLogsButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.openLogsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.openLogsButton.Location = new System.Drawing.Point(170, 365);
            this.openLogsButton.Name = "openLogsButton";
            this.openLogsButton.Size = new System.Drawing.Size(146, 25);
            this.openLogsButton.TabIndex = 0;
            this.openLogsButton.TabStop = false;
            this.openLogsButton.Text = "로그 폴더 열기";
            this.openLogsButton.UseVisualStyleBackColor = true;
            this.openLogsButton.Click += new System.EventHandler(this.openLogsButton_Click);
            // 
            // cleanupButton
            // 
            this.cleanupButton.Enabled = false;
            this.cleanupButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.cleanupButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.cleanupButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.cleanupButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cleanupButton.Location = new System.Drawing.Point(324, 365);
            this.cleanupButton.Name = "cleanupButton";
            this.cleanupButton.Size = new System.Drawing.Size(146, 25);
            this.cleanupButton.TabIndex = 0;
            this.cleanupButton.TabStop = false;
            this.cleanupButton.Text = "오래된 파일 정리";
            this.cleanupButton.UseVisualStyleBackColor = true;
            this.cleanupButton.Click += new System.EventHandler(this.cleanupButton_Click);
            // 
            // restoreBackupButton
            // 
            this.restoreBackupButton.Enabled = false;
            this.restoreBackupButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.restoreBackupButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.restoreBackupButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.restoreBackupButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.restoreBackupButton.Location = new System.Drawing.Point(478, 365);
            this.restoreBackupButton.Name = "restoreBackupButton";
            this.restoreBackupButton.Size = new System.Drawing.Size(146, 25);
            this.restoreBackupButton.TabIndex = 0;
            this.restoreBackupButton.TabStop = false;
            this.restoreBackupButton.Text = "백업으로 복구";
            this.restoreBackupButton.UseVisualStyleBackColor = true;
            this.restoreBackupButton.Click += new System.EventHandler(this.restoreBackupButton_Click);
            // 
            // initialChecker
            // 
            this.initialChecker.WorkerReportsProgress = true;
            this.initialChecker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.initialChecker_DoWork);
            this.initialChecker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.initialChecker_ProgressChanged);
            // 
            // installWorker
            // 
            this.installWorker.WorkerReportsProgress = true;
            this.installWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.installWorker_DoWork);
            this.installWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.initialChecker_ProgressChanged);
            // 
            // chatOnlyWorker
            // 
            this.chatOnlyWorker.WorkerReportsProgress = true;
            this.chatOnlyWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.chatOnlyWorker_DoWork);
            this.chatOnlyWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.initialChecker_ProgressChanged);
            // 
            // removeWorker
            // 
            this.removeWorker.WorkerReportsProgress = true;
            this.removeWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.removeWorker_DoWork);
            this.removeWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.initialChecker_ProgressChanged);
            // 
            // buildReleaseWorker
            // 
            this.buildReleaseWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.buildReleaseWorker_DoWork);
            // 
            // preflightWorker
            // 
            this.preflightWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.preflightWorker_DoWork);
            // 
            // FFXIVKoreanPatch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(640, 820);
            this.Controls.Add(this.restoreBackupButton);
            this.Controls.Add(this.cleanupButton);
            this.Controls.Add(this.openLogsButton);
            this.Controls.Add(this.openReleaseButton);
            this.Controls.Add(this.resetPathsButton);
            this.Controls.Add(this.detectPathsButton);
            this.Controls.Add(this.targetLanguageComboBox);
            this.Controls.Add(this.targetLanguageLabel);
            this.Controls.Add(this.koreaPathBrowseButton);
            this.Controls.Add(this.koreaPathTextBox);
            this.Controls.Add(this.koreaPathLabel);
            this.Controls.Add(this.globalPathBrowseButton);
            this.Controls.Add(this.globalPathTextBox);
            this.Controls.Add(this.globalPathLabel);
            this.Controls.Add(this.preflightCheckButton);
            this.Controls.Add(this.debugBuildReleaseButton);
            this.Controls.Add(this.buildReleaseButton);
            this.Controls.Add(this.installButton);
            this.Controls.Add(this.chatOnlyInstallButton);
            this.Controls.Add(this.removeButton);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.downloadLabel);
            this.Controls.Add(this.progressBar);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(239)))), ((int)(((byte)(239)))), ((int)(((byte)(239)))));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FFXIVKoreanPatch";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FFXIV 한글 패치";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label downloadLabel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Button chatOnlyInstallButton;
        private System.Windows.Forms.Button installButton;
        private System.Windows.Forms.Button buildReleaseButton;
        private System.Windows.Forms.Button debugBuildReleaseButton;
        private System.Windows.Forms.Button preflightCheckButton;
        private System.Windows.Forms.Label globalPathLabel;
        private System.Windows.Forms.TextBox globalPathTextBox;
        private System.Windows.Forms.Button globalPathBrowseButton;
        private System.Windows.Forms.Label koreaPathLabel;
        private System.Windows.Forms.TextBox koreaPathTextBox;
        private System.Windows.Forms.Button koreaPathBrowseButton;
        private System.Windows.Forms.Label targetLanguageLabel;
        private System.Windows.Forms.ComboBox targetLanguageComboBox;
        private System.Windows.Forms.Button detectPathsButton;
        private System.Windows.Forms.Button resetPathsButton;
        private System.Windows.Forms.Button openReleaseButton;
        private System.Windows.Forms.Button openLogsButton;
        private System.Windows.Forms.Button cleanupButton;
        private System.Windows.Forms.Button restoreBackupButton;
        private System.ComponentModel.BackgroundWorker initialChecker;
        private System.ComponentModel.BackgroundWorker installWorker;
        private System.ComponentModel.BackgroundWorker chatOnlyWorker;
        private System.ComponentModel.BackgroundWorker removeWorker;
        private System.ComponentModel.BackgroundWorker buildReleaseWorker;
        private System.ComponentModel.BackgroundWorker preflightWorker;
    }
}
