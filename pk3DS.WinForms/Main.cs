/*----------------------------------------------------------------------------*/
/*--  This program is free software: you can redistribute it and/or modify  --*/
/*--  it under the terms of the GNU General Public License as published by  --*/
/*--  the Free Software Foundation, either version 3 of the License, or     --*/
/*--  (at your option) any later version.                                   --*/
/*--                                                                        --*/
/*--  This program is distributed in the hope that it will be useful,       --*/
/*--  but WITHOUT ANY WARRANTY; without even the implied warranty of        --*/
/*--  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the          --*/
/*--  GNU General Public License for more details.                          --*/
/*--                                                                        --*/
/*--  You should have received a copy of the GNU General Public License     --*/
/*--  along with this program. If not, see <http://www.gnu.org/licenses/>.  --*/
/*----------------------------------------------------------------------------*/

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using System.Drawing;
using System.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace pk3DS.WinForms;

    public sealed partial class Main : Form
{
    private System.Windows.Forms.PictureBox PB_Sprite = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Label L_MascotQuote = new System.Windows.Forms.Label();
    private System.Windows.Forms.PictureBox PB_GameIcon = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Label L_Version = new System.Windows.Forms.Label();
    private System.Windows.Forms.Label L_MascotThought = new System.Windows.Forms.Label();
    private System.Windows.Forms.PictureBox PB_Friendship = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Panel PNL_MascotGlass = new System.Windows.Forms.Panel();
    private System.Windows.Forms.Panel PNL_Sidebar = new System.Windows.Forms.Panel();
    private System.Windows.Forms.Button B_Store = new System.Windows.Forms.Button();
    private Color GradientStart = Color.FromArgb(45, 25, 60);
    private Color GradientEnd = Color.FromArgb(30, 30, 30);
    public static Main Instance;
    public Main()
    {
        Instance = this;
        // Initialize the Main Form
        InitializeComponent();
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.Paint += new PaintEventHandler(Main_Paint);
        ShowdownSetManager.Load();
        ExpansionCodeMap.EnsureLoaded();

        // Prepare DragDrop Functionality
        AllowDrop = TB_Path.AllowDrop = true;
        DragEnter += TabMain_DragEnter;
        DragDrop += TabMain_DragDrop;
        TB_Path.DragEnter += TabMain_DragEnter;
        TB_Path.DragDrop += TabMain_DragDrop;
        foreach (var t in TC_RomFS.TabPages.OfType<TabPage>())
        {
            t.AllowDrop = true;
            t.DragEnter += TabMain_DragEnter;
            t.DragDrop += TabMain_DragDrop;
        }

        randomizationToolStripMenuItem.Click += (s, e) => { OpenUniversalRandomizer(); };
        B_UniversalRandomizer.TabStop = false;

        var settings = Properties.Settings.Default;
        if (CB_Lang.Items.Count == 0)
        {
            CB_Lang.Items.AddRange(new object[] { "日本語", "English", "Français", "Italiano", "Deutsch", "Español", "中文", "한국어", "Dutch", "Portuguese", "Russian", "Traditional Chinese" });
        }
        if (settings.Language >= 0 && settings.Language < CB_Lang.Items.Count)
            CB_Lang.SelectedIndex = settings.Language;

        var path = settings.GamePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                OpenQuick(path);
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error($"Unable to automatically load the previously opened ROM dump located at -- {path}.", ex.Message);
                ResetStatus();
            }
        }

        string[] args = Environment.GetCommandLineArgs();
        string filename = args.Length > 0 ? Path.GetFileNameWithoutExtension(args[0]).ToLower() : "";
        skipBoth = filename.Contains("3DSkip");

        const string randset = RandSettings.FileName;
        if (File.Exists(randset))
            RandSettings.Load(File.ReadAllLines(randset));
        else
        {
            string defaultRand = WinFormsUtil.GetInternalText("randsettings.txt");
            if (!string.IsNullOrEmpty(defaultRand))
                RandSettings.Load(defaultRand.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }

        // Lets the tab strip continue the window's gradient instead of guessing a flat colour that
        // only matches it at one point.
        WinFormsUtil.BackgroundProvider = () => _gradientCache;
        WinFormsUtil.BackgroundOwner = this;

        // Nothing may overwrite code.bin or a CRO without the user seeing it first.
        pk3DS.Core.Modding.BinaryWriteGuard.ApprovalHandler = ConfirmBinaryWrite;

        WinFormsUtil.ApplyTheme(this);
        // The pages and the panels inside them, not just the tab strip - see SetDoubleBufferedTree.
        WinFormsUtil.SetDoubleBufferedTree(TC_RomFS);

        // Button widths are derived from the panel, so they have to be recomputed when it changes.
        foreach (var panel in new[] { FLP_RomFS, FLP_ExeFS, FLP_CRO })
        {
            if (panel == null) continue;
            var captured = panel;
            captured.SizeChanged += (s, e) => LayoutEditorButtons(captured);
        }

        // A hidden tab page reports a stale client size, so its panel has to be measured again the
        // first time it is actually shown - otherwise only the tab open at startup lays out right.
        TC_RomFS.SelectedIndexChanged += (s, e) =>
        {
            foreach (var p in TC_RomFS.SelectedTab?.Controls.OfType<FlowLayoutPanel>() ?? [])
                LayoutEditorButtons(p);
        };
        
        // Add Toggle to Options
        var themeToggle = new ToolStripMenuItem("Visual Mode");
        var darkItem = new ToolStripMenuItem("Dark") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Dark };
        var greyItem = new ToolStripMenuItem("Grey") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Grey };
        var lightItem = new ToolStripMenuItem("Light") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Light };
        var purpleItem = new ToolStripMenuItem("Galaxy Purple") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.GalaxyPurple };
        
        darkItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.Dark; greyItem.Checked = lightItem.Checked = purpleItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        greyItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.Grey; darkItem.Checked = lightItem.Checked = purpleItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        lightItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.Light; darkItem.Checked = greyItem.Checked = purpleItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        purpleItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.GalaxyPurple; darkItem.Checked = greyItem.Checked = lightItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        
        themeToggle.DropDownItems.Add(darkItem);
        themeToggle.DropDownItems.Add(greyItem);
        themeToggle.DropDownItems.Add(lightItem);
        themeToggle.DropDownItems.Add(purpleItem);
        Menu_Options.DropDownItems.Add(themeToggle);

        WinFormsUtil.ApplyThemeToMenuItem(themeToggle, WinFormsUtil.CurrentTheme);

        LoadQuotes();
        InitializeMascotUI();
        UpdateMascot();
        AddThemeMenu();
        AddModdingToolsMenu();
    }

    private void InitializeMascotUI()
    {
        // Sidebar Panel - Narrower and Transparent
        this.PNL_Sidebar.Size = new System.Drawing.Size(180, 420);
        this.PNL_Sidebar.Dock = System.Windows.Forms.DockStyle.Right;
        this.PNL_Sidebar.BackColor = System.Drawing.Color.Transparent; this.PNL_Sidebar.Name = "PNL_Sidebar";

        // ----- MASCOT PictureBox — parented to PNL_Sidebar -----
        this.PB_Sprite.Size = new System.Drawing.Size(160, 160);
        this.PB_Sprite.Location = new System.Drawing.Point(10, 10);
        this.PB_Sprite.SizeMode = PictureBoxSizeMode.Zoom;
        this.PB_Sprite.BackColor = Color.Transparent;
        this.PB_Sprite.TabIndex = 0;
        this.PB_Sprite.Visible = true;
        this.PB_Sprite.Click += new System.EventHandler(this.PB_Sprite_Click);
        this.PNL_Sidebar.Controls.Add(this.PB_Sprite);

        // Load a default mascot sprite immediately
        try { 
            object obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject("_800");
            if (obj is Bitmap img) 
            {
                WinFormsUtil.SetImage(this.PB_Sprite, WinFormsUtil.ScaleImage(img, 2));
                this.PB_Sprite.SizeMode = PictureBoxSizeMode.Zoom;
            }
        }
        catch { }



        // Path label for the top area
        var L_PathLabel = new Label { Text = "Path:", Location = new Point(12, 30), AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent };
        this.Controls.Add(L_PathLabel);
        L_PathLabel.BringToFront();

        // Friendship heart - moved to left as requested
        this.PB_Friendship.Location = new System.Drawing.Point(10, 182);
        this.PB_Friendship.Size = new System.Drawing.Size(24, 24);
        this.PB_Friendship.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_Friendship.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.PB_Friendship);
        this.PB_Friendship.BringToFront();
        
        // Quote below the mascot - moved UP
        this.L_MascotQuote.Location = new System.Drawing.Point(5, 210);
        this.L_MascotQuote.Size = new System.Drawing.Size(170, 85);
        this.L_MascotQuote.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);
        this.L_MascotQuote.TextAlign = System.Drawing.ContentAlignment.TopCenter;
        this.L_MascotQuote.ForeColor = System.Drawing.Color.White;
        this.L_MascotQuote.BackColor = System.Drawing.Color.FromArgb(120, 0, 0, 0); // Semi-transparent black rectangle
        this.L_MascotQuote.Padding = new Padding(5);
        this.L_MascotQuote.BorderStyle = BorderStyle.FixedSingle;
        this.PNL_Sidebar.Controls.Add(this.L_MascotQuote);

        // Game icon + version labels (anchored to bottom of sidebar)
        this.PB_GameIcon.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.PB_GameIcon.Location = new System.Drawing.Point(8, 315);
        this.PB_GameIcon.Size = new System.Drawing.Size(32, 32);
        this.PB_GameIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_GameIcon.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.PB_GameIcon);
 
        this.L_Version.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.L_Version.AutoSize = true;
        this.L_Version.Font = new System.Drawing.Font("Segoe UI", 8F);
        this.L_Version.ForeColor = System.Drawing.Color.LightGray;
        this.L_Version.BackColor = System.Drawing.Color.Transparent;
        this.L_Version.Location = new System.Drawing.Point(45, 319);
        this.L_Version.Text = "";
        this.PNL_Sidebar.Controls.Add(this.L_Version);
 
        this.L_Game.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.L_Game.AutoSize = true;
        this.L_Game.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.L_Game.Location = new System.Drawing.Point(45, 334);
        this.L_Game.Size = new System.Drawing.Size(150, 20);
        this.L_Game.ForeColor = System.Drawing.Color.White;
        this.L_Game.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.L_Game);

        // Store button - sleek modern styling aligned with sidebar layout
        this.B_Store.AutoSize = false;
        this.B_Store.Text = "🛒 Store";
        this.B_Store.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.B_Store.Location = new System.Drawing.Point(42, 178);
        this.B_Store.Size = new System.Drawing.Size(130, 28);
        this.B_Store.FlatStyle = FlatStyle.Flat;
        this.B_Store.ForeColor = Color.White;
        this.B_Store.BackColor = Color.FromArgb(40, 160, 220);
        this.B_Store.FlatAppearance.BorderSize = 0;
        this.B_Store.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 180, 240);
        this.B_Store.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 140, 200);
        this.B_Store.Cursor = Cursors.Hand;
        this.B_Store.Click += new System.EventHandler(this.B_Store_Click);
        this.PNL_Sidebar.Controls.Add(this.B_Store);

        // Add sidebar to form — must come LAST so Dock=Right is calculated correctly
        if (!this.Controls.Contains(this.PNL_Sidebar)) this.Controls.Add(this.PNL_Sidebar);
        this.PNL_Sidebar.Visible = true;
        this.PNL_Sidebar.BringToFront();
        this.PB_Sprite.BringToFront();
        this.PB_Sprite.Click += Mascot_Click;
        SetMascotQuote();

        WinFormsUtil.SetDoubleBufferedTree(this.PNL_Sidebar);

        // Expand window to fit content + sidebar
        this.ClientSize = new System.Drawing.Size(800, 450);
        this.MinimumSize = new System.Drawing.Size(800, 490);
        this.MinimizeBox = true;
        this.MaximizeBox = false;
    }

    /// <summary>
    /// Composites the whole window off-screen before it reaches the display.
    /// <para>
    /// The form paints a gradient under everything with <see cref="ControlStyles.UserPaint"/>, but
    /// a TabControl draws its pages itself and its children draw over that in turn. Switching tabs
    /// therefore repainted in layers - background, page, then every button - and each layer was
    /// briefly visible, which is the flicker. Double-buffering the TabControl alone could not fix
    /// it, because that buffers one control while the tearing happens between controls.
    /// </para>
    /// <para>
    /// WS_EX_COMPOSITED moves the whole hierarchy to a single bottom-up composite, so the window
    /// updates in one step no matter how many nested containers are involved.
    /// </para>
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            const int WS_EX_COMPOSITED = 0x02000000;
            cp.ExStyle |= WS_EX_COMPOSITED;
            return cp;
        }
    }

    // The painted background, rendered once and reused. Rebuilt only when the size or theme
    // changes.
    private Bitmap _gradientCache;
    private Size _gradientCacheSize;
    private string _gradientCacheTheme;

    /// <summary>
    /// Paints the cached background.
    /// <para>
    /// This used to build the gradient from scratch on every paint: a dictionary load, a fresh
    /// LinearGradientBrush, a seven-stop ColorBlend and a full-client fill, all per repaint.
    /// Switching tabs fires several repaints in a row, so that cost landed several times in a
    /// single interaction and was the flicker - not a buffering problem, which is why buffering
    /// the tab control did not help. Blitting a prepared bitmap is a memory copy instead.
    /// </para>
    /// </summary>
    private void Main_Paint(object sender, PaintEventArgs e)
    {
        string themeName = GetThemeForGame();
        string customTheme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(customTheme) && customTheme != "Dark" && customTheme != "Light" && customTheme != "Grey" && customTheme != "GalaxyPurple")
            themeName = customTheme;

        var size = ClientSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        if (_gradientCache == null || _gradientCacheSize != size || _gradientCacheTheme != themeName)
        {
            _gradientCache?.Dispose();
            _gradientCache = new Bitmap(size.Width, size.Height);
            var rect = new Rectangle(Point.Empty, size);
            using (var g = Graphics.FromImage(_gradientCache))
            using (var brush = WinFormsUtil.CreateGradientBrush(rect, themeName, 45f)
                               ?? new System.Drawing.Drawing2D.LinearGradientBrush(rect, GradientStart, GradientEnd, 90F))
            {
                g.FillRectangle(brush, rect);
            }
            _gradientCacheSize = size;
            _gradientCacheTheme = themeName;

            // Child controls that continue the background need the new one, and the tab strip is
            // only repainted when something invalidates it.
            TC_RomFS?.Invalidate();
        }

        e.Graphics.DrawImageUnscaled(_gradientCache, 0, 0);
    }

    /// <summary>
    /// Asks before an executable binary is overwritten, and says what is about to change.
    /// <para>
    /// Marshalled to the UI thread because most of these patches run on a worker - a MessageBox
    /// raised from there would appear behind the window or not at all.
    /// </para>
    /// </summary>
    private bool ConfirmBinaryWrite(pk3DS.Core.Modding.BinaryWriteRequest request)
    {
        if (InvokeRequired)
            return (bool)Invoke(new Func<bool>(() => ConfirmBinaryWrite(request)));

        string changed = request.ChangedBytes switch
        {
            < 0 => "Unknown number of bytes differ.",
            0 => "No bytes differ - this write would change nothing.",
            1 => "1 byte differs from the file on disk.",
            _ => $"{request.ChangedBytes:N0} bytes differ from the file on disk.",
        };

        var answer = WinFormsUtil.Prompt(MessageBoxButtons.YesNo,
            $"Allow {request.FileName} to be modified?",
            request.Reason,
            request.Detail,
            changed,
            Environment.NewLine
            + "These patches target fixed offsets from one specific build. If this ROM is a "
            + "different region, revision, or an already-expanded build, the write can corrupt "
            + "unrelated code. A copy of the original is kept alongside it as .orig.");

        return answer == DialogResult.Yes;
    }

    /// <summary>Drops the cached background so the next paint rebuilds it.</summary>
    private void InvalidateGradientCache()
    {
        _gradientCache?.Dispose();
        _gradientCache = null;
        Invalidate();
    }

    private void AddThemeMenu()
    {
        var themeMenu = new ToolStripMenuItem("Visual Theme");

        void AddGenerationMenu(string label, string[] themes)
        {
            var menu = new ToolStripMenuItem(label);
            foreach (var theme in themes)
            {
                string chosen = theme; // captured per iteration, not shared by every handler
                var item = new ToolStripMenuItem(chosen);
                item.Click += (s, e) =>
                {
                    Properties.Settings.Default.CustomTheme = chosen;
                    Properties.Settings.Default.Save();
                    UpdateMascot();
                    this.Invalidate();
                };
                menu.DropDownItems.Add(item);
            }
            themeMenu.DropDownItems.Add(menu);
        }

        AddGenerationMenu("Gen 6 (XY / ORAS)",
        [
            "Xerneas", "Yveltal", "Zygarde", "Groudon", "Kyogre", "Rayquaza", "Deoxys", "Jirachi",
            "Latias", "Latios", "Hoopa", "Diancie", "Sceptile", "Blaziken", "Swampert", "Metagross",
            "Salamence", "Regice", "Regirock", "Registeel",
        ]);

        AddGenerationMenu("Gen 7 (SM / USUM)",
        [
            "Solgaleo", "Lunala", "Dawn Wings Necrozma", "Dusk Mane Necrozma", "Necrozma",
            "Ultra Necrozma", "Magearna", "Zeraora", "Marshadow", "Incineroar", "Primarina",
            "Decidueye",
        ]);

        AddGenerationMenu("Gen 8 (SwSh)",
        [
            "Cinderace", "Rillaboom", "Inteleon", "Zacian", "Zamazenta", "Dragapult", "Eternatus",
            "Calyrex", "Calyrex Ice Rider", "Calyrex Shadow Rider", "Glastrier", "Spectrier",
            "Regieleki", "Regidrago", "Zarude", "Urshifu", "Urshifu Rapid Strike",
        ]);

        AddGenerationMenu("Gen 9 (SV)",
        [
            "Skeledirge", "Meowscarada", "Quaquaval", "Baxcalibur", "Tinkaton", "Bellibolt",
            "Miraidon", "Koraidon", "Roaring Moon", "Iron Valiant", "Chi-Yu", "Ting-Lu",
            "Chien-Pao", "Wo-Chien", "Hydrapple", "Archaludon", "Pecharunt",
            "Ogerpon", "Ogerpon Wellspring", "Ogerpon Hearthflame", "Ogerpon Cornerstone",
            "Okidogi", "Munkidori", "Fezandipiti", "Terapagos",
        ]);

        themeMenu.DropDownItems.Add(new ToolStripSeparator());

        var customMascotItem = new ToolStripMenuItem("Set Custom Mascot Image and Nickname...");
        customMascotItem.Click += (s, e) => {
            using (var ofd = new OpenFileDialog { Filter = "Image Files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif|All Files (*.*)|*.*", Title = "Select Custom Mascot Image" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Properties.Settings.Default.CustomMascotPath = ofd.FileName;
                    string nick = WinFormsUtil.PromptInput("Custom Mascot Nickname", "Enter a custom nickname for your mascot:", Properties.Settings.Default.CustomMascotNickname ?? "Custom Mascot");
                    if (!string.IsNullOrWhiteSpace(nick))
                        Properties.Settings.Default.CustomMascotNickname = nick;
                    Properties.Settings.Default.Save();
                    UpdateMascot();
                }
            }
        };
        themeMenu.DropDownItems.Add(customMascotItem);

        var clearCustomMascotItem = new ToolStripMenuItem("Clear Custom Mascot");
        clearCustomMascotItem.Click += (s, e) => {
            Properties.Settings.Default.CustomMascotPath = "";
            Properties.Settings.Default.CustomMascotNickname = "";
            Properties.Settings.Default.Save();
            UpdateMascot();
        };
        themeMenu.DropDownItems.Add(clearCustomMascotItem);

        WinFormsUtil.ApplyThemeToMenuItem(themeMenu, WinFormsUtil.CurrentTheme);
        Menu_Options.DropDownItems.Add(themeMenu);
        
        string savedTheme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(savedTheme))
            UpdateMascot();
    }

    private void AddModdingToolsMenu()
    {
        var moddingMenu = new ToolStripMenuItem("Modding / Expansion Tools");

        var applyPatchItem = new ToolStripMenuItem("Apply Expansion Patch (Ultra Sun / Ultra Moon)");
        applyPatchItem.Click += (s, e) => {
            if (Config == null)
            {
                WinFormsUtil.Alert("Please open an Ultra Sun or Ultra Moon v1.0 game workspace before applying the expansion patch.");
                return;
            }

            string detected = DetectUltraVersion();
            var versionPrompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel,
                $"Detected workspace: {(detected == "US" ? "Ultra Sun" : detected == "UM" ? "Ultra Moon" : "Gen 7 Game")}\n\n" +
                "Which Expansion Patch version do you want to apply?\n\n" +
                "YES = Ultra Sun (US folder)\n" +
                "NO = Ultra Moon (UM folder)\n" +
                "CANCEL = Abort",
                "Select Game Version for Patch");

            if (versionPrompt == DialogResult.Cancel) return;
            bool targetUS = (versionPrompt == DialogResult.Yes);

            string[] candidateFolders = targetUS ? ["US"] : ["UM"];

            string gameName = targetUS ? "Ultra Sun" : "Ultra Moon";

            string patchSource = null;
            foreach (string folderName in candidateFolders)
            {
                string baseCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
                if (Directory.Exists(baseCandidate)) { patchSource = baseCandidate; break; }

                string cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), folderName);
                if (Directory.Exists(cwdCandidate)) { patchSource = cwdCandidate; break; }
            }

            if (patchSource == null)
            {
                WinFormsUtil.Error($"Expansion patch directory '{candidateFolders[0]}' not found for {gameName}.");
                return;
            }

            var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, 
                $"Are you sure you want to apply the Gen 9 expansion patch for {gameName} from '{Path.GetFileName(patchSource)}' to this workspace?\n\n" +
                "This will patch code.bin, Battle.cro, Bag.cro, Box.cro, expanded GARCs, and apply 4.xdelta model patch to a/0/9/4.");
            if (dr != DialogResult.Yes) return;

            string xdeltaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "4.xdelta");
            if (!File.Exists(xdeltaPath))
                xdeltaPath = Path.Combine(Directory.GetCurrentDirectory(), "4.xdelta");

            bool success = pk3DS.Core.Modding.PokemonPlusPatcher.ApplyPokemonPlusPatch(RomFSPath, ExeFSPath, patchSource, xdeltaPath, out string status);
            if (success)
            {
                if (RomFSPath != null && Directory.Exists(RomFSPath))
                    CheckIfRomFS(RomFSPath);
                if (ExeFSPath != null && Directory.Exists(ExeFSPath))
                    CheckIfExeFS(ExeFSPath);
                Config?.Info?.RecalculateLimits(Config);
                WinFormsUtil.Alert(status + "\n\nWorkspace data successfully reloaded!");
            }
            else
            {
                WinFormsUtil.Error(status);
            }
        };

        // "&&" renders one literal ampersand. A single "&" is taken as the mnemonic marker and is
        // swallowed, which is why this menu read "Expansion Options  Custom Offsets".
        var configItem = new ToolStripMenuItem("Expansion Options && Custom Offsets");
        configItem.Click += (s, e) => {
            var cfg = pk3DS.Core.Modding.ExpansionConfig.Load(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExpansionConfig.json"));
            var sb = new StringBuilder();
            sb.AppendLine("=== Expansion Options ===");
            sb.AppendLine($"Max Species: {cfg.MaxSpecies}");
            sb.AppendLine($"Max Moves: {cfg.MaxMoves}");
            sb.AppendLine($"Max Items: {cfg.MaxItems}");
            sb.AppendLine($"Max Abilities: {cfg.MaxAbilities}");
            sb.AppendLine($"16-Bit Abilities Enabled: {cfg.Enable16BitAbilities}");
            sb.AppendLine();

            sb.AppendLine("=== Custom Offsets (Expansion Pack code map) ===");
            if (!ExpansionCodeMap.IsAvailable)
            {
                sb.AppendLine("code_map.csv could not be loaded.");
            }
            else
            {
                foreach (var byFile in ExpansionCodeMap.Entries.GroupBy(x => x.TargetFile).OrderByDescending(g => g.Count()))
                {
                    var addressed = byFile.Where(x => x.HasAddress).ToList();
                    string span = addressed.Count > 0
                        ? $"  0x{addressed.Min(x => x.StartOffset):X6}-0x{addressed.Max(x => x.EndOffset):X6}"
                        : "";
                    sb.AppendLine();
                    sb.AppendLine($"-- {byFile.Key}  ({byFile.Count()} edits){span}");

                    foreach (var section in byFile.GroupBy(x => x.Section))
                    {
                        var first = section.FirstOrDefault(x => x.HasAddress) ?? section.First();
                        string at = first.HasAddress ? $"0x{first.StartOffset:X6}" : first.Offset;
                        sb.AppendLine($"   {at,-18} {section.Key} ({section.Count()})");
                        if (!string.IsNullOrWhiteSpace(first.Purpose))
                            sb.AppendLine($"   {"",-18} {first.Purpose}");
                    }
                }
            }

            ShowScrollableReport(sb.ToString(), "Expansion Options && Custom Offsets");
        };

        var statusItem = new ToolStripMenuItem("View Modded Game Status");
        statusItem.Click += (s, e) => {
            if (Config == null)
            {
                WinFormsUtil.Alert("No game workspace currently loaded.");
                return;
            }
            Config.Info.RecalculateLimits(Config);

            var checks = new (string File, string Path)[]
            {
                ("code.bin",   ExeFSPath == null ? null
                                 : Directory.GetFiles(ExeFSPath)
                                     .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                                         .Contains("code", StringComparison.OrdinalIgnoreCase))),
                ("Battle.cro", RomFSPath == null ? null : Path.Combine(RomFSPath, "Battle.cro")),
                ("Bag.cro",    RomFSPath == null ? null : Path.Combine(RomFSPath, "Bag.cro")),
            };

            var lines = new List<string>();
            int patched = 0, testable = 0;
            foreach (var (file, path) in checks)
            {
                if (path == null || !File.Exists(path)) { lines.Add($"  {file}: not found"); continue; }
                try
                {
                    bool isPatched = ExpansionCodeMap.IsExpansionPatched(File.ReadAllBytes(path), file);
                    testable++;
                    if (isPatched) patched++;
                    lines.Add($"  {file}: {(isPatched ? "Expansion Pack detected" : "looks like retail")}");
                }
                catch (Exception ex) { lines.Add($"  {file}: could not be read ({ex.GetType().Name})"); }
            }

            string verdict = testable == 0 ? "Could not check any files."
                : patched == testable ? "This game IS modded with the Expansion Pack."
                : patched == 0 ? "This game does NOT appear to be modded."
                : $"PARTIALLY modded - {patched} of {testable} files carry the pack.";

            string statusStr = verdict + "\n\n" + string.Join("\n", lines) +
                               $"\n\nGame Version: {Config.Version}\n" +
                               $"Max Species ID: {Config.Info.MaxSpeciesID}\n" +
                               $"Max Move ID: {Config.Info.MaxMoveID}\n" +
                               $"Max Item ID: {Config.Info.MaxItemID}\n" +
                               $"Max Ability ID: {Config.Info.MaxAbilityID}\n" +
                               $"Applied Patches: {string.Join(", ", pk3DS.Core.Modding.ProjectState.Instance.AppliedPatches)}";
            WinFormsUtil.Alert(statusStr, "Modded Game Status");
        };

        var launchToolkitItem = new ToolStripMenuItem("Open 3DS Toolkit GUI (DotNet.3DS.Toolkit)");
        launchToolkitItem.Click += (s, e) => {
            if (!ExternalRebuilder.LaunchToolkitGUI(msg => UpdateStatus(msg)))
            {
                WinFormsUtil.Error("Could not find ToolkitForm.exe in tools directory.");
            }
        };

        var exportLumaItem = new ToolStripMenuItem("Export Luma3DS / LayeredFS Mod Package (Compact)");
        exportLumaItem.Click += (s, e) => {
            if (Config == null)
            {
                WinFormsUtil.Alert("Please open a game workspace before exporting a Luma3DS mod package.");
                return;
            }

            var fbd = new FolderBrowserDialog { Description = "Select output folder to save the Luma3DS / LayeredFS mod package" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            bool success = pk3DS.Core.Modding.PokemonPlusPatcher.ExportLayeredFSModPackage(RomFSPath, ExeFSPath, fbd.SelectedPath, out string status);
            if (success)
                WinFormsUtil.Alert(status, "Luma3DS / LayeredFS Patch Export");
            else
                WinFormsUtil.Error(status);
        };

        moddingMenu.DropDownItems.Add(applyPatchItem);
        moddingMenu.DropDownItems.Add(exportLumaItem);
        moddingMenu.DropDownItems.Add(configItem);
        moddingMenu.DropDownItems.Add(statusItem);
        moddingMenu.DropDownItems.Add(launchToolkitItem);
        moddingMenu.DropDownItems.Add(new ToolStripSeparator());

        // Several patches used to rewrite code.bin and the CROs as a side effect of an ordinary
        // edit, at offsets only valid for one build. This puts a confirmation in front of them.
        var approveWritesItem = new ToolStripMenuItem("Confirm before editing code.bin / CROs")
        {
            CheckOnClick = true,
            Checked = pk3DS.Core.Modding.BinaryWriteGuard.RequireApproval,
            ToolTipText = "When on, any patch that rewrites an executable binary must be approved first.",
        };
        approveWritesItem.CheckedChanged += (_, _) =>
            pk3DS.Core.Modding.BinaryWriteGuard.RequireApproval = approveWritesItem.Checked;
        moddingMenu.DropDownItems.Add(approveWritesItem);

        var writeHistoryItem = new ToolStripMenuItem("View Binary Write History...");
        writeHistoryItem.Click += (_, _) =>
        {
            var history = pk3DS.Core.Modding.BinaryWriteGuard.History;
            ShowScrollableReport("Binary Write History",
                history.Count == 0
                    ? "No executable binary has been written this session."
                    : string.Join(Environment.NewLine, history));
        };
        moddingMenu.DropDownItems.Add(writeHistoryItem);

        menuStrip1.Items.Add(moddingMenu);
        WinFormsUtil.ApplyThemeToMenuItem(moddingMenu, WinFormsUtil.CurrentTheme);
    }

    private string[] Quotes;
    private void LoadQuotes()
    {
        try {
            string data = WinFormsUtil.GetInternalText("quotes.txt");
            if (string.IsNullOrEmpty(data)) throw new Exception();
            Quotes = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        } catch {
            Quotes = ["Keep up the good work!", "You're doing great!", "Let's make a great game!"];
        }
    }

    private int Friendship
    {
        get => Properties.Settings.Default.MascotFriendship;
        set { Properties.Settings.Default.MascotFriendship = value; Properties.Settings.Default.Save(); UpdateFriendshipUI(); }
    }

    private void Mascot_Click(object sender, EventArgs e)
    {
        Friendship += 1;
        SetMascotQuote();
    }

    private void SetMascotQuote()
    {
        if (Quotes == null || Quotes.Length == 0) LoadQuotes();
        string quote = Quotes[new Random().Next(Quotes.Length)];
        string name = GetMascotName();
        L_MascotQuote.Text = quote.Replace("[PokemonName]", name);
    }

    private string GetMascotName()
    {
        string customNick = Properties.Settings.Default.CustomMascotNickname;
        if (!string.IsNullOrWhiteSpace(customNick))
            return customNick;

        string theme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(theme) && theme != "Dark" && theme != "Light" && theme != "Grey" && theme != "GalaxyPurple")
        {
            if (theme == "Ultra Necrozma") return "Ultra Necrozma";
            return theme;
        }

        if (Config == null) return "Necrozma";
        if (Config.X) return "Xerneas";
        if (Config.Y) return "Yveltal";
        if (Config.AS) return "Kyogre";
        if (Config.OR) return "Groudon";
        if (Config.Sun) return "Solgaleo";
        if (Config.Moon) return "Lunala";
        if (Config.UltraSun) return "Necrozma";
        if (Config.UltraMoon) return "Necrozma";
        return "Mascot";
    }

    private void UpdateFriendshipUI()
    {
        if (Friendship >= 255) WinFormsUtil.SetImage(PB_Friendship, WinFormsUtil.GetFriendshipIcon(3)); // Max
        else if (Friendship >= 150) WinFormsUtil.SetImage(PB_Friendship, WinFormsUtil.GetFriendshipIcon(2)); // Mid
        else if (Friendship >= 50) WinFormsUtil.SetImage(PB_Friendship, WinFormsUtil.GetFriendshipIcon(1)); // Low
        else WinFormsUtil.SetImage(PB_Friendship, WinFormsUtil.GetFriendshipIcon(0)); // None
    }

    public void HandleFriendship(int points)
    {
        int multiplier = 1;
        var items = Properties.Settings.Default.MascotItems ?? "";
        if (items.Contains("Big Root") || items.Contains("Leftovers"))
            multiplier = 2;

        Friendship = Math.Min(255, Friendship + (points * multiplier));
        UpdateFriendshipUI();
    }

    private string currentPath;
    private void OnPathChanged()
    {
        if (currentPath == TB_Path.Text) return;
        currentPath = TB_Path.Text;
        // Reset session-based perks if game path changes
        Properties.Settings.Default.MascotItems = ""; 
        Properties.Settings.Default.Save();
        UpdateMascot();
    }

    private void UpdateMascot()
    {
        // Default Gradient
        GradientStart = Color.FromArgb(45, 25, 60);
        GradientEnd = Color.FromArgb(30, 30, 30);
        int species = 800;
        int form = 0;

        // Apply Custom Theme Override if present
        string customTheme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(customTheme))
        {
            switch (customTheme)
            {
                case "Xerneas": species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); break;
                case "Yveltal": species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); break;
                case "Groudon": species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); break;
                case "Kyogre": species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); break;
                case "Solgaleo": species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); break;
                case "Lunala": species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); break;
                case "Necrozma": species = 800; form = 0; GradientStart = ColorTranslator.FromHtml("#4A4B56"); break;
                case "Dusk Mane Necrozma": species = 800; form = 1; GradientStart = ColorTranslator.FromHtml("#F5E9D0"); break;
                case "Dawn Wings Necrozma": species = 800; form = 2; GradientStart = ColorTranslator.FromHtml("#B2DAE2"); break;
                case "Ultra Necrozma": species = 800; form = 3; GradientStart = ColorTranslator.FromHtml("#FFF79F"); break;
                case "Rayquaza": species = 384; GradientStart = ColorTranslator.FromHtml("#1F8464"); break;
                case "Deoxys": species = 386; GradientStart = ColorTranslator.FromHtml("#E7935D"); break;
                case "Zygarde": species = 718; form = 1; GradientStart = ColorTranslator.FromHtml("#353535"); break;
                case "Magearna": species = 801; GradientStart = ColorTranslator.FromHtml("#D3B6B9"); break;
                case "Zeraora": species = 807; GradientStart = ColorTranslator.FromHtml("#F6D035"); break;
                case "Marshadow": species = 802; GradientStart = ColorTranslator.FromHtml("#4A4B56"); break;
                case "Incineroar": species = 727; GradientStart = ColorTranslator.FromHtml("#CC2121"); break;
                case "Decidueye": species = 724; GradientStart = ColorTranslator.FromHtml("#155C41"); break;
                case "Primarina": species = 730; GradientStart = ColorTranslator.FromHtml("#54B3D4"); break;
                case "Latias": species = 380; GradientStart = ColorTranslator.FromHtml("#E80606"); break;
                case "Latios": species = 381; GradientStart = ColorTranslator.FromHtml("#6A3DFE"); break;
                case "Jirachi": species = 385; GradientStart = ColorTranslator.FromHtml("#F6F39F"); break;
                case "Diancie": species = 719; GradientStart = ColorTranslator.FromHtml("#9EA5B1"); break;
                case "Hoopa": species = 720; GradientStart = ColorTranslator.FromHtml("#FA6FA0"); break;

                default:
                    if (MascotTransforms.ThemeSpecies.TryGetValue(customTheme, out var mapped))
                    {
                        species = mapped.Species;
                        form = mapped.Form;
                    }
                    break;
            }
        }
        else // Fallback to Game-based detection
        {
            string path = (RomFSPath ?? TB_Path.Text).ToLower();

            string ultra = DetectUltraVersion();
            if (ultra == "UM") { species = 800; form = 2; GradientStart = ColorTranslator.FromHtml("#B2DAE2"); }
            else if (ultra == "US") { species = 800; form = 1; GradientStart = ColorTranslator.FromHtml("#F5E9D0"); }
            else if (path.Contains("omega ruby") || path.Contains("oras")) { species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); }
            else if (path.Contains("alpha sapphire")) { species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); }
            else if (path.Contains("sun")) { species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); }
            else if (path.Contains("moon")) { species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); }
            else if (path.Contains("pokemon x") || path.EndsWith(" x")) { species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); }
            else if (path.Contains("pokemon y") || path.EndsWith(" y")) { species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); }
            else if (Config != null)
            {
                if (Config.X) { species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); }
                else if (Config.Y) { species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); }
                else if (Config.AS) { species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); }
                else if (Config.OR) { species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); }
                else if (Config.Sun) { species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); }
                else if (Config.Moon) { species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); }
                // No UltraSun/UltraMoon test here: DetectUltraVersion above already settled the
                // Ultra pair, and Config cannot tell them apart to settle it again.
                else { species = 800; }
            }
            else { species = 800; }
        }

        // Item-based form overrides. The whole table lives in MascotTransforms so the chained and
        // friendship-gated cases sit alongside the simple ones instead of as special cases here.
        var ownedItemsList = (Properties.Settings.Default.MascotItems ?? "")
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim()).ToList();

        form = MascotTransforms.Resolve(species, form, ownedItemsList, Friendship, CalyrexRiderChoice);

        if (species == 386) form = DeoxysForm % 4; // Deoxys cycles on click rather than by item


        // Check if a Custom Mascot image is configured by the user
        Bitmap sprite = null;
        string customMascotPath = Properties.Settings.Default.CustomMascotPath;
        if (!string.IsNullOrEmpty(customMascotPath) && File.Exists(customMascotPath))
        {
            try
            {
                using (var raw = Image.FromFile(customMascotPath))
                {
                    Bitmap customImg = new Bitmap(raw);
                    // Scale to fit within PB_Sprite size (160x160) if larger
                    if (customImg.Width > 160 || customImg.Height > 160)
                    {
                        float scale = Math.Min(160f / customImg.Width, 160f / customImg.Height);
                        int destW = (int)(customImg.Width * scale);
                        int destH = (int)(customImg.Height * scale);
                        Bitmap scaled = new Bitmap(destW, destH);
                        using (Graphics g = Graphics.FromImage(scaled))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(customImg, 0, 0, destW, destH);
                        }
                        sprite = scaled;
                    }
                    else
                    {
                        sprite = customImg;
                    }
                }
            }
            catch { }
        }

        if (sprite == null)
        {
            sprite = (Bitmap)WinFormsUtil.GetSprite(species, form, 0, 0, Config);
            if (sprite == null)
            {
                string resName = $"_{species}";
                if (form > 0) resName += $"_{form}";
                object obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject(resName);
                if (obj is Bitmap resImg) sprite = resImg;

                sprite ??= LoadCustomSprite(resName);

                // Still nothing: fall back within the species before falling back to another one.
                sprite ??= LoadAnySpeciesSprite(species);

                if (sprite == null)
                {
                    obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject("_800");
                    if (obj is Bitmap defImg) sprite = defImg;
                }
            }
            if (sprite != null)
                sprite = WinFormsUtil.ScaleImage(sprite, 2);
        }

        if (sprite != null)
            WinFormsUtil.SetImage(PB_Sprite, sprite);
        
        this.Invalidate(); // Redraw with new gradient
        
        PB_Sprite.Visible = true;
        PB_Sprite.BringToFront();
        PNL_Sidebar.Visible = true;
        PNL_Sidebar.BringToFront();
        
        // Add quote text randomly
        SetMascotQuote();
        UpdateFriendshipUI();
        
        string themeName = GetThemeForGame();
        if (!string.IsNullOrEmpty(customTheme) && customTheme != "Dark" && customTheme != "Light" && customTheme != "Grey" && customTheme != "GalaxyPurple")
            themeName = customTheme;
        if (species == 800 && form == 3) themeName = "Ultra Necrozma";
        WinFormsUtil.ApplyGradient(this, themeName);
        
        // Update Game Info
        if (Config != null)
        {
            string ultraName = DetectUltraVersion();
            L_Game.Text = ultraName == "UM" ? "Pokémon Ultra Moon" : ultraName == "US" ? "Pokémon Ultra Sun"
                : Config.X ? "Pokémon X" : Config.Y ? "Pokémon Y" : Config.OR ? "Pokémon Omega Ruby" : Config.AS ? "Pokémon Alpha Sapphire" : Config.Sun ? "Pokémon Sun" : Config.Moon ? "Pokémon Moon" : "Pokémon Game";
            L_Version.Text = GetGameVersionString();
            WinFormsUtil.SetImage(PB_GameIcon, GetGameIcon());
        }
        else
        {
            L_Game.Text = "No Game Loaded";
            L_Version.Text = "";
            PB_GameIcon.Image = null;
        }
    }

    /// <summary>
    /// Which Ultra game is loaded: "US", "UM", or null when it is not an Ultra title.
    /// </summary>
    private static string DetectUltraVersion()
    {
        string raw = RomFSPath ?? "";
        string path = raw.ToLowerInvariant();

        // Unambiguous spelled-out names win outright. Moon first: a path naming both is a parent
        // folder, and the more specific token should not be masked by the shorter one.
        if (path.Contains("ultra moon") || path.Contains("ultramoon")) return "UM";
        if (path.Contains("ultra sun") || path.Contains("ultrasun")) return "US";

        foreach (string seg in raw.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                                         StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string word in seg.Split([char.Parse(" "), char.Parse("_"), char.Parse("-"), char.Parse(".")],
                                              StringSplitOptions.RemoveEmptyEntries))
            {
                string w = word.Trim().ToLowerInvariant();
                if (w == "um") return "UM";
                if (w == "us") return "US";
            }
        }

        // Only now the config, which cannot tell the pair apart on its own.
        if (Config != null)
        {
            if (Config.Version == pk3DS.Core.GameVersion.UM) return "UM";
            if (Config.Version == pk3DS.Core.GameVersion.US) return "US";
            if (Config.UltraMoon) return "UM";
            if (Config.UltraSun) return "US";
        }
        return null;
    }

    private static string GetGameVersionString()
    {
        if (Config == null) return "";
        string path = (RomFSPath ?? "").ToLower();
        string ultra = DetectUltraVersion();
        if (ultra == "UM") return "Ultra Moon";
        if (ultra == "US") return "Ultra Sun";
        if (Config.Moon || path.Contains("moon")) return "Moon";
        if (Config.Sun || path.Contains("sun")) return "Sun";
        if (Config.AS || path.Contains("alpha sapphire") || path.Contains("sapphire")) return "Alpha Sapphire";
        if (Config.OR || path.Contains("omega ruby") || path.Contains("ruby")) return "Omega Ruby";
        if (Config.Y || path.Contains("pokemon y") || path.EndsWith(" y")) return "Y";
        if (Config.X || path.Contains("pokemon x") || path.EndsWith(" x")) return "X";

        return Config.Version.ToString();
    }

    private static Image GetGameIcon()
    {
        if (Config == null && ExeFSPath == null) return null;
        string ultra = DetectUltraVersion();
        if (ultra == "UM")
        {
            if (SMDH?.LargeIcon?.Icon != null && (SMDH.AppInfo?[1]?.ShortDescription?.Contains("Moon", StringComparison.OrdinalIgnoreCase) ?? false))
                return SMDH.LargeIcon.Icon;
            return GetFallbackGameIcon();
        }
        if (ultra == "US")
        {
            if (SMDH?.LargeIcon?.Icon != null && (SMDH.AppInfo?[1]?.ShortDescription?.Contains("Sun", StringComparison.OrdinalIgnoreCase) ?? false))
                return SMDH.LargeIcon.Icon;
            return GetFallbackGameIcon();
        }

        if (SMDH?.LargeIcon?.Icon != null)
            return SMDH.LargeIcon.Icon;

        return GetFallbackGameIcon();
    }

    private static Bitmap GetFallbackGameIcon()
    {
        if (Config == null) return null;
        string path = (RomFSPath ?? "").ToLower();
        string ultraIcon = DetectUltraVersion();
        if (ultraIcon == "UM")
            return WinFormsUtil.GetSprite(800, 2, 0, 0, Config); // Dawn Wings Necrozma
        if (ultraIcon == "US")
            return WinFormsUtil.GetSprite(800, 1, 0, 0, Config); // Dusk Mane Necrozma
        if (Config.Moon || path.Contains("moon"))
            return WinFormsUtil.GetSprite(792, 0, 0, 0, Config); // Lunala
        if (Config.Sun || path.Contains("sun"))
            return WinFormsUtil.GetSprite(791, 0, 0, 0, Config); // Solgaleo
        if (Config.AS || path.Contains("alpha sapphire") || path.Contains("sapphire"))
            return WinFormsUtil.GetSprite(382, 0, 0, 0, Config); // Kyogre
        if (Config.OR || path.Contains("omega ruby") || path.Contains("ruby"))
            return WinFormsUtil.GetSprite(383, 0, 0, 0, Config); // Groudon
        if (Config.Y || path.Contains("pokemon y"))
            return WinFormsUtil.GetSprite(717, 0, 0, 0, Config); // Yveltal
        if (Config.X || path.Contains("pokemon x"))
            return WinFormsUtil.GetSprite(716, 0, 0, 0, Config); // Xerneas

        return WinFormsUtil.GetSprite(800, 0, 0, 0, Config);
    }

    private string GetThemeForGame()
    {
        string path = (RomFSPath ?? TB_Path.Text).ToLower();
        if (Config != null)
        {
            if (DetectUltraVersion() == "UM") return "Dawn Wings Necrozma";
            if (DetectUltraVersion() == "US") return "Dusk Mane Necrozma";
            if (Config.Moon) return "Lunala";
            if (Config.Sun) return "Solgaleo";
            if (Config.AS) return "Kyogre";
            if (Config.OR) return "Groudon";
            if (Config.Y) return "Yveltal";
            if (Config.X) return "Xerneas";
        }
        if (path.Contains("ultra moon") || path.Contains("ultramoon") || path.Contains(@"\um") || path.Contains("_um") || path.EndsWith("um")) return "Dawn Wings Necrozma";
        if (path.Contains("ultra sun") || path.Contains("ultrasun") || path.Contains(@"\us") || path.Contains("_us") || path.EndsWith("us")) return "Dusk Mane Necrozma";
        if (path.Contains("moon")) return "Lunala";
        if (path.Contains("sun")) return "Solgaleo";
        if (path.Contains("omega ruby") || path.Contains("ruby")) return "Groudon";
        if (path.Contains("alpha sapphire") || path.Contains("sapphire")) return "Kyogre";
        if (path.Contains("pokemon y") || path.EndsWith(" y")) return "Yveltal";
        if (path.Contains("pokemon x") || path.EndsWith(" x")) return "Xerneas";

        return "Necrozma";
    }

    /// <summary>
    /// A sprite from the CustomSprites folder beside the executable, or null when absent.
    /// <para>
    /// Read through memory so the file is not left locked - CustomSprites is a folder the user
    /// drops images into while the editor is running.
    /// </para>
    /// </summary>
    private static Bitmap LoadCustomSprite(string resName)
    {
        try
        {
            string dir = Path.Combine(Application.StartupPath, "CustomSprites");
            foreach (string ext in new[] { ".png", ".bmp", ".gif", ".jpg" })
            {
                string path = Path.Combine(dir, resName + ext);
                if (!File.Exists(path)) continue;
                using var ms = new MemoryStream(File.ReadAllBytes(path));
                return new Bitmap(ms);
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Any sprite at all for a species, preferring its base form.
    /// <para>
    /// Every Gen 8 and Gen 9 mascot ships only as form variants - CustomSprites has _888_1 for
    /// Crowned Zacian but no plain _888 - so an exact lookup missed and the mascot fell all the way
    /// through to the Necrozma default. Showing the species in a form the user did not select is a
    /// far smaller error than showing a different species entirely, so a form sprite is taken as a
    /// last resort before that default.
    /// </para>
    /// </summary>
    private static Bitmap LoadAnySpeciesSprite(int species)
    {
        var exact = LoadCustomSprite($"_{species}");
        if (exact != null) return exact;

        if (pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject($"_{species}") is Bitmap fromResx)
            return fromResx;

        try
        {
            string dir = Path.Combine(Application.StartupPath, "CustomSprites");
            if (!Directory.Exists(dir)) return null;

            // Lowest form index first, so a base-form stand-in beats an exotic one.
            var candidate = Directory.EnumerateFiles(dir, $"_{species}_*.*")
                .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".bmp" or ".gif" or ".jpg")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (candidate == null) return null;

            using var ms = new MemoryStream(File.ReadAllBytes(candidate));
            return new Bitmap(ms);
        }
        catch { return null; }
    }

    /// <summary>
    /// Which rider the player picked for the Reins of Unity, or null if they have not been asked.
    /// <para>
    /// Stored as a marker inside MascotItems rather than as its own setting, because the shop
    /// already persists that string and adding a generated settings key for one nullable int is
    /// more machinery than the choice is worth.
    /// </para>
    /// </summary>
    private int? CalyrexRiderChoice
    {
        get
        {
            string items = Properties.Settings.Default.MascotItems ?? "";
            foreach (var (label, form) in MascotTransforms.ReinsChoices)
            {
                if (items.Contains("Rider:" + label, StringComparison.OrdinalIgnoreCase))
                    return form;
            }
            return null;
        }
    }

    /// <summary>
    /// Asks which rider the Reins of Unity should summon and records the answer. Called once, when
    /// the item is first applied - re-picking means clearing the mascot's items.
    /// </summary>
    private void PromptForRiderChoice()
    {
        if (CalyrexRiderChoice.HasValue) return;

        var choices = MascotTransforms.ReinsChoices;
        var result = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel,
            "The Reins of Unity bind Calyrex to a steed.",
            $"Yes: {choices[0].Label}\nNo: {choices[1].Label}\nCancel: decide later");
        if (result == DialogResult.Cancel) return;

        string picked = result == DialogResult.Yes ? choices[0].Label : choices[1].Label;
        var items = Properties.Settings.Default.MascotItems ?? "";
        Properties.Settings.Default.MascotItems = (items.Length == 0 ? "" : items + ",") + "Rider:" + picked;
        Properties.Settings.Default.Save();
        UpdateMascot();
    }

    private static int DeoxysForm = 0;
    private void PB_Sprite_Click(object sender, EventArgs e)
    {
        // Asking here rather than at purchase time keeps the shop a pure transaction: buying the
        // item never opens a second dialog on top of the shop's own.
        if (Properties.Settings.Default.CustomTheme is "Calyrex"
            && (Properties.Settings.Default.MascotItems ?? "").Contains(MascotTransforms.ChoiceItem))
        {
            PromptForRiderChoice();
        }

        var items = Properties.Settings.Default.MascotItems ?? "";
        if (items.Contains("Meteorite") && PB_Sprite.Image != null)
        {
            // Specifically handling Deoxys form loop
            string customTheme = Properties.Settings.Default.CustomTheme;
            if (customTheme == "Deoxys")
            {
                DeoxysForm++;
                UpdateMascot();
            }
        }
        HandleFriendship(2);
        if (Quotes == null || Quotes.Length == 0) return;
        string quote = Quotes[new Random().Next(Quotes.Length)];
        
        // Contextual Thoughts
        if (TC_RomFS.SelectedTab == Tab_RomFS)
        {
            var active = FLP_RomFS.Controls.OfType<Button>().FirstOrDefault(b => b.Focused);
            if (active == B_Personal) quote = "It dreams about what it would be if it got buffed.";
            else if (active == B_LevelUp) quote = "Maybe it could use a new move or two!";
        }

        SetMascotQuote();
    }

    private void B_Store_Click(object sender, EventArgs e)
    {
        if (Friendship < 100)
        {
            WinFormsUtil.Alert("You need at least 100 Friendship points to open the Store!");
            return;
        }

        var extras = new (string Name, int ID)[]
        {
            ("Solganium Z", 927), ("Lunalium Z", 928),
        };

        // Known icon IDs for the stock items; anything else falls back to the name-keyed sprite
        // cache and then to the generic held-item icon, so a missing ID costs a picture, not a crash.
        var knownIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Red Orb"] = 534, ["Blue Orb"] = 535, ["Meteorite"] = 729,
            ["Latiasite"] = 684, ["Latiosite"] = 685, ["Diancite"] = 764,
            ["Prison Bottle"] = 720, ["Ultranecrozium Z"] = 929,
        };

        var storeItems = extras
            .Concat(MascotTransforms.AllItems()
                .Select(n => (Name: n, ID: knownIds.TryGetValue(n, out int id) ? id : 0)))
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
        var ownedItems = (Properties.Settings.Default.MascotItems ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        using (var f = new Form { Text = "Mascot Store", Size = new Size(400, 500), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog })
        {
            var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            foreach (var item in storeItems)
            {
                var pnl = new Panel { Size = new Size(350, 60), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 5) };
                var pb = new PictureBox { Size = new Size(48, 48), Location = new Point(5, 5), SizeMode = PictureBoxSizeMode.Zoom };
                pb.Image = ItemSpriteCache.Get(item.Name)
                           ?? (item.ID > 0 ? (Bitmap)Properties.Resources.ResourceManager.GetObject($"item_{item.ID}") : null)
                           ?? Properties.Resources.helditem;
                var lbl = new Label { Text = item.Name, Location = new Point(60, 10), Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true };
                var cost = new Label { Text = ownedItems.Contains(item.Name) ? "OWNED" : "50 PTS", Location = new Point(60, 30), AutoSize = true, ForeColor = Color.Gold };
                
                var btnBuy = new Button { Text = "BUY", Size = new Size(60, 30), Location = new Point(280, 15), Enabled = !ownedItems.Contains(item.Name) };
                btnBuy.Click += (s, ev) => {
                    if (Friendship < 50) { WinFormsUtil.Alert("Not enough points!"); return; }
                    Friendship -= 50;
                    ownedItems.Add(item.Name);
                    Properties.Settings.Default.MascotItems = string.Join(",", ownedItems);
                    Properties.Settings.Default.Save();
                    WinFormsUtil.Alert($"Purchased {item.Name}!");
                    UpdateMascot();
                    f.Close();
                };

                pnl.Controls.AddRange(new Control[] { pb, lbl, cost, btnBuy });
                flp.Controls.Add(pnl);
            }
            f.Controls.Add(flp);
            WinFormsUtil.ApplyTheme(f);
            f.ShowDialog();
        }
    }

    public static GameConfig Config;
    public static string RomFSPath;
    public static string ExeFSPath;
    public static string ExHeaderPath;
    private volatile int threads;
    internal static volatile int Language;
    internal static SMDH SMDH;
    private uint HANSgameID; // for exporting RomFS/ExeFS with correct X8 gameID
    private readonly bool skipBoth;
    public static PersonalInfo[] SpeciesStat => Config.Personal.Table;

    // Main Form Methods
    private void L_About_Click(object sender, EventArgs e)
    {
        new About().ShowDialog();
    }

    private void Menu_CustomSprites_Click(object sender, EventArgs e)
    {
        var ed = new CustomSpriteEditor();
        WinFormsUtil.ApplyTheme(ed);
        ed.ShowDialog();
        UpdateMascot(); // Refresh main menu sprite if we edited the mascot's sprite
    }

    private void Menu_CustomNames_Click(object sender, EventArgs e)
    {
        if (Config == null) return;
        var ed = new CustomNameEditor();
        WinFormsUtil.ApplyTheme(ed);
        ed.ShowDialog();
    }

    private void L_GARCInfo_Click(object sender, EventArgs e)
    {
        if (RomFSPath == null)
            return;

        string s = "Game Type: " + Config.Version + Environment.NewLine;
        s = Config.Files.Select(file => file.Name).Aggregate(s, (current, t) => current + string.Format(Environment.NewLine + "{0} - {1}", t, Config.GetGARCFileName(t)));

        var copyPrompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, s, "Copy to Clipboard?");
        if (copyPrompt != DialogResult.Yes)
            return;

        try { Clipboard.SetText(s); }
        catch { WinFormsUtil.Alert("Unable to copy to Clipboard."); }
    }

    private void L_Game_Click(object sender, EventArgs e) { var ed = new EnhancedRestore(Config); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); }

    private void B_Open_Click(object sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            OpenQuick(fbd.SelectedPath);
            OnPathChanged();
        }
    }

    private void ChangeLanguage(object sender, EventArgs e)
    {
        if (InvokeRequired)
            Invoke((MethodInvoker)delegate { Language = CB_Lang.SelectedIndex; });
        else Language = CB_Lang.SelectedIndex;
        if (Config != null)
            Config.Language = Language;
        Menu_Options.DropDown.Close();
        if (!Tab_RomFS.Enabled || Config == null)
            return;

        if ((Config.XY || Config.ORAS) && Language > 7)
        {
            WinFormsUtil.Alert("Language not available for games. Defaulting to English.");
            if (InvokeRequired)
                Invoke((MethodInvoker)delegate { CB_Lang.SelectedIndex = 2; });
            else CB_Lang.SelectedIndex = 2;
            return; // set event re-triggers this method
        }

        UpdateProgramTitle();

        try
        {
            if (!string.IsNullOrEmpty(RomFSPath))
                Config.Initialize(RomFSPath, ExeFSPath, Language);
            else
                Config.InitializeGameText();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not load this language's data.", ex.Message);
            return;
        }

        if (Config.GameTextStrings == null)
        {
            WinFormsUtil.Error("This language's text could not be read; keeping the previous one.");
            return;
        }

        if (!LanguageCoversData(out string shortfall))
        {
            WinFormsUtil.Alert(
                "This language's text has not been expanded to match the ROM." + Environment.NewLine + Environment.NewLine +
                shortfall + Environment.NewLine + Environment.NewLine +
                "Editors would run off the end of the name lists, so the language is being reset to English.");

            if (InvokeRequired)
                Invoke((MethodInvoker)delegate { CB_Lang.SelectedIndex = 2; });
            else CB_Lang.SelectedIndex = 2;
            return; // set event re-triggers this method
        }

        Properties.Settings.Default.Language = Language;
        Properties.Settings.Default.Save();
    }

    /// <summary>
    /// True when the selected language names everything the ROM's data files contain.
    /// </summary>
    private bool LanguageCoversData(out string shortfall)
    {
        shortfall = "";
        try
        {
            var lines = new List<string>();

            int itemData = Config.GetGARCData("item")?.Files.Length ?? 0;
            int itemText = Config.GetText(TextName.ItemNames).Length;
            if (itemData > 0 && itemText < itemData)
                lines.Add($"Items: {itemText} names for {itemData} items");

            int moveData = Config.Moves?.Length ?? 0;
            int moveText = Config.GetText(TextName.MoveNames).Length;
            if (moveData > 0 && moveText < moveData)
                lines.Add($"Moves: {moveText} names for {moveData} moves");

            shortfall = string.Join(Environment.NewLine, lines);
            return lines.Count == 0;
        }
        catch
        {
            return true; // never block on a check that itself failed
        }
    }

    private void Menu_Exit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        try
        {
            var g = Config?.GARCGameText;
            string[][] files = Config?.GameTextStrings;

            if (g?.Files != null && files != null && files.All(f => f != null))
            {
                g.Files = files.Select(x => TextFile.GetBytes(Config, x)).ToArray();
                g.Save();
            }
        }
        catch (Exception ex)
        {
            // Never let saving text stop the program from closing.
            System.Diagnostics.Debug.WriteLine($"Game text was not saved on exit: {ex}");
        }

        try
        {
            var text = RandSettings.Save();
            File.WriteAllLines(RandSettings.FileName, text, Encoding.Unicode);
        }
        catch
        {
            // ignored
        }
    }

    private void OpenQuick(string path)
    {
        if (ThreadActive())
            return;

        try
        {
            if (!Directory.Exists(path)) // File
                OpenFile(path);
            else // Directory
                OpenDirectory(path);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error($"Failed to open -- {path}", ex.Message);
            ResetStatus();
        }
    }

    private void OpenFile(string path)
    {
        if (!File.Exists(path))
            return;

        var fi = new FileInfo(path);
        if (fi.Name.Contains("code.bin")) // Compress/Decompress .code.bin
        {
            OpenExeFSCodeBinary(path, fi);
        }
        else if (fi.Name.Contains("exe", StringComparison.OrdinalIgnoreCase)) // Unpack exefs
        {
            OpenExeFSCombined(path, fi);
        }
        else if (fi.Name.Contains("rom", StringComparison.OrdinalIgnoreCase))
        {
            WinFormsUtil.Alert("RomFS unpacking not implemented.");
        }
        else
        {
            var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Unpack sub-files?", "Cancel: Abort");
            if (dr == DialogResult.Cancel)
                return;
            bool recurse = dr == DialogResult.Yes;
            ToolsUI.OpenARC(path, pBar1, recurse);
        }
    }

    private void OpenExeFSCombined(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 != 0)
            return;
        var dir = Path.GetDirectoryName(path);
        if (dir is null)
            return;

        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS.bin.", "Unpack?");
        if (prompt != DialogResult.Yes)
            return;

        RunWorker("Unpacking the ExeFS", () =>
        {
            ExeFS.UnpackExeFS(path, dir);
            WinFormsUtil.Alert("Unpacked!");
        });
    }

    private void OpenExeFSCodeBinary(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 == 0)
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed code.bin.", "Compress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            RunWorker("Compressing code.bin", () =>
            {
                new BLZCoder(["-en", path], pBar1);
                WinFormsUtil.Alert("Compressed!");
            });
        }
        else
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code.bin.", "Decompress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            RunWorker("Decompressing code.bin", () =>
            {
                new BLZCoder(["-d", path], pBar1);
                WinFormsUtil.Alert("Decompressed!");
            });
        }
    }

    private void OpenDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Check for ROMFS/EXEFS/EXHEADER
        RomFSPath = ExeFSPath = null; // Reset
        Config = null;
        SMDH = null;
        HANSgameID = 0;

        string[] folders = Directory.GetDirectories(path);
        int count = folders.Length;

        // Find RomFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("rom", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfRomFS(f);
        // Find ExeFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("exe", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfExeFS(f);

        if (ExeFSPath != null && File.Exists(Path.Combine(ExeFSPath, "icon.bin")))
        {
            try { SMDH = new SMDH(Path.Combine(ExeFSPath, "icon.bin")); } catch { SMDH = null; }
            HANSgameID = SMDH?.AppSettings?.StreetPassID ?? 0;
        }

        if (count > 3)
            if (Properties.Settings.Default.ShowFolderWarning)
            {
                var msg = "pk3DS will function best if you keep your Game Files folder clean and free of unnecessary folders.\n\nDo you want to hide this warning in the future?";
                if (MessageBox.Show(msg, "Alert", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Properties.Settings.Default.ShowFolderWarning = false;
                    Properties.Settings.Default.Save();
                }
            }

        // Enable buttons if applicable
        Tab_RomFS.Enabled = Menu_Restore.Enabled = Tab_CRO.Enabled = Menu_CRO.Enabled = Menu_Shuffler.Enabled = RomFSPath != null;
        Tab_ExeFS.Enabled = RomFSPath != null && ExeFSPath != null;
        if (RomFSPath != null && Config != null)
        {
            ToggleSubEditors();
            L_Version.Text = GetGameVersionString();
            L_Version.Visible = true;
            WinFormsUtil.SetImage(PB_GameIcon, GetGameIcon());
            
            if (Directory.Exists("personal"))
                Directory.Delete("personal", true); // Force reloading of personal data if the game is switched.

            TB_Path.Text = path;
        }
        else if (ExeFSPath != null)
        {
            L_Game.Text = "ExeFS loaded - no RomFS";
            L_Version.Visible = false;
            PB_GameIcon.Image = null;
            TB_Path.Text = path;
        }
        else
        {
            L_Game.Text = "No Game Loaded";
            L_Version.Visible = false;
            PB_GameIcon.Image = null;
            TB_Path.Text = "";
        }

        if (RomFSPath != null)
        {
            HandleFriendship(1); // Editor open
            UpdateMascot();
            
            // Trigger Data Loading
            if (RTB_Status.Text.Length > 0)
                RTB_Status.Clear();

            UpdateStatus("Data found! Loading persistent data for subforms...", false);
            try
            {
                if (Config is not null)
                {
                    if (ExeFSPath is not null)
                        Config.Initialize(RomFSPath, ExeFSPath, Language);
                }
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error("Failed to initialize game logic.", ex.Message);
                ResetStatus();
                return;
            }
        }

        // Take a pristine backup the first time this game folder is opened.
        if (Config != null && RomFSPath != null)
        {
            try { Config.BackupFiles(); }
            catch (Exception ex) { UpdateStatus("Could not create a backup: " + ex.Message, false); }
        }

        UpdateProgramTitle();

        // Enable Rebuilding options if all files have been found
        CheckIfExHeader(path);
        Menu_ExeFS.Enabled = ExeFSPath != null;
        Menu_RomFS.Enabled = Menu_Restore.Enabled = Menu_GARCs.Enabled = Menu_CustomSprites.Enabled = Menu_CustomNames.Enabled = RomFSPath != null;
        Menu_Patch.Enabled = RomFSPath != null && ExeFSPath != null;
        Menu_3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;
        Menu_Trimmed3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;

        UpdateMascot();
        string gradientName = Config.Version.ToString();
        if (gradientName.Contains("Sun") || gradientName.Contains("Moon")) gradientName = "Solgaleo"; // Default to Solgaleo/Lunala
        if (Config.X) gradientName = "Xerneas";
        if (Config.Y) gradientName = "Yveltal";
        if (Config.OR) gradientName = "Primal Groudon";
        if (Config.AS) gradientName = "Primal Kyogre";
        
        if (Properties.Settings.Default.SelectedGradient != "Default")
            gradientName = Properties.Settings.Default.SelectedGradient;
            
        WinFormsUtil.ApplyGradient(this, gradientName);

        L_Game.Visible = RomFSPath != null;
        TB_Path.Select(TB_Path.TextLength, 0);
        // Method finished.
        System.Media.SystemSounds.Asterisk.Play();
        ResetStatus();
        Properties.Settings.Default.GamePath = path;
        Properties.Settings.Default.Save();
    }

    private void B_ExtractCXI_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a CXI requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select CXI", Filter = "CXI files (*.cxi)|*.cxi" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var inputCXI = ofd.FileName;
        ExtractNCCH(inputCXI, fbd.SelectedPath);
    }

    private void B_Extract3DS_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a 3DS file requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select 3DS", Filter = "3DS files (*.3ds)|*.3ds" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var input3DS = ofd.FileName;
        ExtractNCSD(input3DS, fbd.SelectedPath);
    }

    private void ExtractNCCH(string ncchPath, string outputDirectory)
    {
        if (!File.Exists(ncchPath))
            return;

        var ncch = new NCCH();

        RunWorker("Extracting the NCCH", () =>
        {
            ncch.ExtractNCCHFromFile(ncchPath, outputDirectory, RTB_Status, pBar1);
            WinFormsUtil.Alert("Extraction complete!");
        });
    }

    private void ExtractNCSD(string ncsdPath, string outputDirectory)
    {
        if (!File.Exists(ncsdPath))
            return;

        var ncsd = new NCSD();
        RunWorker("Extracting the NCSD", () =>
        {
            ncsd.ExtractFilesFromNCSD(ncsdPath, outputDirectory, RTB_Status, pBar1);
            WinFormsUtil.Alert("Extraction complete!");
        });
    }

    /// <summary>Editor buttons per row.</summary>
    private const int EditorButtonColumns = 4;

    /// <summary>
    /// Sizes the editor buttons so a fixed number fit per row and the grid fills its panel.
    /// <para>
    /// A FlowLayoutPanel wraps purely on available width, so with fixed-size buttons the column
    /// count was whatever happened to fit - three here - and the remaining width was left empty.
    /// Deriving the width from the panel instead makes the count deliberate and removes the gap;
    /// re-running on resize keeps it true when the window changes.
    /// </para>
    /// </summary>
    /// <summary>
    /// Shows a long report in a scrollable, selectable window.
    /// <para>
    /// A MessageBox truncates and cannot be scrolled or copied out of, which makes it useless for
    /// the code map - the whole value of that listing is being able to look through it and paste an
    /// offset elsewhere.
    /// </para>
    /// </summary>
    private static void ShowScrollableReport(string text, string title)
    {
        using var form = new Form
        {
            Text = title,
            Size = new Size(720, 560),
            StartPosition = FormStartPosition.CenterParent,
        };
        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            Text = text.Replace("\n", Environment.NewLine),
        };
        form.Controls.Add(box);
        WinFormsUtil.ApplyTheme(form);
        form.ShowDialog();
    }

    /// <summary>
    /// How much of a panel is actually on screen, which is not the same as how wide it is.
    /// <para>
    /// The panel is docked inside a tab page that extends past the window's client area and behind
    /// the mascot sidebar, so its own ClientSize is larger than the region the user can see. Sizing
    /// against that put the fourth column in a part of the panel that exists but is clipped - and
    /// made it invisible to a check comparing button.Right against ClientSize.Width, because by the
    /// panel's own reckoning everything fit.
    /// </para>
    /// </summary>
    private int VisibleWidthOf(Control panel)
    {
        int width = panel.ClientSize.Width;
        var form = panel.FindForm();
        if (form == null) return width;

        try
        {
            int panelLeft = panel.PointToScreen(Point.Empty).X;
            int limit = form.PointToScreen(new Point(form.ClientSize.Width, 0)).X;

            // The sidebar overlaps the tab area rather than reducing it, so it clips too.
            if (PNL_Sidebar is { Visible: true })
                limit = Math.Min(limit, PNL_Sidebar.PointToScreen(Point.Empty).X);

            return Math.Max(0, Math.Min(width, limit - panelLeft));
        }
        catch { return width; }
    }

    private void LayoutEditorButtons(FlowLayoutPanel panel)
    {
        if (panel == null || panel.Controls.Count == 0) return;

        var buttons = panel.Controls.OfType<Control>().Where(c => c is not Label).ToList();
        if (buttons.Count == 0) return;

        const int rightReserve = 28;

        int available = VisibleWidthOf(panel) - panel.Padding.Horizontal - rightReserve;
        if (available <= 0) return;

        bool hadAutoScroll = panel.AutoScroll;
        panel.AutoScroll = false;

        // Reserve the vertical scrollbar when the rows cannot fit, since it will appear and take
        // width away the moment the buttons are placed.
        int rows = (int)Math.Ceiling(buttons.Count / (double)EditorButtonColumns);
        int rowHeight = buttons[0].Height + buttons[0].Margin.Vertical;
        if (rows * rowHeight > panel.ClientSize.Height - panel.Padding.Vertical)
            available -= SystemInformation.VerticalScrollBarWidth;

        int spacing = buttons.Max(b => b.Margin.Horizontal);
        int width = ((available - (spacing * EditorButtonColumns)) / EditorButtonColumns) - 1;
        panel.AutoScroll = hadAutoScroll;
        if (width < 60) return; // too narrow to force; let it flow naturally

        // AutoSize wins over any assigned width, so it has to go first or the sizing below is
        // silently discarded and the buttons keep their designer width.
        foreach (var b in buttons)
        {
            if (b is ButtonBase bb) bb.AutoSize = false;
        }

        int usableHeight = panel.ClientSize.Height - panel.Padding.Vertical;
        if (usableHeight > 0)
        {
            int rowCount = (int)Math.Ceiling(buttons.Count / (double)EditorButtonColumns);
            int perRow = usableHeight / Math.Max(1, rowCount);
            int height = Math.Clamp(perRow - buttons[0].Margin.Vertical, 40, 96);
            foreach (var b in buttons)
                b.Height = height;
        }

        for (int attempt = 0; attempt < 12 && width >= 60; attempt++)
        {
            panel.SuspendLayout();
            foreach (var b in buttons)
                b.Width = width;
            panel.ResumeLayout(true);

            // Against the visible edge, not the panel's own width - the panel is wider than what
            // is on screen, so measuring against itself reports a fit that the user cannot see.
            int limit = VisibleWidthOf(panel) - panel.Padding.Right;
            int overhang = buttons.Max(b => b.Right) - limit;

            // Also catch the case where it fits but wrapped early, leaving fewer than four per row.
            int firstRowCount = buttons.Count(b => b.Top == buttons[0].Top);
            if (overhang <= 0 && firstRowCount >= Math.Min(EditorButtonColumns, buttons.Count))
                break;

            width -= Math.Max(1, (overhang + EditorButtonColumns - 1) / EditorButtonColumns);
        }
    }

    private void ToggleSubEditors()
    {
        // Hide all buttons
        foreach (var f in from TabPage t in TC_RomFS.TabPages from f in t.Controls.OfType<FlowLayoutPanel>() select f)
        {
            for (int i = f.Controls.Count - 1; i >= 0; i--)
                f.Controls.Remove(f.Controls[i]);
        }

        B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded

        Control[] romfs, exefs, cro;

        switch (Config.Generation)
        {
            case 6:
                romfs = [B_UniversalRandomizer, B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Maison, B_TitleScreen, B_OWSE,
                ];
                exefs = [B_MoveTutor, B_TMHM, B_Mart, B_Pickup, B_OPower, B_ShinyRate];
                cro = [B_TypeChart, B_Starter, B_Gift, B_Static, B_CROExpander];
                B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded
                break;
            case 7:
                romfs = [B_UniversalRandomizer, B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Royal, B_Pickup, B_OWSE,
                ];
                exefs = [B_TM, B_TypeChart, B_ShinyRate];
                cro = [B_Mart, B_MoveTutor, B_CROExpander, B_ResearchCenter];
                B_MoveTutor.Visible = Config.USUM;

                if (Config.Version != GameVersion.SMDEMO)
                    romfs = [.. romfs, .. new[] { B_Static }];
                break;
            default:
                romfs = exefs = cro = [new Label { Text = "No editors available." }];
                break;
        }

        FLP_RomFS.Controls.AddRange(romfs);
        FLP_ExeFS.Controls.AddRange(exefs);
        FLP_CRO.Controls.AddRange(cro);

        foreach (var panel in new[] { FLP_RomFS, FLP_ExeFS, FLP_CRO })
            LayoutEditorButtons(panel);

        B_UniversalRandomizer.TabStop = false;
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.CurrentTheme);
    }

    private void UpdateProgramTitle() => Text = GetProgramTitle();

    private static string GetProgramTitle()
    {
        string ultra = DetectUltraVersion();
        if (ultra == "UM") return "pk3DS - Pokémon Ultra Moon";
        if (ultra == "US") return "pk3DS - Pokémon Ultra Sun";

        if (SMDH?.AppSettings != null && SMDH.AppInfo != null)
        {
            int[] AILang = [0, 0, 1, 2, 4, 3, 5, 7, 8, 9, 6, 11];
            int langIdx = Language >= 0 && Language < AILang.Length ? AILang[Language] : 1;
            if (langIdx < SMDH.AppInfo.Length && !string.IsNullOrEmpty(SMDH.AppInfo[langIdx]?.ShortDescription))
                return "pk3DS - " + SMDH.AppInfo[langIdx].ShortDescription;
        }

        if (Config != null)
        {
            string v = GetGameVersionString();
            return string.IsNullOrEmpty(v) ? "pk3DS" : "pk3DS - Pokémon " + v;
        }

        return "pk3DS";
    }

    private static GameConfig CheckGameType(string[] files)
    {
        try
        {
            if (files.Length > 1000)
                return null;
            var parent = Directory.GetParent(files[0]);
            if (parent is null)
                return null;

            string[] fileArr = Directory.GetFiles(Path.Combine(parent.FullName, "a"), "*", SearchOption.AllDirectories);
            int fileCount = fileArr.Count(file => Path.GetFileName(file).Length == 1);
            return new GameConfig(fileCount);
        }
        catch { }
        return null;
    }

    private static bool CheckIfRomFS(string path)
    {
        string[] top = Directory.GetDirectories(path);
        var fi = new FileInfo(top[top.Length > 1 ? 1 : 0]);
        // Check to see if the folder is romfs
        if (fi.Name == "a")
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            var cfg = CheckGameType(files);

            if (cfg == null)
            {
                RomFSPath = null;
                Config = null;
                WinFormsUtil.Error("File count does not match expected game count.", "Files: " + files.Length);
                return false;
            }

            RomFSPath = path;
            Config = cfg;
            pk3DS.Core.Modding.ProjectState.SetRomFS(path);
            return true;
        }
        WinFormsUtil.Error("Folder does not contain an 'a' folder in the top level.");
        RomFSPath = null;
        return false;
    }

    private bool CheckIfExeFS(string path)
    {
        string[] files = Directory.GetFiles(path);
        if (files.Length == 1 && string.Equals(Path.GetFileName(files[0]), "exefs.bin", StringComparison.OrdinalIgnoreCase))
        {
            // Prompt if the user wants to unpack the ExeFS.
            if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS binary.", "Unpack?"))
                return false;

            // User wanted to unpack. Unpack.
            if (!ExeFS.UnpackExeFS(files[0], path))
                return false; // on unpack fail

            // Remove ExeFS binary after unpacking
            File.Delete(files[0]);

            files = Directory.GetFiles(path);
            // unpack successful, continue onward!
        }

        string cBin = Path.Combine(path, "code.bin");
        string dotCBin = Path.Combine(path, ".code.bin");
        if (File.Exists(cBin) && File.Exists(dotCBin) && SameFile(cBin, dotCBin))
            try { File.Delete(cBin); } catch { }

        string bBnr = Path.Combine(path, "banner.bnr");
        string bBin = Path.Combine(path, "banner.bin");
        if (File.Exists(bBnr) && File.Exists(bBin))
            try { File.Delete(bBnr); } catch { }

        string iIcn = Path.Combine(path, "icon.icn");
        string iBin = Path.Combine(path, "icon.bin");
        if (File.Exists(iIcn) && File.Exists(iBin))
            try { File.Delete(iIcn); } catch { }

        files = Directory.GetFiles(path);

        if (files.Length < 3 || files.Length > 6)
            return false;

        var fi = new FileInfo(files[0]);
        if (!fi.Name.Contains("code"))
        {
            if (new FileInfo(files[1]).Name != "code.bin")
                return false;

            File.Move(files[1], Path.Combine(Path.GetDirectoryName(files[1]), ".code.bin"));
            files = Directory.GetFiles(path);
            fi = new FileInfo(files[0]);
        }
        if (fi.Length % 0x200 != 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code binary.", "Decompress? File will be replaced.") == DialogResult.Yes)
            RunWorker("Decompressing code.bin", () => { new BLZCoder(["-d", files[0]], pBar1); WinFormsUtil.Alert("Decompressed!"); });

        ExeFSPath = path;
        return true;
    }

    private static bool CheckIfExHeader(string path)
    {
        ExHeaderPath = null;
        // Input folder path should contain the ExHeader.
        string[] files = Directory.GetFiles(path);
        foreach (string fp in from s in files let f = new FileInfo(s) where (f.Name.StartsWith("exh", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("decryptedexh", StringComparison.OrdinalIgnoreCase)) && f.Length == 0x800 select s)
            ExHeaderPath = fp;

        return ExHeaderPath != null;
    }

    private bool ThreadActive()
    {
        if (threads <= 0)
            return false;
        WinFormsUtil.Alert("Please wait for all operations to finish first."); return true;
    }

    private void TabMain_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) is true)
            e.Effect = DragDropEffects.Copy;
    }

    private void TabMain_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: not 0 } files)
            return;
        string path = files[0]; // open first D&D
        OpenQuick(path);
    }

    // RomFS Subform Items
    private void RebuildRomFS(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild RomFS?") != DialogResult.Yes)
            return;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".romfs" : "romfs.bin",
            Filter = "HANS RomFS|*.romfs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                try
                {
                    UpdateStatus(Environment.NewLine + "Building RomFS binary...");
                    bool success = ExternalRebuilder.RebuildRomFS(RomFSPath, sfd.FileName, msg => UpdateStatus(msg));
                    if (!success)
                    {
                        UpdateStatus("3dstool build failed or unavailable; building RomFS binary internally...");
                        RomFS.BuildRomFS(RomFSPath, sfd.FileName, RTB_Status, pBar1);
                    }
                    HandleFriendship(5);
                    UpdateStatus("RomFS Binary rebuild complete." + Environment.NewLine);
                    WinFormsUtil.Alert("Wrote RomFS Binary:", sfd.FileName);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"RomFS rebuild error: {ex.Message}" + Environment.NewLine);
                }
                finally
                {
                    Interlocked.Decrement(ref threads);
                }
            }).Start();
        }
    }


    private void B_UniversalRandomizer_Click(object sender, EventArgs e)
    {
        OpenUniversalRandomizer();
    }

    private void OpenUniversalRandomizer()
    {
        bool? competitive = WinFormsUtil.PromptRandomizerMode();
        if (competitive == null) return; // user closed the prompt without choosing

        // This window was the one editor that never had the theme applied, so it opened in the
        // system's own colours - including a native, light tab strip against everything else.
        using var form = new pk3DS.WinForms.Subforms.UniversalRandomizerForm(competitive.Value);
        WinFormsUtil.ApplyTheme(form);
        form.ShowDialog();
    }

    /// <summary>
    /// Runs an editor on a worker thread, turning a failure into a message instead of a crash.
    /// <para>
    /// Every one of these handlers reaches straight into <see cref="Config"/> and its GARCs. When a
    /// ROM has not finished loading, or a language's archive is missing, those are null and the
    /// dereference throws on a thread with no handler - which Windows Forms can only turn into a
    /// process kill. The editors themselves are unchanged; this only decides what happens when the
    /// data they need is not there.
    /// </para>
    /// </summary>
    private void RunEditor(string what, Action work) => StartGuarded(what, work, count: false, editor: true);

    /// <summary>
    /// Runs non-editor background work (packing, compression, extraction) under the same guard.
    /// <para>
    /// These sites all bracketed themselves with <c>Interlocked.Increment</c>/<c>Decrement</c> on
    /// <see cref="threads"/> but without a <c>finally</c>, so a throw did two things: it killed the
    /// process, and - had it not - it would have left the counter stuck above zero, after which
    /// <see cref="ThreadActive"/> refuses every later operation for the rest of the session. Both
    /// are handled here so no caller has to remember either half.
    /// </para>
    /// </summary>
    private void RunWorker(string what, Action work) => StartGuarded(what, work, count: true, editor: false);

    /// <summary>
    /// The one place a worker thread is created, so the guard cannot be forgotten at a call site.
    /// <para>
    /// The thread is deliberately left in the foreground. Marking it background would let the
    /// runtime tear it down at exit, which for a GARC repack or a RomFS build means a half-written
    /// file on disk - a worse outcome than the wait.
    /// </para>
    /// <para>
    /// The busy count is raised on the calling thread rather than inside the lambda: incrementing
    /// inside meant a window between <c>Start()</c> and the thread being scheduled during which
    /// <see cref="ThreadActive"/> still read zero and a second operation could start on the same
    /// data.
    /// </para>
    /// </summary>
    private void StartGuarded(string what, Action work, bool count, bool editor)
    {
        if (count)
            Interlocked.Increment(ref threads);

        new Thread(() =>
        {
            try
            {
                if (editor && Config == null)
                {
                    Invoke(() => WinFormsUtil.Alert("No game is loaded.", $"Open a ROM before using the {what} editor."));
                    return;
                }
                work();
            }
            catch (Exception ex)
            {
                ReportWorkerFailure(what, ex, editor);
            }
            finally
            {
                if (count)
                    Interlocked.Decrement(ref threads);
            }
        }).Start();
    }

    /// <summary>
    /// Surfaces a background failure on the UI thread, tolerating a form that is already closing.
    /// </summary>
    private void ReportWorkerFailure(string what, Exception ex, bool editor)
    {
        string title = editor ? $"The {what} editor could not open." : $"{what} could not be completed.";
        string detail = ex is NullReferenceException
            ? "Some of the game data it needs is missing. This usually means the ROM did not finish loading, "
              + "or the selected language has no data for it."
            : ex.Message;

        try
        {
            if (IsHandleCreated && !IsDisposed)
                Invoke(() => WinFormsUtil.Error(title, detail));
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>Fetches a GARC, or explains which one is missing rather than throwing later.</summary>
    private bool TryGetGarc(string name, out GARCFile garc)
    {
        garc = Config?.GetGARCData(name);
        if (garc?.Files != null) return true;
        Invoke(() => WinFormsUtil.Error("Missing game data.",
            $"'{name}' could not be read from this ROM. If the language was changed recently, try setting it back to English."));
        return false;
    }

    private void B_GameText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Game Text", () =>
        {
            var g = Config.GARCGameText;
            string[][] files = Config.GameTextStrings;
            if (g?.Files == null || files == null)
            {
                Invoke(() => WinFormsUtil.Error("Game text is not loaded.",
                    "The text archive for the selected language could not be read."));
                return;
            }
            Invoke(() => { var ed = new TextEditor(files, "gametext"); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            g.Files = TryWriteText(files, g);
            g.Save();
        });
    }

    private void B_StoryText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        // The thread here was constructed and never started, so this button did nothing at all.
        RunEditor("Story Text", () =>
        {
            if (!TryGetGarc("storytext", out var g))
                return;
            string[][] files = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => { var ed = new TextEditor(files, "storytext"); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            g.Files = TryWriteText(files, g);
            g.Save();
        });
    }

    private static byte[][] TryWriteText(string[][] files, GARCFile g)
    {
        byte[][] data = new byte[files.Length][];
        var errata = new List<string>();
        for (int i = 0; i < data.Length; i++)
        {
            try
            {
                data[i] = TextFile.GetBytes(Config, files[i]);
            }
            catch (Exception ex)
            {
                errata.Add($"File {i:000} | {ex.Message}");
                // revert changes
                data[i] = g.GetFile(i);
            }
        }
        if (errata.Count == 0)
            return data;

        string[] options =
        [
            "Cancel: Discard all changes",
            "Yes: Save changes, dump errata/failed text",
            "No: Save changes, don't dump errata/failed text",
        ];
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Errors found while attempting to save text."
                                                                    + Environment.NewLine + "Example: " + errata[0],
            string.Join(Environment.NewLine, options));
        if (dr == DialogResult.Cancel)
            return g.Files; // discard
        if (dr == DialogResult.No)
            return data;

        const string txt_errata = "text_errata.txt";
        const string txt_failed = "text_failed.txt";
        File.WriteAllLines(txt_errata, errata);
        TextEditor.ExportTextFile(txt_failed, true, files);

        WinFormsUtil.Alert("Saved text files to path: " + Application.StartupPath,
            txt_errata + Environment.NewLine + txt_failed);

        return data;
    }

    private void B_Maison_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        DialogResult dr;
        switch (Config.Generation)
        {
            case 6:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Super Maison instead of Normal Maison?", "Yes = Super, No = Normal, Cancel = Abort");
                break;
            case 7:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Battle Royal instead of Battle Tree?", "Yes = Royal, No = Tree, Cancel = Abort");
                break;
            default:
                return;
        }
        if (dr == DialogResult.Cancel)
            return;

        RunEditor("Battle Maison", () =>
        {
            bool super = dr == DialogResult.Yes;
            string c = super ? "S" : "N";
            var trdata = Config.GetGARCData("maisontr" + c);
            var trpoke = Config.GetGARCData("maisonpk" + c);
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new MaisonEditor6(trd, trp, super); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new MaisonEditor7(trd, trp, super); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            trdata.Files = trd;
            trpoke.Files = trp;
            trdata.Save();
            trpoke.Save();
        });
    }

    private void B_Personal_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Personal Stats", () =>
        {
            if (Config.GARCPersonal?.Files == null || Config.GARCLearnsets?.Files == null)
            {
                Invoke(() => WinFormsUtil.Error("Missing game data.", "Personal or learnset data could not be read from this ROM."));
                return;
            }
            if (!TryGetGarc("eggmove", out var ge) || !TryGetGarc("evolution", out var gev)) return;

            byte[][] d = Config.GARCPersonal.Files;
            var gl = Config.GARCLearnsets;
            byte[][] l = gl.Files;
            byte[][] eg = ge.Files;
            byte[][] ev = gev.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new PersonalEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    PersonalEditor7 ped = null;
                    Invoke(() => { ped = new PersonalEditor7(d, l, eg, ev); WinFormsUtil.ApplyTheme(ped); HandleFriendship(1); ped.ShowDialog(); });
                    if (ped != null)
                    {
                        d = ped.Files;
                        l = ped.Learnsets;
                        eg = ped.EggMoves;
                        ev = ped.EvolutionFiles;
                    }
                    break;
            }

            // Set Master Table back (Safely reconstruct to avoid length mismatches)
            if (d.Length > 1)
            {
                int entryLen = d[0].Length;
                // In Gen 7, all personal entries are the same size. 
                // The Master Table is the last entry and is much larger.
                var actualEntries = d.Where(f => f != null && f.Length == entryLen).ToList();
                int tableSize = actualEntries.Count * entryLen;
                byte[] masterTable = new byte[tableSize];
                for (int i = 0; i < actualEntries.Count; i++)
                    actualEntries[i].CopyTo(masterTable, i * entryLen);

                // Re-assemble the file list: [Entries...] + [MasterTable]
                var finalFiles = actualEntries.ToArray();
                Array.Resize(ref finalFiles, finalFiles.Length + 1);
                finalFiles[^1] = masterTable;
                d = finalFiles;
            }

            Config.GARCPersonal.Files = d;
            Config.GARCPersonal.Save();
            Config.InitializePersonal();

            // Save any changes from jumps
            gl.Files = l;
            gl.Save();
            Config.InitializeLearnset();
            
            var ge_new = Config.GetGARCData("eggmove");
            if (ge_new != null)
            {
                ge_new.Files = eg;
                ge_new.Save();
            }

            var g_evo = Config.GetGARCData("evolution");
            if (g_evo != null)
            {
                g_evo.Files = ev;
                g_evo.Save();
                Config.InitializeEvos();
            }
        });
    }

    private void B_Trainer_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Trainer", () =>
        {
            if (!TryGetGarc("trclass", out var trclass)) return;
            if (!TryGetGarc("trdata", out var trdata)) return;
            if (!TryGetGarc("trpoke", out var trpoke)) return;
            byte[][] trc = trclass.Files;
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;

            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new RSTE(trd, trp); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { 
                        try {
                            var ed = new SMTE(trd, trp); 
                            WinFormsUtil.ApplyTheme(ed); 
                            HandleFriendship(1); 
                            ed.ShowDialog(); 
                        } catch (Exception ex) {
                            WinFormsUtil.Error("Failed to open Trainer Editor (SMTE):\n" + ex.Message + "\n" + ex.StackTrace);
                        }
                    });
                    break;
            }
            trclass.Files = trc;
            trdata.Files = trd;
            trpoke.Files = trp;
            trclass.Save();
            trdata.Save();
            trpoke.Save();
        });
    }

    private void B_Wild_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Wild Encounter", () =>
        {
            switch (Config.Generation)
            {
                case 6:
                {
                    Action action;
                    if (Config.ORAS)
                        action = () => { var ed = new RSWE(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); };
                    else if (Config.XY)
                        action = () => { var ed = new XYWE(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); };
                    else return;

                    string[] files = ["encdata"];
                    SetMainEnabled(false);
                    try
                    {
                        FileGet(files, false);
                        Invoke(action);
                        FileSet(files);
                    }
                    finally { SetMainEnabled(true); }
                    break;
                }
                case 7:
                {
                    string[] files = ["encdata", "zonedata", "worlddata"];
                    SetMainEnabled(false);
                    Interlocked.Increment(ref threads);
                    try
                    {
                        UpdateStatus($"GARC Get: {files[0]}... ");
                        var ed = Config.GetlzGARCData(files[0]);
                        UpdateStatus($"GARC Get: {files[1]}... ");
                        var zd = Config.GetlzGARCData(files[1]);
                        UpdateStatus($"GARC Get: {files[2]}... ");
                        var wd = Config.GetlzGARCData(files[2]);
                        if (ed == null || zd == null || wd == null)
                        {
                            Invoke(() => WinFormsUtil.Error("Missing game data.",
                                "The encounter, zone or world archive could not be read from this ROM."));
                            return;
                        }

                        UpdateStatus("Running SMWE... ");
                        Invoke(() => { var editor = new SMWE(ed, zd, wd); WinFormsUtil.ApplyTheme(editor); HandleFriendship(1); editor.ShowDialog(); });

                        UpdateStatus($"GARC Set: {files[0]}... ");
                        ed.Save();
                        ResetStatus();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref threads);
                        SetMainEnabled(true);
                    }
                    break;
                }
                default:
                    return;
            }
        });
    }

    /// <summary>
    /// Enables or disables the main window from a worker thread, tolerating a closed form.
    /// </summary>
    private void SetMainEnabled(bool enabled)
    {
        try
        {
            if (IsHandleCreated && !IsDisposed)
                Invoke((MethodInvoker)delegate { Enabled = enabled; });
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void B_OWSE_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (Properties.Settings.Default.ShowOWSEWarning)
        {
            var msg = "The OverWorld/ScriptEditor handles things more advanced that basic data editing; make sure you know what you're doing if you want to edit anything (besides items)!\n\nDo you want to disable this notification in the future?";
            var result = MessageBox.Show(msg, "Prompt", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
            {
                Properties.Settings.Default.ShowOWSEWarning = false;
                Properties.Settings.Default.Save();
            }
            else if (result == DialogResult.Cancel)
            {
                return;
            }
        }
        switch (Config.Generation)
        {
            case 6:
                RunOWSE6();
                return;
            case 7:
                RunOWSE7();
                return;
        }
    }

    private void RunOWSE6()
    {
        Enabled = false;
        RunEditor("Overworld", () =>
        {
            bool reload = ModifierKeys is Keys.Control or (Keys.Alt | Keys.Control);
            string[] files = ["encdata", "storytext", "mapGR", "mapMatrix"];
            if (reload || files.Sum(t => Directory.Exists(t) ? 0 : 1) != 0) // Dev bypass if all exist already
                FileGet(files, false);

            // Don't set any data back. Just view.
            {
                var g = Config.GetGARCData("storytext");
                string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
                Invoke(() => { var ed = new OWSE(); WinFormsUtil.ApplyTheme(ed); ed.Show(); });
                Invoke(() => { var te = new TextEditor(tfiles, "storytext"); WinFormsUtil.ApplyTheme(te); te.Show(); });
                while (Application.OpenForms.Count > 1)
                    Thread.Sleep(200);
            }
            Invoke((MethodInvoker)delegate { Enabled = true; });
            FileSet(files);
        });
    }

    private void RunOWSE7()
    {
        Enabled = false;
        RunEditor("Overworld", () =>
        {
            var files = new[] { "encdata", "zonedata", "worlddata" };
            UpdateStatus($"GARC Get: {files[0]}... ");
            var ed = Config.GetlzGARCData(files[0]);
            UpdateStatus($"GARC Get: {files[1]}... ");
            var zd = Config.GetlzGARCData(files[1]);
            UpdateStatus($"GARC Get: {files[2]}... ");
            var wd = Config.GetlzGARCData(files[2]);

            var g = Config.GetGARCData("storytext");
            string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => { var te = new TextEditor(tfiles, "storytext"); WinFormsUtil.ApplyTheme(te); te.Show(); });
            Invoke(() => { var ow = new OWSE7(ed, zd, wd); WinFormsUtil.ApplyTheme(ow); ow.Show(); });
            while (Application.OpenForms.Count > 1)
                Thread.Sleep(200);
            Invoke((MethodInvoker)delegate { Enabled = true; });
        });
    }

    private void B_Evolution_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Evolution", () =>
        {
            var g = Config.GetGARCData("evolution");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new EvolutionEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new EvolutionEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            Config.InitializeEvos();
            g.Save();
        });
    }

    private void B_MegaEvo_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Mega Evolution", () =>
        {
            var g = Config.GetGARCData("megaevo");
            byte[][] d = g.Files;

            // Auto-expand megaevo GARC to match personal GARC for custom forms
            if (Config.Generation == 7)
            {
                int personalCount = Config.Personal.Table.Length;
                if (d.Length < personalCount)
                {
                    int oldLen = d.Length;
                    Array.Resize(ref d, personalCount);
                    for (int i = oldLen; i < personalCount; i++)
                        d[i] = new byte[16]; // Empty mega evo entry
                }
            }

            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new MegaEvoEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new MegaEvoEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            g.Save();
        });
    }

    private void B_Item_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Item", () =>
        {
            var g = Config.GetGARCData("item");
            byte[][] d = g.Files;
            int oldLen = d.Length;
            
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new ItemEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    ItemEditor7 editor7 = null;
                    Invoke(() => { editor7 = new ItemEditor7(d); WinFormsUtil.ApplyTheme(editor7); HandleFriendship(1); editor7.ShowDialog(); });
                    if (editor7 != null) d = editor7.Files; // Array.Resize in ChangeEntry may create a new array; recapture it
                    break;
            }
            g.Files = d;
            g.Save();
            
            if (Config.Generation == 7 && d.Length > oldLen)
            {
                int maxItemID = d.Length - 1;
                ushort[] emptyItemList = new ushort[0];
                pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMBattleBagPatch(Config.RomFS, maxItemID, emptyItemList);
                pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMItemAttributesPatch(Config.RomFS, maxItemID, emptyItemList);
            }

            // Persist any game text changes made by the item editor (item names/flavor)
            var gt = Config.GARCGameText;
            gt.Files = TryWriteText(Config.GameTextStrings, gt);
            gt.Save();
            Config.InitializeGameText();
        });
    }

    private void B_Move_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Move", () =>
        {
            var g = Config.GARCMoves;
            byte[][] Moves;
            switch (Config.Generation)
            {
                case 6:
                    bool isMini = Config.ORAS;
                    Moves = isMini ? Mini.UnpackMini(g.GetFile(0), "WD") : g.Files;
                    Invoke(() => new MoveEditor6(Moves).ShowDialog());
                    g.Files = isMini ? [Mini.PackMini(Moves, "WD")] : Moves;
                    break;
                case 7:
                    Moves = Mini.UnpackMini(g.GetFile(0), "WD");
                    Invoke(() => 
                    {
                        var editor = new MoveEditor7(Moves);
                        WinFormsUtil.ApplyTheme(editor);
                        HandleFriendship(1);
                        editor.ShowDialog();
                        Moves = editor.Files;
                    });
                    g.Files = [Mini.PackMini(Moves, "WD")];
                    break;
            }
            g.Save();
            Config.InitializeMoves();
        });
    }

    private void B_LevelUp_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Level Up Moves", () =>
        {
            byte[][] d = Config.GARCLearnsets.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new LevelUpEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new LevelUpEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            Config.GARCLearnsets.Files = d;
            Config.GARCLearnsets.Save();
            Config.InitializeLearnset();
        });
    }

    private void B_EggMove_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Egg Move", () =>
        {
            var g = Config.GetGARCData("eggmove");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new EggMoveEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new EggMoveEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            g.Save();
        });
    }

    private void B_TitleScreen_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        RunEditor("Title Screen", () =>
        {
            string[] files = ["titlescreen"];
            FileGet(files); // Compressed files exist, handled in the other form since there's so many
            Invoke(() => { var ed = new TitleScreenEditor6(); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            FileSet(files);
        });
    }
    // RomFS File Requesting Method Wrapper
    private void FileGet(string[] files, bool skipDecompression = true, bool skipGet = false)
    {
        if (skipGet || skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Get: {toEdit} @ {GARC}... ");
            ThreadGet(Path.Combine(RomFSPath, GARC), toEdit, true, skipDecompression);
            while (threads > 0) Thread.Sleep(50);
            ResetStatus();
        }
    }

    private void FileSet(IEnumerable<string> files, bool keep = false)
    {
        if (skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Set: {toEdit} @ {GARC}... ");
            ThreadSet(Path.Combine(RomFSPath, GARC), toEdit, 4); // 4 bytes for Gen6
            while (threads > 0) Thread.Sleep(50);
            if (!keep && Directory.Exists(toEdit)) Directory.Delete(toEdit, true);
            ResetStatus();
        }
    }

    // ExeFS Subform Items
    private void RebuildExeFS(object sender, EventArgs e)
    {
        if (ExeFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild ExeFS?") != DialogResult.Yes)
            return;

        string[] files = Directory.GetFiles(ExeFSPath);
        int file = 0;
        if (files[1].Contains("code"))
            file = 1;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".exefs" : "exefs.bin",
            Filter = "HANS ExeFS|*.exefs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            RunWorker("Rebuilding the ExeFS", () =>
            {
                new BLZCoder(["-en", files[file]], pBar1);
                WinFormsUtil.Alert("Compressed!");
                ExeFS.PackExeFS(ExeFS.GetExeFSFiles(ExeFSPath), sfd.FileName);
                HandleFriendship(10);
            });
        }
    }

    private void B_Pickup_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new PickupEditor6().Show();
                break;
            case 7:
                var pickup = Config.GetlzGARCData("pickup");
                Invoke(() => { var ed = new PickupEditor7(pickup); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); });
                break;
        }
    }

    private void B_TMHM_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath == null)
            return;
        switch (Config.Generation)
        {
            case 6: new TMHMEditor6().Show(); break;
            case 7: { var ed = new TMEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); } break;
        }
    }

    private void B_Mart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new MartEditor6().Show();
                break;

            case 7:
                if (ThreadActive())
                    return;
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) { var ed = Config.USUM ? new MartEditor7UU() : (Form)new MartEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); }
                break;
        }
    }

    private void B_MoveTutor_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new TutorEditor6().Show();
                break;
            case 7:
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) { var ed = new TutorEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); }
                break;
        }
    }

    private void B_OPower_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) new OPower().Show();
    }

    private void B_ShinyRate_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) { var ed = new ShinyRate(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); }
    }

    private void B_ResearchCenter_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (Config.Version == GameVersion.USUM)
            new ResearchCenter7().ShowDialog();
        else
            WinFormsUtil.Error("Compatibility Error", "Research Center is currently only optimized for Ultra Sun and Ultra Moon.");
    }

    // CRO Subform Items
    private void PatchCRO_CRR(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuilding CRO/CRR is not necessary if you patch the RO module.", "Continue?"))
            return;
        RunWorker("Updating the CRO and CRR hashes", () =>
        {
            CRO.E_HashCRR(Path.Combine(RomFSPath, ".crr", "static.crr"), RomFSPath, true, /* true // don't patch crr for now */ false, RTB_Status, pBar1);
            WinFormsUtil.Alert("CRO's and CRR have been updated.",
                "If you have made any modifications, it is required that the RSA Verification check be patched on the system in order for the modified CROs to load (ie, no file redirection like NTR's layeredFS).");
        });
    }

    private void B_Starter_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllPoke3Select.cro");
        string CRO2 = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllPoke3Select.cro was not found in your RomFS folder!");
            return;
        }
        if (!File.Exists(CRO2))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new StarterEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    private void B_TypeChart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        switch (Config.Generation)
        {
            case 6:
                {
                    if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                        return;
                    string CRO = Path.Combine(RomFSPath, "DllBattle.cro");
                    if (!File.Exists(CRO))
                    {
                        WinFormsUtil.Error("File Missing!", "DllBattle.cro was not found in your RomFS folder!");
                        return;
                    }
                    var ed6 = new TypeChart6(); 
                    WinFormsUtil.ApplyTheme(ed6); 
                    ed6.ShowDialog();
                }
                break;
            case 7:
                {
                    var ed7 = new TypeChart7(); 
                    WinFormsUtil.ApplyTheme(ed7); 
                    ed7.ShowDialog();
                }
                break;
        }
    }

    private void B_Gift_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new GiftEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    private void B_Static_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config.Generation == 7)
        {
            RunEditor("Static Encounter", () =>
            {
                var esg = Config.GetGARCData("encounterstatic");
                byte[][] es = esg.Files;

                Invoke(() => { var ed = new StaticEncounterEditor7(es); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); });
                esg.Files = es;
                esg.Save();
            });
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new StaticEncounterEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }  

    private void B_CROExpander_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        var ed = new CROExpander(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    // Trimmed 3DS Building
    private void B_RebuildTrimmed3DS_Click(object sender, EventArgs e)
    {
        Rebuild3DSExternal(trimmed: true);
    }

    // 3DS Building
    private void B_Rebuild3DS_Click(object sender, EventArgs e)
    {
        Rebuild3DSExternal(trimmed: false);
    }

    private void Rebuild3DSExternal(bool trimmed)
    {
        if (ThreadActive())
            return;

        string gameParentDir = !string.IsNullOrEmpty(RomFSPath) 
            ? Path.GetDirectoryName(RomFSPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) 
            : null;

        var sfd = new SaveFileDialog
        {
            InitialDirectory = (!string.IsNullOrEmpty(gameParentDir) && Directory.Exists(gameParentDir)) ? gameParentDir : null,
            FileName = trimmed ? "newROM_trimmed.3ds" : "newROM.3ds",
            Filter = "3DS ROM (*.3ds)|*.3ds|CCI File (*.cci)|*.cci|All Files (*.*)|*.*",
        };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        string path = sfd.FileName;

        UpdateStatus(Environment.NewLine + $"Launching external {(trimmed ? "Trimmed " : "")}3DS Toolkit rebuilder in CMD window...");

        string script = ExternalRebuilder.LaunchExternalBatchRebuild(
            RomFSPath, ExeFSPath, ExHeaderPath, path, out string sentinelDone, out string sentinelError, msg => UpdateStatus(msg), trim: trimmed);

        if (script != null && sentinelDone != null)
        {
            StartRebuildMonitoring(sentinelDone, sentinelError, path, trimmed ? "Trimmed 3DS ROM" : "3DS ROM");
        }
        else
        {
            WinFormsUtil.Error("Failed to launch external 3DS Toolkit rebuild script.");
        }
    }

    private void StartRebuildMonitoring(string doneFlag, string errorFlag, string targetPath, string labelText)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
        {
            if (File.Exists(doneFlag))
            {
                timer.Stop();
                timer.Dispose();
                HandleFriendship(10);

                var romfsFix = NcchRomFsHash.Fix(targetPath);
                UpdateStatus((romfsFix.Changed ? "Repaired RomFS hash: " : "RomFS hash: ") + romfsFix.Message + Environment.NewLine);

                UpdateStatus($"{labelText} rebuild complete." + Environment.NewLine);
                WinFormsUtil.Alert($"Wrote {labelText}:", targetPath);
            }
            else if (File.Exists(errorFlag))
            {
                timer.Stop();
                timer.Dispose();
                UpdateStatus($"{labelText} rebuild failed." + Environment.NewLine);
                WinFormsUtil.Error($"Failed to rebuild {labelText}. Check command window for details.");
            }
        };
        timer.Start();
    }


    /// <summary>Whether two files hold exactly the same bytes.</summary>
    private static bool SameFile(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;

            using var sa = File.OpenRead(a);
            using var sb = File.OpenRead(b);
            var ba = new byte[64 * 1024];
            var bb = new byte[64 * 1024];
            while (true)
            {
                int na = sa.Read(ba, 0, ba.Length);
                int nb = sb.Read(bb, 0, bb.Length);
                if (na != nb) return false;
                if (na == 0) return true;
                if (!ba.AsSpan(0, na).SequenceEqual(bb.AsSpan(0, nb))) return false;
            }
        }
        catch { return false; }
    }

    // Extra Tools
    private void L_SubTools_Click(object sender, EventArgs e)
    {
        new ToolsUI().ShowDialog();
    }

    private void B_Patch_Click(object sender, EventArgs e)
    {
        new Patch().ShowDialog();
    }

    private void Menu_BLZ_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        void RunCoder(string mode, string doneMessage)
        {
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                try
                {
                    new BLZCoder([mode, path], pBar1);
                    WinFormsUtil.Alert(doneMessage);
                }
                catch (Exception ex)
                {
                    WinFormsUtil.Error($"Could not {(mode == "-en" ? "compress" : "decompress")} the file.", ex.Message);
                }
                finally { Interlocked.Decrement(ref threads); }
            }).Start();
        }

        if (ModifierKeys != Keys.Control && fi.Length % 0x200 == 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed Binary.", "Compress? File will be replaced.") == DialogResult.Yes)
            RunCoder("-en", "Compressed!");
        else if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed Binary", "Decompress? File will be replaced.") == DialogResult.Yes)
            RunCoder("-d", "Decompressed!");
    }

    private void Menu_LZ11_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        byte[] data = File.ReadAllBytes(path);
        string predict = data[0] == 0x11 ? "compressed" : "decompressed";
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, $"Detected {predict} file. Do what?",
            "Yes = Decompress\nNo = Compress\nCancel = Abort");
        RunWorker("LZ11 compression", () =>
        {
            if (dr == DialogResult.Yes)
            {
                try
                {
                    LZSS.Decompress(path, Path.Combine(Directory.GetParent(path).FullName, "dec_" + Path.GetFileNameWithoutExtension(path) + ".bin"));
                }
                catch (Exception err) { WinFormsUtil.Alert("Tried decompression, may have worked:", err.ToString()); }
                WinFormsUtil.Alert("File Decompressed!", path);
            }
            if (dr == DialogResult.No)
            {
                LZSS.Compress(path, Path.Combine(Directory.GetParent(path).FullName, Path.GetFileNameWithoutExtension(path).Replace("_dec", "") + ".lz"));
                WinFormsUtil.Alert("File Compressed!", path);
            }
        });
    }

    private void Menu_SMDH_Click(object sender, EventArgs e)
    {
        new Icon().ShowDialog();
    }

    private void Menu_Shuffler_Click(object sender, EventArgs e)
    {
        new Shuffler().ShowDialog();
    }

    // GARC Requests
    internal static string GetGARCFileName(string requestedGARC, int lang)
    {
        var garc = Config.GetGARCReference(requestedGARC);
        if (garc.LanguageVariant)
            garc = garc.GetRelativeGARC(lang);

        return garc.Reference;
    }

    private bool GetGARC(string infile, string outfolder, bool PB, bool bypassExt = false)
    {
        try
        {
            if (skipBoth && Directory.Exists(outfolder))
            {
                UpdateStatus("Skipped - Exists!", false);
                return true;
            }
            bool success = GarcUtil.UnpackGARC(infile, outfolder, bypassExt, PB ? pBar1 : null, L_Status, true);
            UpdateStatus(success ? "Success!" : "Failed!", false);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not get the GARC:", e.Message); return false; }
        finally { Interlocked.Decrement(ref threads); }
    }

    private bool SetGARC(string outfile, string infolder, int padBytes, bool PB)
    {
        try
        {
            if (skipBoth || (ModifierKeys == Keys.Control && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Cancel writing data back to GARC?") == DialogResult.Yes))
            {
                UpdateStatus("Aborted!", false);
                return false;
            }

            bool success = GarcUtil.PackGARC(infolder, outfile, Config.GARCVersion, padBytes, PB ? pBar1 : null, L_Status, true);
            UpdateStatus(success ? "Success!" : "Failed!", false);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not set the GARC back:", e.Message); return false; }
        finally { Interlocked.Decrement(ref threads); }
    }

    private void ThreadGet(string infile, string outfolder, bool PB = true, bool bypassExt = false)
    {
        Interlocked.Increment(ref threads);
        if (Directory.Exists(outfolder))
        {
            try { Directory.Delete(outfolder, true); }
            catch { }
        }

        new Thread(() => GetGARC(infile, outfolder, PB, bypassExt)).Start();
    }

    private void ThreadSet(string outfile, string infolder, int padBytes, bool PB = true)
    {
        Interlocked.Increment(ref threads);
        new Thread(() => SetGARC(outfile, infolder, padBytes, PB)).Start();
    }

    // Update RichTextBox
    private void UpdateStatus(string status, bool preBreak = true)
    {
        string newtext = (preBreak ? Environment.NewLine : "") + status;
        try
        {
            if (RTB_Status.InvokeRequired)
            {
                RTB_Status.Invoke((MethodInvoker)delegate
                {
                    RTB_Status.AppendText(newtext);
                    RTB_Status.SelectionStart = RTB_Status.Text.Length;
                    RTB_Status.ScrollToCaret();
                    L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
                });
            }
            else
            {
                RTB_Status.AppendText(newtext);
                RTB_Status.SelectionStart = RTB_Status.Text.Length;
                RTB_Status.ScrollToCaret();
                L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
            }
        }
        catch { }
    }

    private void ResetStatus()
    {
        try
        {
            if (L_Status.InvokeRequired)
            {
                L_Status.Invoke((MethodInvoker)(() => L_Status.Text = ""));
            }
            else
            {
                L_Status.Text = "";
            }
        }
        catch { }
    }

    private void SetInt32SeedToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Reseed RNG?", "If yes, copy the 32 bit (not hex) integer seed to the clipboard before hitting Yes."))
            return;

        string val = string.Empty;
        try { val = Clipboard.GetText(); }
        catch { }
        if (int.TryParse(val, out int seed))
        {
            Util.ReseedRand(seed);
            WinFormsUtil.Alert($"Reseeded RNG to seed: {seed}");
            return;
        }
        WinFormsUtil.Alert("Unable to set seed.");
    }
}
