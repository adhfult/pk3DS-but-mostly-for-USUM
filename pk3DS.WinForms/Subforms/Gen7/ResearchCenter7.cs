using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Modding.Research;

namespace pk3DS.WinForms;

/// <summary>
/// Battle engine research and editing, built on the resolved-mechanic stack rather than on raw
/// offsets.
/// <para>
/// The previous Research Center, the Mechanic Editor and the code.bin editor all asked the user to
/// supply addresses and trusted whatever came back. That does not survive contact with a modified
/// ROM: the tables move between builds, corpus routines are recorded at addresses that are occupied
/// here, and a relocation table can quietly grow over the segment behind it. Everything in this form
/// therefore goes through <see cref="BattleMechanicMap"/> (tables located by id fingerprint) and
/// ends with <see cref="CroVerifier"/>, and every write is preceded by a dry run and a backup.
/// </para>
/// </summary>
public sealed class ResearchCenter7 : Form
{
    // Loaded state
    private readonly ComboBox _version = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private ResearchDatabase _db;
    private string _dbVersion;
    private BattleMechanicMap _map;
    private byte[] _rom;
    private string _romPath;
    private (uint Offset, int Length) _reserve;
    private uint _bump;
    private Dictionary<uint, ResearchFunction> _symbols = [];

    // Chrome
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel _status = new("No binary loaded.");
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };

    // Overview
    private readonly TextBox _overview = Mono(readOnly: true);

    // Mechanics browser
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly TextBox _filter = new() { Width = 190 };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        HideSelection = false, MultiSelect = false, GridLines = false,
    };
    private readonly TextBox _detail = Mono(readOnly: true);

    // Install
    private readonly ComboBox _newKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly TextBox _newId = new() { Width = 90 };
    private readonly TextBox _newName = new() { Width = 190 };
    private readonly TextBox _newTiming = new() { Width = 70, Text = "0x00" };
    private readonly ComboBox _newSource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly TextBox _newBody = Mono();
    private readonly TextBox _installLog = Mono(readOnly: true);

    // Hook
    private readonly TextBox _hookSite = new() { Width = 110 };
    private readonly TextBox _hookAsm = Mono();
    private readonly TextBox _nopSite = new() { Width = 110 };
    private readonly NumericUpDown _nopCount = new() { Minimum = 1, Maximum = 64, Value = 1, Width = 60 };
    private readonly TextBox _hookLog = Mono(readOnly: true);


    public ResearchCenter7()
    {
        Text = "Research Center";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 640);
        Size = new Size(1180, 780);

        BuildChrome();

        _tabs.TabPages.Add(Group("Add to ROM",
            "Finished things you can install. Pick one, choose its id, dry run, add.",
            BuildRecipesTab()));

        _tabs.TabPages.Add(Group("Create",
            "Build something new. Not sure which page you want? Start on \"Where do I start?\".",
            BuildStartHereTab(), BuildCustomFunctionTab(), BuildInstallTab(), BuildHookTab()));

        _tabs.TabPages.Add(Group("Research",
            "What this ROM contains and what the notes record. Nothing here writes to the ROM.",
            BuildOverviewTab(), BuildMechanicsTab(), BuildNotesTab(), BuildPortTab()));

        Shown += (_, _) => Analyze();
    }

    /// <summary>
    /// Wraps related pages in one top-level tab, with a line saying what the group is for.
    /// </summary>
    /// <summary>Inner tab strips by group title, so a page can send you to a sibling.</summary>
    private static readonly Dictionary<string, TabControl> _groupTabs = [];

    private static TabPage Group(string title, string blurb, params TabPage[] pages)
    {
        var page = new TabPage(title);

        Control body;
        if (pages.Length == 1)
        {
            body = new Panel { Dock = DockStyle.Fill };
            foreach (Control c in pages[0].Controls.Cast<Control>().ToList())
            {
                pages[0].Controls.Remove(c);
                body.Controls.Add(c);
            }
        }
        else
        {
            var inner = new TabControl { Dock = DockStyle.Fill };
            foreach (var p in pages) inner.TabPages.Add(p);
            body = inner;
            _groupTabs[title] = inner;
        }

        page.Controls.Add(body);
        page.Controls.Add(new Label
        {
            Text = blurb,
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 26,
            ForeColor = Color.Gray,
            Padding = new Padding(6, 6, 0, 0),
        });
        return page;
    }

    private static TextBox Mono(bool readOnly = false) => new()
    {
        Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both,
        WordWrap = false, ReadOnly = readOnly, Font = new Font(FontFamily.GenericMonospace, 8.5f),
    };

    #region Chrome

    private void BuildChrome()
    {
        _target.Items.AddRange(["Battle.cro", "Bag.cro", "Shop.cro", "Box.cro", "Status.cro", "FieldRo.cro", "Evolution.cro", "code.bin"]);
        _target.SelectedIndex = 0;
        // Selecting a recipe moves this to that recipe's file; that is a display change, not a
        // request to re-read the ROM, so it does not trigger a re-analyze.
        _target.SelectedIndexChanged += (_, _) => { if (!_suppressAnalyze) Analyze(); };

        var reload = new Button { Text = "Re-analyze", Width = 100 };
        reload.Click += (_, _) => Analyze();

        var verify = new Button { Text = "Verify", Width = 80 };
        verify.Click += (_, _) => { RunVerify(); _tabs.SelectedIndex = 0; };

        _version.Items.AddRange(["Auto", .. ResearchVersion.Known]);
        _version.SelectedIndex = 0;
        _version.SelectedIndexChanged += (_, _) =>
        {
            ResearchVersion.Override = _version.SelectedIndex <= 0 ? null : _version.SelectedItem as string;
            _db = null;                    // different column, different symbols
            _dbVersion = null;
            Analyze();
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 5, 0, 0) };
        top.Controls.AddRange([new Label { Text = "Binary:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) },
                               _target,
                               new Label { Text = "Game:", AutoSize = true, Padding = new Padding(8, 4, 0, 0) },
                               _version, reload, verify]);

        var credit = new ToolStripStatusLabel("Battle research by ABZB")
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.Gray,
        };

        var strip = new StatusStrip();
        strip.Items.Add(_status);
        strip.Items.Add(credit);

        Controls.Add(_tabs);
        Controls.Add(top);
        Controls.Add(strip);
    }

    #endregion

    #region Tabs

    private TabPage BuildOverviewTab()
    {
        var page = new TabPage("Overview");
        page.Controls.Add(_overview);
        return page;
    }

    private TabPage BuildMechanicsTab()
    {
        var page = new TabPage("Mechanics");

        // Beyond the three effect tables: the timings they fire at, the master tables they live in,
        // and the engine routines they call. Columns are set per view by SetColumns.
        _kind.Items.AddRange(["Move", "Ability", "Item", "Timings", "Master tables", "Engine routines"]);
        _kind.Width = 130;
        _kind.SelectedIndex = 0;
        _kind.SelectedIndexChanged += (_, _) => RefreshList();
        _filter.TextChanged += (_, _) => RefreshList();

        _list.SelectedIndexChanged += (_, _) => ShowDetail();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30 };
        bar.Controls.AddRange([new Label { Text = "Show:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _kind,
                               new Label { Text = "Filter:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _filter,
                               _mechSummary]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 620 };
        split.Panel1.Controls.Add(_list);
        split.Panel2.Controls.Add(_detail);

        page.Controls.Add(split);
        page.Controls.Add(bar);
        return page;
    }

    private TabPage BuildInstallTab()
    {
        var page = new TabPage("Add mechanic");

        _newKind.Items.AddRange(["Move", "Ability", "Item"]);
        _newKind.SelectedIndex = 0;
        _newKind.SelectedIndexChanged += (_, _) => DescribeId();
        _newId.TextChanged += (_, _) => DescribeId();

        _newSource.Items.AddRange(["ARM assembly", "Hex bytes", "Reuse an existing routine (address)"]);
        _newSource.SelectedIndex = 0;

        var idNote = new Label { AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(4, 6, 0, 0), Name = "idNote" };

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, ColumnCount = 6, AutoSize = false };
        grid.Controls.Add(new Label { Text = "Kind", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, 0);
        grid.Controls.Add(_newKind, 1, 0);
        grid.Controls.Add(new Label { Text = "Game id", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, 2, 0);
        grid.Controls.Add(_newId, 3, 0);
        grid.Controls.Add(idNote, 4, 0);
        grid.Controls.Add(new Label { Text = "Label", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, 1);
        grid.Controls.Add(_newName, 1, 1);
        grid.Controls.Add(new Label { Text = "Timing", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, 2, 1);
        grid.Controls.Add(_newTiming, 3, 1);
        grid.Controls.Add(_newSource, 4, 1);

        var plan = new Button { Text = "Dry run", Width = 100 };
        plan.Click += (_, _) => DoInstall(commit: false);
        var install = new Button { Text = "Install", Width = 100 };
        install.Click += (_, _) => DoInstall(commit: true);
        var timings = new Button { Text = "Timing usage…", Width = 120 };
        timings.Click += (_, _) => ShowTimingUsage();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32 };
        buttons.Controls.AddRange([plan, install, timings]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_newBody);
        split.Panel2.Controls.Add(_installLog);

        page.Controls.Add(split);
        page.Controls.Add(buttons);
        page.Controls.Add(grid);
        return page;
    }

    private readonly ComboBox _fnKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ComboBox _fnTarget = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly TextBox _fnName = new() { Width = 170 };
    private readonly TextBox _fnMechanic = new() { Width = 170 };
    private readonly TextBox _fnTiming = new() { Width = 70, Text = "0x00" };
    private readonly TextBox _fnBody = Mono();
    private readonly TextBox _fnLog = Mono(readOnly: true);
    private CustomFunctionDefinition _fnCurrent;

    /// <summary>Binaries a custom function can install into, with the file each one is.</summary>
    private static readonly (ResearchTarget Target, string Label, string File)[] FunctionTargets =
    [
        (ResearchTarget.BattleCro,  "Battle.cro",   "Battle.cro"),
        (ResearchTarget.CodeBin,    "code.bin",     "code.bin"),
        (ResearchTarget.BagCro,     "Bag.cro",      "Bag.cro"),
        (ResearchTarget.ShopCro,    "Shop.cro",     "Shop.cro"),
        (ResearchTarget.BoxCro,     "Box.cro",      "Box.cro"),
        (ResearchTarget.StatusCro,  "Status.cro",   "Status.cro"),
        (ResearchTarget.FieldRoCro, "DllField.cro", "DllField.cro"),
        (ResearchTarget.EvolutionCro, "Evolution.cro", "Evolution.cro"),
    ];

    /// <summary>
    /// A front door for the Create group: what each page is for, and a button that opens it.
    /// </summary>
    private TabPage BuildStartHereTab()
    {
        var page = new TabPage("Where do I start?");

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(16, 12, 12, 12),
        };

        void Option(string question, string detail, string tabName)
        {
            flow.Controls.Add(new Label
            {
                Text = question, AutoSize = true, Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 12, 0, 2),
            });
            flow.Controls.Add(new Label
            {
                Text = detail, AutoSize = false, Width = 640, Height = 34,
                ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 4),
            });

            var go = new Button { Text = "Open \"" + tabName + "\"", Width = 200, Margin = new Padding(0, 0, 0, 8) };
            go.Click += (_, _) =>
            {
                if (!_groupTabs.TryGetValue("Create", out var strip)) return;
                foreach (TabPage p in strip.TabPages)
                {
                    if (p.Text != tabName) continue;
                    strip.SelectedTab = p;
                    return;
                }
            };
            flow.Controls.Add(go);
        }

        flow.Controls.Add(new Label
        {
            Text = "Before building anything, check the \"Add to ROM\" tab — if what you want is already\n"
                 + "there as a recipe, installing it is one click and it has been verified against this build.",
            AutoSize = true, Margin = new Padding(0, 0, 0, 10),
        });

        Option("I want an existing move, ability or item to behave differently.",
               "Start from a template, point it at the move/ability/item, and install. This is the usual case\n"
             + "and the only one that does not need an address.",
               "Custom function");

        Option("I want a move, ability or item the game does not have at all.",
               "Claims a free id, writes its name and description, and attaches behaviour to it.",
               "Add mechanic");

        Option("I already have code, and need the game to call it.",
               "Writes a branch at an address you supply. Only needed when a function is not reached by\n"
             + "attaching it to a mechanic — read the Research tab first to find the call site.",
               "Code hook");

        page.Controls.Add(flow);
        return page;
    }

    private TabPage BuildCustomFunctionTab()
    {
        var page = new TabPage("Custom function");

        _fnKind.Items.AddRange(["Move", "Ability", "Item"]);
        _fnKind.SelectedIndex = 1;

        _fnTarget.Items.AddRange([.. FunctionTargets.Select(t => t.Label)]);
        _fnTarget.SelectedIndex = 0;

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, ColumnCount = 6 };
        grid.Controls.Add(new Label { Text = "Name", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, 0);
        grid.Controls.Add(_fnName, 1, 0);
        grid.Controls.Add(new Label { Text = "Attaches to", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, 2, 0);
        grid.Controls.Add(_fnKind, 3, 0);
        grid.Controls.Add(new Label { Text = "Target", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, 4, 0);
        grid.Controls.Add(_fnTarget, 5, 0);
        grid.Controls.Add(new Label { Text = "Move/Ability/Item", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, 1);
        grid.Controls.Add(_fnMechanic, 1, 1);
        grid.Controls.Add(new Label { Text = "Timing", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, 2, 1);
        grid.Controls.Add(_fnTiming, 3, 1);

        var bNew = new Button { Text = "New", Width = 70 };
        var bTemplate = new Button { Text = "From template…", Width = 120 };
        var bOpen = new Button { Text = "Open…", Width = 80 };
        var bSave = new Button { Text = "Save…", Width = 80 };
        var bPlan = new Button { Text = "Dry run", Width = 100 };
        var bInstall = new Button { Text = "Install", Width = 100 };

        bNew.Click += (_, _) => { _fnCurrent = null; _fnName.Text = _fnMechanic.Text = ""; _fnBody.Text = ""; _fnLog.Text = "New definition."; };
        bTemplate.Click += (_, _) => NewFromTemplate();
        bOpen.Click += (_, _) => OpenCustomFunction();
        bSave.Click += (_, _) => SaveCustomFunction();
        bPlan.Click += (_, _) => RunCustomFunction(commit: false);
        bInstall.Click += (_, _) => RunCustomFunction(commit: true);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32 };
        buttons.Controls.AddRange([bNew, bTemplate, bOpen, bSave, bPlan, bInstall]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_fnBody);
        split.Panel2.Controls.Add(_fnLog);

        page.Controls.Add(split);
        page.Controls.Add(buttons);
        page.Controls.Add(grid);
        return page;
    }

    /// <summary>Reads the form into the portable definition the installer consumes.</summary>
    private CustomFunctionDefinition GatherCustomFunction()
    {
        var def = _fnCurrent ?? new CustomFunctionDefinition();
        def.Name = _fnName.Text.Trim();
        def.Mechanic = (CustomMechanicKind)_fnKind.SelectedIndex;
        int ti = _fnTarget.SelectedIndex;
        def.Target = ti >= 0 && ti < FunctionTargets.Length ? FunctionTargets[ti].Target : ResearchTarget.BattleCro;

        // Either an index or a name; -1 tells the installer to resolve the name against the
        // game's own text tables at install time rather than baking an index in now.
        string mech = _fnMechanic.Text.Trim();
        if (int.TryParse(mech, out int idx)) { def.MechanicIndex = idx; def.MechanicName = null; }
        else { def.MechanicIndex = -1; def.MechanicName = mech; }

        def.Timing = TryParseNumber(_fnTiming.Text, out uint t) ? (byte)t : (byte)0;
        def.Assembly = [.. _fnBody.Text.Replace("\r\n", "\n").Split('\n')];
        def.HexCode = null;
        return def;
    }

    /// <summary>
    /// Starts a definition from a template rather than an empty pane.
    /// <para>
    /// The template supplies the structure, the register conventions and the corpus sheet that
    /// documents the real thing. The addresses it needs arrive as "TODO" lines, because those are
    /// build-specific and a plausible-looking guess would assemble cleanly and corrupt the module.
    /// </para>
    /// </summary>
    private void NewFromTemplate()
    {
        // Hand the picker this ROM's symbols so its preview shows the addresses that will actually
        // be assembled, rather than the {sym:...} names.
        var table = _db == null || _romPath == null
            ? null
            : new ArmSymbolTable(_db, TargetOf(_romPath));

        using var picker = new FunctionTemplatePicker(table, _map);
        WinFormsUtil.ApplyTheme(picker);
        if (picker.ShowDialog() != DialogResult.OK || picker.Result == null) return;

        _fnCurrent = picker.Result;
        _fnName.Text = _fnCurrent.Name;
        _fnKind.SelectedIndex = (int)_fnCurrent.Mechanic;

        int ti = Array.FindIndex(FunctionTargets, t => t.Target == _fnCurrent.Target);
        _fnTarget.SelectedIndex = ti >= 0 ? ti : 0;

        _fnMechanic.Text = "";
        _fnTiming.Text = $"0x{_fnCurrent.Timing:X2}";
        _fnBody.Text = string.Join(Environment.NewLine, _fnCurrent.Assembly ?? []);

        var lines = _fnCurrent.Assembly ?? [];
        int todo = lines.Count(l => l.Contains("TODO", StringComparison.Ordinal));
        int check = lines.Count(l => l.Contains("CHECK:", StringComparison.Ordinal));

        // A CHECK is a note about this ROM, not a gap. TODO is still counted because a hand-edited
        // or imported definition can carry one, but no built-in template produces any.
        _fnLog.Text = $"Started from a template.{Environment.NewLine}"
                    + (todo == 0
                        ? "The body is complete - addresses resolve when you install."
                        : $"{todo} line(s) marked TODO are code you still need to write.")
                    + Environment.NewLine
                    + (check == 0
                        ? ""
                        : $"{check} note(s) marked CHECK are things worth knowing about this shape." + Environment.NewLine)
                    + "Name the move/ability/item it attaches to, set the timing, then Dry run.";
    }

    private void OpenCustomFunction()
    {
        using var ofd = new OpenFileDialog { Filter = "Custom function|*.json" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        try
        {
            _fnCurrent = CustomFunctionDefinition.FromJson(File.ReadAllText(ofd.FileName));
            _fnName.Text = _fnCurrent.Name;
            _fnKind.SelectedIndex = (int)_fnCurrent.Mechanic;
            int loaded = Array.FindIndex(FunctionTargets, t => t.Target == _fnCurrent.Target);
            _fnTarget.SelectedIndex = loaded >= 0 ? loaded : 0;
            _fnMechanic.Text = _fnCurrent.MechanicIndex >= 0 ? _fnCurrent.MechanicIndex.ToString() : _fnCurrent.MechanicName ?? "";
            _fnTiming.Text = $"0x{_fnCurrent.Timing:X2}";
            _fnBody.Text = string.Join(Environment.NewLine, _fnCurrent.Assembly ?? []);
            _fnLog.Text = $"Loaded {Path.GetFileName(ofd.FileName)}.";
        }
        catch (Exception ex) { _fnLog.Text = "Could not read that definition: " + ex.Message; }
    }

    private void SaveCustomFunction()
    {
        var def = GatherCustomFunction();
        if (string.IsNullOrWhiteSpace(def.Name)) { _fnLog.Text = "Give the function a name first."; return; }

        using var sfd = new SaveFileDialog { Filter = "Custom function|*.json", FileName = def.Name + ".json" };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        try
        {
            File.WriteAllText(sfd.FileName, def.ToJson());
            _fnCurrent = def;
            _fnLog.Text = $"Saved to {sfd.FileName}.";
        }
        catch (Exception ex) { _fnLog.Text = "Could not save: " + ex.Message; }
    }

    /// <summary>
    /// Plans the install and, when asked, commits it.
    /// <para>
    /// The dry run is not decoration: it resolves the mechanic, assembles the code and allocates
    /// space without touching the file, so a definition that cannot work says so before anything
    /// is written.
    /// </para>
    /// </summary>
    private void RunCustomFunction(bool commit)
    {
        if (_map?.Cro == null) { _fnLog.Text = "Load a CRO on the Overview tab first."; return; }

        var def = GatherCustomFunction();
        if (string.IsNullOrWhiteSpace(def.Name)) { _fnLog.Text = "Give the function a name first."; return; }

        var wanted = Array.Find(FunctionTargets, t => t.Target == def.Target);
        string loadedFile = Path.GetFileName(_map.Cro.SourcePath ?? "");
        if (wanted.File != null && loadedFile.Length > 0
            && !string.Equals(loadedFile, wanted.File, StringComparison.OrdinalIgnoreCase))
        {
            _fnLog.Text = $"This definition targets {wanted.File}, but {loadedFile} is loaded on the "
                        + $"Overview tab.{Environment.NewLine}Load {wanted.File} there first.";
            return;
        }

        // Surface an assembly error here rather than as a failed plan step.
        var code = def.ResolveCode(out string codeError);
        if (code == null) { _fnLog.Text = codeError; return; }

        string[] names = def.Mechanic switch
        {
            CustomMechanicKind.Move => Main.Config?.GetText(TextName.MoveNames),
            CustomMechanicKind.Item => Main.Config?.GetText(TextName.ItemNames),
            _ => Main.Config?.GetText(TextName.AbilityNames),
        };

        InstallPlan plan;
        try { plan = CustomFunctionInstaller.Plan(def, _map.Cro, _db, names, _map); }
        catch (Exception ex) { _fnLog.Text = "Planning failed: " + ex.Message; return; }

        var sb = new StringBuilder();
        sb.AppendLine($"{code.Length} bytes of code assembled.");
        sb.AppendLine(plan.Describe());

        if (!commit)
        {
            sb.AppendLine();
            sb.AppendLine(plan.HasErrors ? "Dry run found errors - nothing would be installed." : "Dry run clean.");
            _fnLog.Text = sb.ToString();
            return;
        }

        if (plan.HasErrors)
        {
            sb.AppendLine();
            sb.AppendLine("Refusing to install: the plan has errors.");
            _fnLog.Text = sb.ToString();
            return;
        }

        bool ok;
        try { ok = CustomFunctionInstaller.Commit(plan, _map.Cro, m => sb.AppendLine(m)); }
        catch (Exception ex) { sb.AppendLine("Install failed: " + ex.Message); _fnLog.Text = sb.ToString(); return; }

        sb.AppendLine(ok
            ? "Installed into the in-memory CRO. Use the Overview tab's save to write it out - "
              + "that write goes through the approval prompt."
            : "Install did not complete.");
        _fnLog.Text = sb.ToString();
    }

    private TabPage BuildHookTab()
    {
        var page = new TabPage("Code hook");

        var hookBtn = new Button { Text = "Dry run hook", Width = 110 };
        hookBtn.Click += (_, _) => DoHook(commit: false);
        var hookGo = new Button { Text = "Install hook", Width = 110 };
        hookGo.Click += (_, _) => DoHook(commit: true);
        var nopBtn = new Button { Text = "NOP range", Width = 100 };
        nopBtn.Click += (_, _) => DoNop();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
        bar.Controls.AddRange([new Label { Text = "Hook at", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _hookSite,
                               hookBtn, hookGo,
                               new Label { Text = "   NOP at", AutoSize = true, Padding = new Padding(8, 7, 0, 0) }, _nopSite,
                               new Label { Text = "x", AutoSize = true, Padding = new Padding(2, 7, 0, 0) }, _nopCount, nopBtn]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_hookAsm);
        split.Panel2.Controls.Add(_hookLog);

        page.Controls.Add(split);
        page.Controls.Add(bar);
        return page;
    }

    // Port tab -------------------------------------------------------------------------------
    private readonly TextBox _portPath = new() { Width = 420 };
    private readonly TextBox _portLog = Mono(readOnly: true);

    /// <summary>
    /// Re-applies a saved manifest to whatever binary is currently loaded.
    /// <para>
    /// Deliberately two-stage. "Preview" runs the whole install in memory and prints exactly what it
    /// would do; only "Apply" writes, and it writes a backup first and re-reads the file afterwards.
    /// Every failure this kit guards against was a case where the in-memory result looked correct
    /// and the file on disk did not match it.
    /// </para>
    /// </summary>
    private TabPage BuildPortTab()
    {
        var page = new TabPage("Port");

        var browse = new Button { Text = "Folder...", Width = 90 };
        var preview = new Button { Text = "Preview", Width = 100 };
        var apply = new Button { Text = "Apply all", Width = 100 };

        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Folder holding the port .json files" };
            if (dlg.ShowDialog() == DialogResult.OK) _portPath.Text = dlg.SelectedPath;
        };
        preview.Click += (_, _) => RunPort(commit: false);
        apply.Click += (_, _) => RunPort(commit: true);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4) };
        bar.Controls.AddRange([new Label { Text = "Port folder:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) },
                               _portPath, browse, preview, apply]);

        page.Controls.Add(_portLog);
        page.Controls.Add(bar);
        return page;
    }

    /// <summary>
    /// Applies every recognised .json in the chosen folder — Battle.cro, code.bin and game data —
    /// in one pass, stopping at the first stage that fails.
    /// </summary>
    private void RunPort(bool commit)
    {
        if (Main.Config == null) { _portLog.Text = "Load a ROM in the main window first."; return; }
        if (!Directory.Exists(_portPath.Text)) { _portLog.Text = "Choose the folder holding the port .json files."; return; }

        try
        {
            var bundle = PortBundle.Load(_portPath.Text);
            if (bundle.Manifest == null && bundle.Data == null && bundle.CodeBin == null)
            {
                _portLog.Text = string.Join(Environment.NewLine, bundle.Notes);
                return;
            }

            // Ids are checked against this build's own name lists, so a shifted id cannot quietly
            // attach an effect to an unrelated move or ability.
            var names = new Dictionary<CustomMechanicKind, string[]>
            {
                [CustomMechanicKind.Move] = Main.Config.GetText(TextName.MoveNames),
                [CustomMechanicKind.Ability] = Main.Config.GetText(TextName.AbilityNames),
                [CustomMechanicKind.Item] = Main.Config.GetText(TextName.ItemNames),
            };

            var result = bundle.ApplyAll(Main.Config, _db, commit, nameTables: names);

            var lines = new List<string> { $"folder: {_portPath.Text}", "", result.Describe() };
            if (!commit && result.Success)
            {
                lines.Add("");
                lines.Add("Preview only — press \"Apply all\" to write.");
            }
            _portLog.Text = string.Join(Environment.NewLine, lines);

            if (commit && result.Success) Analyze();   // reload so the other tabs reflect the port
        }
        catch (Exception ex)
        {
            _portLog.Text = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private readonly ComboBox _noteCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly TextBox _noteFilter = new() { Width = 200 };
    private readonly ListView _noteList = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        HideSelection = false, MultiSelect = false,
    };
    private readonly TextBox _noteDetail = Mono(readOnly: true);
    private readonly Label _noteSummary = new() { AutoSize = true, Padding = new Padding(12, 6, 0, 0), ForeColor = Color.Gray };

    private readonly ListBox _recipeList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        SelectionMode = SelectionMode.MultiExtended,
    };
    private readonly TextBox _recipeDetail = Mono(readOnly: true);
    private readonly TextBox _recipeIds = new() { Width = 220 };
    private readonly Label _recipeIdLabel = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private readonly Label _recipeIdHint = new() { AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(8, 6, 0, 0) };

    private readonly Button _bLevelCaps = new();

    /// <summary>Category filter and free-text search over the recipe list.</summary>
    private readonly ComboBox _recipeCategory = new()
    {
        Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly TextBox _recipeSearch = new() { Width = 170 };
    private readonly Label _recipeCount = new() { AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(8, 6, 0, 0) };

    /// <summary>Everything discovered, before the category/search filter narrows it.</summary>
    private List<Recipe> _allRecipes = [];

    /// <summary>Set while the Binary selector is moved programmatically.</summary>
    private bool _suppressAnalyze;

    /// <summary>Binary the selected recipe writes that the Binary selector cannot display, or null.</summary>
    private string _unselectableTarget;

    /// <summary>The level cap progression, as edited. Defaults to the researched Fly/ride flags.</summary>
    /// <summary>
    /// The level cap progression shown and edited here. Backed by <see cref="LevelCapSettings"/>
    /// rather than held locally, because that is what the installer reads — a table kept only in
    /// this form was edited, displayed, and then quietly ignored when Add to ROM assembled the
    /// defaults instead.
    /// </summary>
    private static LevelCapTable _levelCaps
    {
        get => LevelCapSettings.Table;
        set => LevelCapSettings.Table = value;
    }

    /// <summary>One labelled id box per package parameter, rebuilt when the selection changes.</summary>
    private readonly FlowLayoutPanel _recipeParams = new();
    private readonly Dictionary<string, TextBox> _recipeParamBoxes = new(StringComparer.OrdinalIgnoreCase);

    private TabPage BuildRecipesTab()
    {
        var page = new TabPage("Recipes");

        _recipeList.SelectedIndexChanged += (_, _) =>
        {
            if (_recipeList.SelectedItem is CategoryHeading)
            {
                int next = _recipeList.SelectedIndex + 1;
                _recipeList.SetSelected(_recipeList.SelectedIndex, false);
                if (next < _recipeList.Items.Count) _recipeList.SelectedIndex = next;
                return;
            }
            ShowRecipe();
        };

        var bDry = new Button { Text = "Dry run", Width = 100 };
        var bAdd = new Button { Text = "Add to ROM", Width = 110 };
        var bRemove = new Button { Text = "Remove / Revert", Width = 130 };
        bDry.Click += (_, _) => RunRecipe(commit: false);
        bAdd.Click += (_, _) => RunRecipe(commit: true);
        bRemove.Click += (_, _) => RevertRecipe();

        _recipeIdLabel.Text = "First id:";

        _bLevelCaps.Text = "Level caps…";
        _bLevelCaps.Width = 110;
        _bLevelCaps.Visible = false;
        _bLevelCaps.Click += (_, _) =>
        {
            using var dlg = new LevelCapEditor(_levelCaps);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;
            _levelCaps = dlg.Result;
            ShowRecipe();
        };

        _recipeCategory.SelectedIndexChanged += (_, _) => ApplyRecipeFilter();
        _recipeSearch.TextChanged += (_, _) => ApplyRecipeFilter();

        var filterBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30 };
        filterBar.Controls.AddRange(
        [
            new Label { Text = "Show:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _recipeCategory,
            new Label { Text = "Find:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _recipeSearch,
            _recipeCount,
        ]);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32 };
        bar.Controls.AddRange([_recipeIdLabel, _recipeIds, bDry, bAdd, bRemove, _bLevelCaps, _recipeIdHint]);

        _recipeParams.Dock = DockStyle.Top;
        _recipeParams.AutoSize = true;
        _recipeParams.WrapContents = true;
        _recipeParams.Visible = false;

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 260 };
        split.Panel1.Controls.Add(_recipeList);
        split.Panel2.Controls.Add(_recipeDetail);

        page.Controls.Add(split);
        page.Controls.Add(_recipeParams);
        page.Controls.Add(bar);
        page.Controls.Add(filterBar);

        if (_recipeList.Items.Count > 0) _recipeList.SelectedIndex = 0;
        return page;
    }

    /// <summary>
    /// Fills the list: the hand-written recipes, plus one per research sheet that records real
    /// byte writes.
    /// </summary>
    private void RefreshRecipes()
    {
        // The version decides which .ips set is offered at all: US and UM share patch names but
        // hold different offsets, so only the loaded build's folder is ever listed.
        string version = ResearchVersion.Resolve(Main.Config, Main.Config?.RomFS);
        _allRecipes = [.. Recipes.Discover(_db, version)];

        string keepCategory = _recipeCategory.SelectedItem?.ToString();
        _recipeCategory.BeginUpdate();
        _recipeCategory.Items.Clear();
        _recipeCategory.Items.Add(AllCategories);
        foreach (string c in _allRecipes.Select(r => r.Category).Distinct().OrderBy(c => c))
            _recipeCategory.Items.Add(c);
        _recipeCategory.EndUpdate();

        int ci = keepCategory != null ? _recipeCategory.Items.IndexOf(keepCategory) : -1;
        _recipeCategory.SelectedIndex = ci >= 0 ? ci : 0;

        ApplyRecipeFilter();
    }

    private const string AllCategories = "All";

    /// <summary>
    /// Narrows the list to the chosen category and search text, grouped by category within.
    /// </summary>
    private void ApplyRecipeFilter()
    {
        var keep = _recipeList.SelectedItem as Recipe;
        string cat = _recipeCategory.SelectedItem?.ToString() ?? AllCategories;
        string q = _recipeSearch.Text.Trim();

        bool Matches(Recipe r) =>
            (cat == AllCategories || r.Category == cat) &&
            (q.Length == 0 ||
             r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
             (r.Summary ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));

        var shown = _allRecipes.Where(Matches)
            .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _recipeList.BeginUpdate();
        _recipeList.Items.Clear();

        // Headings are only worth the space when more than one category is on screen.
        string last = null;
        bool heading = cat == AllCategories;
        foreach (var r in shown)
        {
            if (heading && r.Category != last)
            {
                _recipeList.Items.Add(new CategoryHeading(r.Category));
                last = r.Category;
            }
            _recipeList.Items.Add(r);
        }
        _recipeList.EndUpdate();

        _recipeCount.Text = shown.Count == _allRecipes.Count
            ? $"{shown.Count} recipes"
            : $"{shown.Count} of {_allRecipes.Count}";

        int i = keep != null ? _recipeList.Items.IndexOf(keep) : -1;
        if (i < 0) i = _recipeList.Items.Cast<object>().ToList().FindIndex(o => o is Recipe);
        if (i >= 0) _recipeList.SelectedIndex = i;
    }

    /// <summary>A non-selectable divider row naming the group that follows it.</summary>
    private sealed record CategoryHeading(string Name)
    {
        public override string ToString() => "── " + Name.ToUpperInvariant() + " ──";
    }

    private void ShowRecipe()
    {
        if (_recipeList.SelectedItem is not Recipe r) return;

        BuildRecipeParamBoxes(r);

        bool isLevelCap = r.Name.Contains("Level Cap", StringComparison.OrdinalIgnoreCase)
                       || (r.SheetFile ?? "").Contains("Level Cap", StringComparison.OrdinalIgnoreCase);
        _bLevelCaps.Visible = isLevelCap;

        string primary = r.ResolvedTargets.FirstOrDefault();
        int ti = primary == null ? -1 : _target.Items.IndexOf(primary);
        if (ti >= 0 && _target.SelectedIndex != ti)
        {
            _suppressAnalyze = true;
            try { _target.SelectedIndex = ti; }
            finally { _suppressAnalyze = false; }
        }
        _unselectableTarget = ti < 0 && !string.IsNullOrEmpty(primary) && primary != "(unknown)"
            ? primary
            : null;

        _recipeIdHint.Text = r.SlotCount switch
        {
            0 => "no id needed - this recipe only changes behaviour",
            1 => "one id",
            _ => $"{r.SlotCount} consecutive ids, starting here",
        };

        var lines = new List<string>
        {
            r.Name,
            "",
            r.Summary,
            "",
            $"  kind      {r.Kind}",
            $"  ids       {r.SlotCount}",
            $"  effect    {r.EffectKind}" +
                (r.PatchName != null ? $"  ({r.PatchName})" : "") +
                (r.TemplateName != null ? $"  ({r.TemplateName})" : ""),
            $"  changes   {string.Join(", ", r.ResolvedTargets)}",
        };

        if (_unselectableTarget != null)
        {
            lines.Add("");
            lines.Add($"  NOTE: the Binary selector cannot show {_unselectableTarget}, so it is still");
            lines.Add("        pointing at another file. That only affects the Research tabs - this");
            lines.Add("        recipe writes to the files listed above regardless of the selector.");
        }

        lines.Add("");
        lines.Add("--- what it adds ---");
        foreach (var e in r.Entries.Take(24))
            lines.Add($"  {e.Name}" + (string.IsNullOrEmpty(e.Description) ? "" : $"{Environment.NewLine}      {e.Description}"));
        if (r.Entries.Count > 24) lines.Add($"  … and {r.Entries.Count - 24} more");

        if (isLevelCap)
        {
            lines.Add("");
            lines.Add("--- level caps ---");
            var problems = _levelCaps.Validate();
            lines.Add($"  {_levelCaps.Entries.Count} step(s), {_levelCaps.ToBytes().Length} bytes " +
                      $"({LevelCapTable.EntrySize} per entry plus a terminator)");
            foreach (var e in _levelCaps.Entries.OrderBy(e => e.Cap))
                lines.Add($"    Lv {e.Cap,3}  flag 0x{e.FlagOffset:X2} bit 0x{e.FlagBit:X2}   {e.Label}");
            foreach (string p in problems) lines.Add("  ! " + p);
            lines.Add("  Use the 'Level caps…' button to change these.");
        }

        lines.Add("");
        lines.AddRange(DescribeRecipeCode(r));

        if (r.Caveats.Count > 0)
        {
            lines.Add("");
            lines.Add("--- worth knowing ---");
            foreach (string c in r.Caveats) lines.Add("  " + c);
        }

        _recipeDetail.Text = string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// The bytes a recipe writes, as offset + hex + disassembly, or a plain statement that it
    /// writes none.
    /// </summary>
    /// <summary>
    /// Applies several .ips patches as one operation — the equivalent of merging them first.
    /// </summary>
    private void RunIpsBatch(List<Recipe> patches, bool commit)
    {
        var log = new List<string> { $"{(commit ? "Add" : "Dry run")}: {patches.Count} .ips patches", "" };

        string codePath = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(Main.Config.ExeFS);
        if (!File.Exists(codePath))
        {
            _recipeDetail.Text = "ExeFS/.code.bin was not found - open the ExeFS in pk3DS first.";
            return;
        }

        string loadedVersion = ResearchVersion.Resolve(Main.Config);
        var wrongVersion = patches
            .Where(p => !string.IsNullOrEmpty(p.ForVersion) &&
                        !string.Equals(p.ForVersion, loadedVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (wrongVersion.Count > 0)
        {
            log.Add($"These are built for another game; the loaded ROM is {loadedVersion}:");
            foreach (var p in wrongVersion) log.Add($"  {p.Name} ({p.ForVersion})");
            log.Add("");
            log.Add("Refused; nothing was written.");
            _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
            return;
        }

        var parsed = new List<(string Name, List<IpsRecord> Records)>();
        foreach (var p in patches)
        {
            try { parsed.Add((p.Name, IpsPatch.Read(File.ReadAllBytes(p.IpsPath!)))); }
            catch (Exception ex) { log.Add($"  {p.Name}: unreadable - {ex.Message}"); }
        }
        if (parsed.Count != patches.Count)
        {
            log.Add("");
            log.Add("Refused; nothing was written.");
            _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
            return;
        }

        foreach (var (name, records) in parsed)
            log.Add($"  {name}: {IpsPatch.Describe(records)}");

        var clashes = IpsPatch.FindConflicts(parsed);
        log.Add("");
        if (clashes.Count > 0)
        {
            log.Add($"{clashes.Count} address range(s) written by more than one patch:");
            foreach (var c in clashes.Take(12))
                log.Add($"  0x{c.Offset:X6}  {string.Join(" + ", c.Patches)}");
            if (clashes.Count > 12) log.Add($"  … and {clashes.Count - 12} more");
            log.Add("");
            log.Add("Refused: these patches overwrite each other. Apply them separately if that is intended.");
            _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
            return;
        }
        log.Add("No overlapping writes.");

        byte[] code = File.ReadAllBytes(codePath);
        foreach (var (name, records) in parsed)
        {
            var past = records.Where(x => x.End > code.Length).ToList();
            foreach (var x in past)
                log.Add($"  {name}: writes past the end of code.bin (0x{x.End:X6}) - wrong build");
            if (past.Count > 0)
            {
                log.Add("");
                log.Add("Refused; nothing was written.");
                _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
                return;
            }
        }

        if (!commit)
        {
            log.Add($"Would write {parsed.Sum(p => p.Records.Sum(x => x.Bytes.Length))} byte(s) into code.bin.");
            _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
            return;
        }

        int total = 0;
        foreach (var (_, records) in parsed) total += IpsPatch.Apply(code, records);
        File.WriteAllBytes(codePath, code);
        log.Add($"{total} byte(s) written to code.bin from {parsed.Count} patch(es).");
        _recipeDetail.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
    }

    /// <summary>
    /// The name table a package parameter's ids live in. A package can claim a move (Scale Shot,
    /// Terrain Pulse) or an ability, not only an item, so the free-slot search has to look in the
    /// right table - offering a free ITEM id for a move parameter just moves the collision.
    /// </summary>
    private static string[] TableForParameterType(string type) => (type ?? "").ToLowerInvariant() switch
    {
        "move" => Main.Config?.GetText(TextName.MoveNames),
        "ability" => Main.Config?.GetText(TextName.AbilityNames),
        _ => Main.Config?.GetText(TextName.ItemNames),
    };

    /// <summary>Whether an id is unnamed or a placeholder in the given table.</summary>
    private static bool IsFreeId(string[] names, int id)
    {
        if (names == null || id < 0 || id >= names.Length) return false;
        string n = names[id]?.Trim() ?? "";
        return n.Length == 0 || n is "???" or "-----" or "—" or "———"
            || n.All(c => c is '?' or '？' or '(' or ')');
    }

    /// <summary>
    /// Lowest id the given table is not already using, or -1.
    /// </summary>
    private static int NextFreeId(string[] names) => NextFreeId(names, null);

    /// <summary>
    /// Lowest free id not already handed out in <paramref name="taken"/>, or -1.
    /// </summary>
    private static int NextFreeId(string[] names, HashSet<int> taken)
    {
        if (names == null) return -1;
        bool Available(int i) => taken == null || !taken.Contains(i);

        for (int i = 1; i < names.Length; i++)
            if (string.IsNullOrWhiteSpace(names[i]) && Available(i)) return i;

        for (int i = 1; i < names.Length; i++)
            if (IsFreeId(names, i) && Available(i)) return i;

        return -1;
    }

    /// <summary>
    /// Start of the lowest run of <paramref name="count"/> consecutive free ids, or -1.
    /// </summary>
    private static int NextFreeRun(string[] names, int count)
    {
        if (names == null || count <= 0) return -1;
        if (count == 1) return NextFreeId(names);

        for (int i = 1; i + count <= names.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < count && ok; k++)
                if (!IsFreeId(names, i + k)) ok = false;
            if (ok) return i;
        }
        return -1;
    }

    /// <summary>The name table a recipe's own slots live in.</summary>
    private static string[] TableForRecipe(Recipe r) => r?.Kind switch
    {
        CustomMechanicKind.Move => Main.Config?.GetText(TextName.MoveNames),
        CustomMechanicKind.Ability => Main.Config?.GetText(TextName.AbilityNames),
        _ => Main.Config?.GetText(TextName.ItemNames),
    };

    /// <summary>
    /// Shows one id box per parameter for a multi-parameter package; hides the row otherwise.
    /// </summary>
    private void BuildRecipeParamBoxes(Recipe r)
    {
        foreach (Control c in _recipeParams.Controls) c.Dispose();
        _recipeParams.Controls.Clear();
        _recipeParamBoxes.Clear();

        var ps = r.Package?.Parameters;
        bool multi = ps is { Count: > 1 };

        bool needsId = r.SlotCount > 0 || multi;

        _recipeParams.Visible = multi;
        _recipeIds.Visible = !multi && needsId;
        _recipeIdLabel.Visible = !multi && needsId;

        if (!multi && needsId)
        {
            int wanted = r.Package != null ? 1 : r.SlotCount;
            int start = NextFreeRun(TableForRecipe(r), wanted);
            _recipeIds.Text = start > 0 ? start.ToString() : "";
        }

        if (!multi) return;

        // Ids already offered in this pass, so two parameters are never handed the same slot.
        var offered = new HashSet<int>();

        foreach (var (_, p) in ps!)
        {
            if (!string.Equals(p?.Type, "list", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string part in (p?.Default ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(part, out int listId)) offered.Add(listId);
        }

        foreach (var (key, p) in ps!)
        {
            // A "list" parameter holds an ordered run of ids (the mints' twenty-one), so it needs
            // room for the whole list rather than a single-number box.
            bool isList = string.Equals(p?.Type, "list", StringComparison.OrdinalIgnoreCase);

            string initial = p?.Default ?? "";
            if (!isList && (p?.AllowUnnamed ?? true) && int.TryParse(initial, out int def))
            {
                var table = TableForParameterType(p?.Type);
                if (!IsFreeId(table, def) || offered.Contains(def))
                {
                    int free = NextFreeId(table, offered);
                    if (free > 0) initial = free.ToString();
                }
            }
            if (int.TryParse(initial, out int claimed)) offered.Add(claimed);

            var box = new TextBox { Width = isList ? 320 : 70, Text = initial };
            _recipeParamBoxes[key] = box;

            string label = string.IsNullOrWhiteSpace(p?.ExpectName) ? key : p!.ExpectName;
            _recipeParams.Controls.Add(new Label
            {
                Text = label + ":",
                AutoSize = true,
                Padding = new Padding(8, 6, 2, 0),
            });
            _recipeParams.Controls.Add(box);
        }

        _recipeParams.Controls.Add(new Label
        {
            Text = $"  ({ps.Count} ids; each needs its own free slot)",
            AutoSize = true,
            ForeColor = Color.Gray,
            Padding = new Padding(8, 6, 0, 0),
        });
    }

    private static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [];
        hex = hex.Trim();
        if (hex.Length % 2 != 0) return [];
        try { return Convert.FromHexString(hex); } catch { return []; }
    }

    /// <summary>Disassembles a package's code, addressed from the base it was assembled for.</summary>
    private static IEnumerable<string> Disassemble(byte[] code, string sourceBase)
    {
        uint at = 0;
        if (!string.IsNullOrWhiteSpace(sourceBase))
        {
            string s = sourceBase.Trim();
            try { at = Convert.ToUInt32(s, s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 : 10); }
            catch { at = 0; }
        }

        for (int i = 0; i + 4 <= code.Length; i += 4)
        {
            uint word = BitConverter.ToUInt32(code, i);
            uint addr = at + (uint)i;
            yield return $"0x{addr:X6}  {ARMCodec.DisassembleWord(word, addr).Split(':').Last().Trim()}";
        }
    }

    private List<string> DescribeRecipeCode(Recipe r)
    {
        var lines = new List<string> { "--- code ---" };

        if (r.EffectKind == RecipeEffectKind.Package && r.Package != null)
        {
            var p = r.Package;
            // File name only. The full path runs through the user's home directory, and this pane
            // is the sort of thing that gets pasted into a bug report.
            if (r.PackagePath != null) lines.Add($"  from {Path.GetFileName(r.PackagePath)}");

            foreach (var m in p.Mechanics ?? [])
            {
                lines.Add($"  {m.Kind} '{m.Name}' id {m.IdText ?? m.Id.ToString()}");
                foreach (var s in m.Slots ?? [])
                {
                    if (s.Reuse != null)
                    {
                        lines.Add($"    {s.Timing}  reuse {s.Reuse.Kind} {s.Reuse.Id} '{s.Reuse.Name}' " +
                                  $"(its own slot {s.Reuse.Timing})");
                        continue;
                    }

                    var code = HexToBytes(s.Code);
                    lines.Add($"    {s.Timing}  {code.Length} bytes of new code, built for {s.SourceBase}");
                    foreach (string d in Disassemble(code, s.SourceBase).Take(10)) lines.Add("        " + d);
                    if (code.Length / 4 > 10) lines.Add($"        … {(code.Length / 4) - 10} more instruction(s)");
                }
            }

            foreach (var b in p.Blocks ?? [])
            {
                var code = HexToBytes(b.Code);
                lines.Add($"  block '{b.Name}': {code.Length} bytes, built for {b.SourceBase}");
                foreach (string d in Disassemble(code, b.SourceBase).Take(8)) lines.Add("      " + d);
            }

            foreach (var sp in p.SitePatches ?? [])
                lines.Add($"  hook {sp.Offset}: {sp.Original} -> {sp.Patched}" +
                          (sp.IsHook ? $"  (to block '{sp.HookTarget}')" : ""));

            foreach (var kv in p.ItemData ?? [])
            {
                string key = kv.Key;
                if (key.StartsWith("${", StringComparison.Ordinal) && key.EndsWith('}'))
                    key = "the id you enter for '" + key[2..^1] + "'";
                lines.Add($"  item data {key}: clone from {kv.Value.CloneFrom}, fling {kv.Value.FlingPower}");
            }

            return lines;
        }

        if (r.EffectKind == RecipeEffectKind.ItemPatch && r.PatchName == null)
        {
            lines.Add("  No code. This adds the item, its name and its description only.");
            lines.Add("  It will sit in the bag and do nothing until a handler is written for it.");
            return lines;
        }

        if (r.EffectKind == RecipeEffectKind.DataOnly)
        {
            lines.Add("  No code: data and text only.");
            return lines;
        }

        if (r.EffectKind == RecipeEffectKind.IpsPatch)
        {
            if (string.IsNullOrWhiteSpace(r.IpsPath) || !File.Exists(r.IpsPath))
            {
                lines.Add($"  '{Path.GetFileName(r.IpsPath ?? "")}' was not found.");
                return lines;
            }

            List<IpsRecord> records;
            try { records = IpsPatch.Read(File.ReadAllBytes(r.IpsPath)); }
            catch (Exception ex) { lines.Add("  Could not read it: " + ex.Message); return lines; }

            if (records.Count == 0) { lines.Add("  It records no writes."); return lines; }

            lines.Add($"  {Path.GetFileName(r.IpsPath)} into code.bin - {IpsPatch.Describe(records)}");
            lines.Add("");

            foreach (var rec in records.Take(48))
            {
                // An IPS offset is a file offset; code.bin loads a megabyte higher, which is the
                // address any disassembly or note about this patch will be written in terms of.
                uint loadAddr = (uint)rec.Offset + 0x100000;
                var bytes = rec.Bytes ?? [];

                string hex = string.Join(" ", bytes.Take(16).Select(b => b.ToString("X2")));
                if (bytes.Length > 16) hex += " …";
                lines.Add($"  0x{rec.Offset:X6} (load 0x{loadAddr:X6})  {bytes.Length} byte(s)  {hex}");

                if (bytes.Length % 4 != 0) continue;
                foreach (string d in Disassemble(bytes, $"0x{loadAddr:X}").Take(6))
                    lines.Add("        " + d);
                if (bytes.Length / 4 > 6) lines.Add($"        … {(bytes.Length / 4) - 6} more instruction(s)");
            }
            if (records.Count > 48) lines.Add($"  … and {records.Count - 48} more record(s)");
            return lines;
        }

        if (r.EffectKind != RecipeEffectKind.CorpusPatch || string.IsNullOrWhiteSpace(r.SheetFile))
        {
            lines.Add("  No recorded byte writes for this recipe.");
            return lines;
        }

        if (_db == null)
        {
            lines.Add("  The research notes are still loading.");
            return lines;
        }

        var patches = _db.Sheets
            .Where(s => string.Equals(Path.GetFileName(s.SourceFile ?? ""), r.SheetFile,
                                      StringComparison.OrdinalIgnoreCase))
            .SelectMany(s => s.Patches)
            .OrderBy(p => p.Offset)
            .ToList();

        if (patches.Count == 0)
        {
            lines.Add($"  '{r.SheetFile}' records no byte writes.");
            return lines;
        }

        var assembled = patches.Where(p => p.Bytes is { Length: > 0 }).ToList();
        var notesOnly = patches.Where(p => p.Bytes is not { Length: > 0 }).ToList();

        if (assembled.Count > 0)
        {
            lines.Add($"  {assembled.Count} write(s) into {r.Target}, " +
                      $"0x{assembled.Min(p => p.Offset):X6}..0x{assembled.Max(p => p.Offset + (uint)p.Bytes.Length):X6}");
            lines.Add("");

            foreach (var p in assembled.Take(48))
            {
                string hex = string.Join(" ", p.Bytes.Select(b => b.ToString("X2")));
                lines.Add($"  0x{p.Offset:X6}  {hex}");

                // Whole 4-byte words are ARM instructions; show what they decode to. Anything else
                // is data (an item id, a jump-table entry) and is left as hex.
                if (p.Bytes.Length % 4 != 0) continue;
                for (int w = 0; w < p.Bytes.Length / 4; w++)
                {
                    uint word = BitConverter.ToUInt32(p.Bytes, w * 4);
                    uint at = p.Offset + (uint)(w * 4);
                    string dis = ARMCodec.DisassembleWord(word, at);
                    lines.Add($"            {dis.Split(':').Last().Trim()}");
                }
            }
            if (assembled.Count > 48) lines.Add($"  … and {assembled.Count - 48} more");
        }

        if (notesOnly.Count == 0) return lines;

        // Rows with assembly text but no assembled bytes. Showing them as if they were patches is
        // what made an unfinished sheet look installable, so they are labelled and kept apart.
        if (assembled.Count > 0) lines.Add("");
        lines.Add($"  {notesOnly.Count} step(s) with NO assembled bytes - working notes, not writable:");
        foreach (var p in notesOnly.Take(40))
        {
            string note = string.IsNullOrWhiteSpace(p.Note) ? "" : $"   ; {p.Note}";
            lines.Add($"    +0x{p.Offset:X4}  {p.Assembly}{note}");
        }
        if (notesOnly.Count > 40) lines.Add($"    … and {notesOnly.Count - 40} more");

        if (assembled.Count == 0)
            lines.Add("  Nothing here can be written to the ROM yet.");

        return lines;
    }

    private void RunRecipe(bool commit)
    {
        if (_recipeList.SelectedItem is not Recipe r) return;
        if (Main.Config == null) { _recipeDetail.Text = "Load a ROM first."; return; }

        var picked = _recipeList.SelectedItems.OfType<Recipe>().ToList();
        if (picked.Count == 0) return;
        if (picked.Count > 1)
        {
            var ips = picked.Where(x => x.EffectKind == RecipeEffectKind.IpsPatch).ToList();
            if (ips.Count != picked.Count)
            {
                _recipeDetail.Text =
                    "Only .ips patches can be applied together. Deselect the others, or run them one at a time.";
                return;
            }
            RunIpsBatch(ips, commit);
            return;
        }

        // A multi-parameter package is driven by its own boxes; everything else by "First id".
        Dictionary<string, string> values = null;
        int first = 0;

        bool needsId = r.SlotCount > 0 || _recipeParamBoxes.Count > 0;

        if (!needsId)
        {
            // No id to collect; Entries is empty, so the assignment loop below does nothing.
        }
        else if (_recipeParamBoxes.Count > 0)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allIds = new List<int>();
            foreach (var (key, box) in _recipeParamBoxes)
            {
                string text = box.Text.Trim();
                var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) { _recipeDetail.Text = $"'{key}' needs at least one item id."; return; }

                var parsed = new List<int>();
                foreach (string part in parts)
                {
                    if (!int.TryParse(part, out int v) || v <= 0)
                    {
                        _recipeDetail.Text = $"'{key}': '{part}' is not a positive item id.";
                        return;
                    }
                    parsed.Add(v);
                }
                values[key] = string.Join(",", parsed);
                allIds.AddRange(parsed);
            }
            first = allIds.Min();
        }
        else if (!int.TryParse(_recipeIds.Text.Trim(), out first) || first <= 0)
        {
            _recipeDetail.Text = "Enter the first id to use.";
            return;
        }

        // Ids are assigned here rather than typed one per slot: every multi-slot recipe needs a
        // consecutive block anyway, so one number is the whole decision.
        for (int i = 0; i < r.Entries.Count; i++) r.Entries[i].Id = first + i;

        string cro = ResolveTargetPath();
        var result = commit
            ? RecipeInstaller.Apply(r, Main.Config, cro, _db, _map, values)
            : RecipeInstaller.Plan(r, Main.Config, cro, _db, _map, values);

        _recipeDetail.Text = ErrorWindow.Redact(
            $"{(commit ? "Add" : "Dry run")}: {r.Name}{Environment.NewLine}{Environment.NewLine}" +
            result.Describe() + Environment.NewLine + Environment.NewLine +
            (result.Ok
                ? (commit ? "Done. Save the ROM from the main window." : "Clean - nothing was written.")
                : "Refused; nothing was written."));
    }

    private void RevertRecipe()
    {
        if (_recipeList.SelectedItem is not Recipe r) return;
        if (Main.Config == null) { _recipeDetail.Text = "Load a ROM first."; return; }

        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo,
                $"Revert/remove '{r.Name}' from the loaded game files?",
                "This will restore original recorded instructions/bytes or file backups for this recipe.") != DialogResult.Yes)
            return;

        string cro = ResolveTargetPath();
        var result = RecipeInstaller.Revert(r, Main.Config, cro, _db, _map);

        _recipeDetail.Text = ErrorWindow.Redact(
            $"Revert: {r.Name}{Environment.NewLine}{Environment.NewLine}" +
            result.Describe() + Environment.NewLine + Environment.NewLine +
            (result.Ok
                ? "Done. The original bytes/backup have been restored."
                : "Reverting encountered issues - see above."));
    }

    private TabPage BuildNotesTab()
    {
        var page = new TabPage("Research notes");

        _noteCategory.Items.AddRange(["All", "Move", "Ability", "Item", "Generic", "AI", "Other", "Field", "Research", "Root"]);
        _noteCategory.SelectedIndex = 0;
        _noteCategory.SelectedIndexChanged += (_, _) => RefreshNotes();
        _noteFilter.TextChanged += (_, _) => RefreshNotes();
        _noteList.SelectedIndexChanged += (_, _) => ShowNote();

        _noteList.Columns.Add("Sheet", 260);
        _noteList.Columns.Add("Category", 80);
        _noteList.Columns.Add("Kind", 120);
        _noteList.Columns.Add("Records", 70);
        _noteList.Columns.Add("Source file", 260);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30 };
        bar.Controls.AddRange([new Label { Text = "Category:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _noteCategory,
                               new Label { Text = "Filter:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _noteFilter,
                               _noteSummary]);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_noteList);
        split.Panel2.Controls.Add(_noteDetail);

        page.Controls.Add(split);
        page.Controls.Add(bar);
        return page;
    }

    private void RefreshNotes()
    {
        _noteList.BeginUpdate();
        _noteList.Items.Clear();
        _noteDetail.Clear();

        if (_db == null)
        {
            _noteSummary.Text = "No notes loaded.";
            _noteList.EndUpdate();
            return;
        }

        string want = _noteCategory.SelectedItem as string ?? "All";
        string f = _noteFilter.Text.Trim();

        int shown = 0;
        foreach (var s in _db.Sheets.OrderBy(s => s.Category).ThenBy(s => s.DisplayName ?? s.SheetName))
        {
            if (want != "All" && !string.Equals(s.Category, want, StringComparison.OrdinalIgnoreCase)) continue;

            string name = s.DisplayName ?? s.SheetName ?? "";
            string file = Path.GetFileName(s.SourceFile ?? "");
            if (f.Length > 0 &&
                !name.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                !file.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                !(s.Kind.ToString().Contains(f, StringComparison.OrdinalIgnoreCase))) continue;

            var row = new ListViewItem(name);
            row.SubItems.Add(s.Category ?? "");
            row.SubItems.Add(s.Kind.ToString());
            row.SubItems.Add(s.RecordCount.ToString("N0"));
            row.SubItems.Add(file);          // file name only - never the folder it came from
            row.Tag = s;
            _noteList.Items.Add(row);
            shown++;
        }

        _noteSummary.Text = $"{shown} of {_db.Sheets.Count} sheet(s)   " +
                            $"{_db.AllFunctions.Count():N0} routines, {_db.AllPatches.Count():N0} recorded writes";
        _noteList.EndUpdate();
    }

    private void ShowNote()
    {
        if (_noteList.SelectedItems.Count == 0 || _noteList.SelectedItems[0].Tag is not ResearchSheet s) return;

        var lines = new List<string>
        {
            s.DisplayName ?? s.SheetName ?? "(unnamed sheet)",
            $"  from        {Path.GetFileName(s.SourceFile ?? "")}",
            $"  category    {s.Category}",
            $"  kind        {s.Kind}  (confidence {s.Confidence:P0})",
            $"  target      {s.Target}",
            "",
            $"  {s.Functions.Count} routine(s), {s.Patches.Count} write(s), {s.Relocations.Count} relocation(s),",
            $"  {s.Tables.Count} table(s), {s.Timings.Count} timing(s), {s.FreeSpace.Count} free-space row(s)",
        };

        if (s.Functions.Count > 0)
        {
            lines.Add("");
            lines.Add("--- routines (usable as {sym:name} in a custom function) ---");
            foreach (var fn in s.Functions.Take(40))
                lines.Add($"  0x{fn.Offset:X6}  {Flatten(fn.Name ?? "")}");
            if (s.Functions.Count > 40) lines.Add($"  … and {s.Functions.Count - 40} more");
        }

        if (s.Timings.Count > 0)
        {
            lines.Add("");
            lines.Add("--- timings ---");
            foreach (var t in s.Timings.Take(20))
                lines.Add($"  0x{t.Value:X2}  {Flatten(t.Meaning ?? "")}");
        }

        if (s.Tables.Count > 0)
        {
            lines.Add("");
            lines.Add("--- tables ---");
            foreach (var t in s.Tables.Take(20))
                lines.Add($"  0x{t.EditedTableData:X6}  {t.Name}  ({t.EditedEntryCount} entries)");
        }

        if (s.Diagnostics.Count > 0)
        {
            lines.Add("");
            lines.Add("--- notes the reader could not use ---");
            foreach (string d in s.Diagnostics.Take(12)) lines.Add("  " + d);
        }

        _noteDetail.Text = string.Join(Environment.NewLine, lines);
    }

    #endregion

    #region Analysis

    private static string CorpusFolder()
    {
        foreach (string candidate in new[]
        {
            Path.Combine(Application.StartupPath, "ARM Functions"),
            Path.Combine(Directory.GetCurrentDirectory(), "ARM Functions"),
            Main.RomFSPath == null ? null : Path.Combine(Main.RomFSPath, "..", "ARM Functions"),
        })
        {
            if (candidate != null && Directory.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }

    private string ResolveTargetPath()
    {
        string pick = _target.SelectedItem as string ?? "Battle.cro";
        if (pick == "code.bin")
        {
            if (string.IsNullOrEmpty(Main.ExeFSPath)) return null;
            foreach (string n in new[] { ".code.bin", "code.bin" })
            {
                string p = Path.Combine(Main.ExeFSPath, n);
                if (File.Exists(p)) return p;
            }
            return null;
        }
        if (string.IsNullOrEmpty(Main.RomFSPath)) return null;
        string cro = Path.Combine(Main.RomFSPath, pick);
        return File.Exists(cro) ? cro : null;
    }

    /// <summary>True while the corpus is being parsed on a worker thread.</summary>
    private bool _loadingCorpus;

    private void Analyze()
    {
        if (_loadingCorpus) return;

        _map = null; _rom = null; _romPath = null; _reserve = (0, 0); _bump = 0;
        _list.Items.Clear(); _detail.Clear();

        var log = new List<string>();

        _romPath = ResolveTargetPath();
        if (_romPath == null)
        {
            _status.Text = "Binary not found — load a ROM in the main window first.";
            _overview.Text = "Could not locate the selected binary.\r\n\r\n" +
                             $"RomFS: {ErrorWindow.Redact(Main.RomFSPath ?? "(not set)")}\r\nExeFS: {ErrorWindow.Redact(Main.ExeFSPath ?? "(not set)")}";
            return;
        }
        _rom = File.ReadAllBytes(_romPath);
        log.Add($"{Path.GetFileName(_romPath)} — {_rom.Length:N0} bytes");
        log.Add(ErrorWindow.Redact(_romPath));

        string corpus = CorpusFolder();

        string version = ResearchVersion.Resolve(Main.Config, Main.RomFSPath);
        if (_dbVersion != version) { _db = null; _dbVersion = version; }

        if (_db == null)
        {
            BeginCorpusLoad(corpus, version);
            return;
        }

        Describe(log, corpus);
    }

    /// <summary>
    /// Parses the corpus on a worker thread, then re-runs <see cref="Analyze"/> with it in hand.
    /// </summary>
    private void BeginCorpusLoad(string corpus, string version)
    {
        _loadingCorpus = true;
        Cursor = Cursors.AppStarting;
        _status.Text = "Reading the research notes…";
        _overview.Text = "Parsing…";

        var progress = new List<string>();

        void Report(string line)
        {
            lock (progress) progress.Add(line);
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(() => _status.Text = ErrorWindow.Redact(line.Trim())); } catch (ObjectDisposedException) { }
        }

        var worker = new System.Threading.Thread(() =>
        {
            ResearchDatabase loaded = null;
            string failure = null;
            try { loaded = ResearchDatabase.LoadBest(corpus, version, ResearchVersion.CachePathFor(version), Report); }
            catch (Exception ex) { failure = $"{ex.GetType().Name}: {ex.Message}"; }

            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(() => FinishCorpusLoad(version, loaded, failure)); }
            catch (ObjectDisposedException) { }
        })
        { IsBackground = true, Name = "research-corpus" };

        worker.Start();
    }

    private void FinishCorpusLoad(string version, ResearchDatabase loaded, string failure)
    {
        _loadingCorpus = false;
        Cursor = Cursors.Default;

        if (failure != null)
        {
            _status.Text = "Research notes could not be read.";
            _overview.Text = $"The research notes failed to load:{Environment.NewLine}{Environment.NewLine}{failure}";
            return;
        }

        if (_dbVersion == version) _db = loaded;

        Analyze();
    }

    /// <summary>Everything after the corpus is loaded. Milliseconds, so it stays on the UI thread.</summary>
    private void Describe(List<string> log, string corpus)
    {
        // code.bin is not a CRO: report what can be reported and stop.
        if (_romPath.EndsWith("bin", StringComparison.OrdinalIgnoreCase))
        {
            _symbols = _db.FunctionSymbols(ResearchTarget.CodeBin);
            log.Add("");
            log.Add("code.bin is a flat binary, not a CRO — mechanic tables and relocation checks do");
            log.Add("not apply. Hooks and corpus patches still work against it.");
            _overview.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
            _status.Text = $"{Path.GetFileName(_romPath)} loaded ({_symbols.Count} symbols).";
            RefreshNotes();
            return;
        }

        _symbols = _db.FunctionSymbols(TargetOf(_romPath));
        _map = BattleMechanicMap.Build(_rom, _db, _romPath);

        log.Add("");
        log.Add(_map.Summary());

        if (_map.Cro != null)
        {
            _reserve = CodeRelocator.FindReserve(_map.Cro);
            _bump = _reserve.Offset;
            log.Add("");
            log.Add(_reserve.Length > 0
                ? $"reserve: 0x{_reserve.Offset:X6} .. 0x{_reserve.Offset + (uint)_reserve.Length:X6} ({_reserve.Length:N0} bytes free)"
                : "reserve: none found — new code cannot be placed until the segment is expanded");

            var report = CroVerifier.Verify(_map.Cro);
            log.Add($"verify: {report.RelocationsChecked:N0} relocation(s), {(report.Ok ? "no errors" : "ERRORS")}");
            foreach (var f in report.Findings.Where(f => f.Severity != PlanSeverity.Info)) log.Add("  " + f);
        }

        _overview.Text = ErrorWindow.Redact(string.Join(Environment.NewLine, log));
        _status.Text = _map == null || _map.Mechanics.Count == 0
            ? "Loaded, but no mechanics were resolved."
            : $"{Path.GetFileName(_romPath)} — {_map.Mechanics.Count} mechanics, {_reserve.Length:N0} bytes reserve.";

        RefreshList();
        RefreshNotes();
        RefreshRecipes();
        DescribeId();
    }

    private static ResearchTarget TargetOf(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "battle.cro" => ResearchTarget.BattleCro,
        "bag.cro" => ResearchTarget.BagCro,
        "shop.cro" => ResearchTarget.ShopCro,
        "evolution.cro" => ResearchTarget.EvolutionCro,
        _ => ResearchTarget.CodeBin,
    };

    private void RunVerify()
    {
        if (_map?.Cro == null) { _status.Text = "Nothing to verify."; return; }
        var report = CroVerifier.Verify(_map.Cro);
        var lines = new List<string> { $"verify: {report.RelocationsChecked:N0} relocations, ok={report.Ok}, broken chains={report.ChainsBroken}" };
        lines.AddRange(report.Findings.Select(f => "  " + f));
        _overview.Text = string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine + _overview.Text;
    }

    #endregion

    #region Mechanics browser

    private CustomMechanicKind SelectedKind => (_kind.SelectedItem as string) switch
    {
        "Ability" => CustomMechanicKind.Ability,
        "Item" => CustomMechanicKind.Item,
        _ => CustomMechanicKind.Move,
    };

    /// <summary>The four things this ROM's Battle.cro can be browsed as.</summary>
    private enum MechView { Effects, Timings, Tables, Routines }

    private MechView SelectedView => (_kind.SelectedItem as string) switch
    {
        "Timings" => MechView.Timings,
        "Master tables" => MechView.Tables,
        "Engine routines" => MechView.Routines,
        _ => MechView.Effects,
    };

    /// <summary>
    /// Counts per kind and the next unused id, so adding an entry does not start with a hunt for
    /// somewhere to put it.
    /// </summary>
    private readonly Label _mechSummary = new()
    {
        AutoSize = true,
        Padding = new Padding(12, 6, 0, 0),
        ForeColor = Color.Gray,
    };

    private void UpdateMechanicSummary()
    {
        if (_map == null) { _mechSummary.Text = ""; return; }

        switch (SelectedView)
        {
            case MechView.Timings:
                var hist = _map.TimingHistogram();
                int named = _db == null ? 0 : hist.Keys.Count(k => _db.Timings.ContainsKey(k));
                _mechSummary.Text = $"{hist.Count} distinct timing(s) in use, {named} documented, " +
                                    $"{hist.Values.Sum()} slot(s) total";
                return;

            case MechView.Tables:
                _mechSummary.Text = $"{_map.Tables.Count} master table(s) located by id fingerprint";
                return;

            case MechView.Routines:
                _mechSummary.Text = $"{_symbols.Count} documented routine(s) in {Path.GetFileName(_romPath)}";
                return;
        }

        var parts = new List<string>();
        foreach (CustomMechanicKind k in Enum.GetValues<CustomMechanicKind>())
            parts.Add($"{k}: {_map.OfKind(k).Count()}");

        // Highest mapped id for the selected kind; a new entry goes above it.
        var ids = _map.OfKind(SelectedKind).Select(m => m.Id).ToList();
        string next = ids.Count > 0
            ? $"   next free {SelectedKind.ToString().ToLowerInvariant()} id: {ids.Max() + 1}"
            : "";

        _mechSummary.Text = string.Join("   ", parts) + next;
    }

    /// <summary>Rebuilds the column headers, since each view shows different fields.</summary>
    private void SetColumns(params (string Header, int Width)[] cols)
    {
        _list.Columns.Clear();
        foreach (var (header, width) in cols) _list.Columns.Add(header, width);
    }

    private void RefreshList()
    {
        UpdateMechanicSummary();
        _list.BeginUpdate();
        _list.Items.Clear();
        _detail.Clear();

        string f = _filter.Text.Trim();
        bool Matches(params string[] fields) =>
            f.Length == 0 || fields.Any(s => (s ?? "").Contains(f, StringComparison.OrdinalIgnoreCase));

        switch (SelectedView)
        {
            case MechView.Timings: FillTimings(Matches); break;
            case MechView.Tables: FillTables(Matches); break;
            case MechView.Routines: FillRoutines(Matches); break;
            default: FillEffects(Matches); break;
        }

        _list.EndUpdate();
    }

    private void FillEffects(Func<string[], bool> matches)
    {
        SetColumns(("Id", 60), ("Name", 190), ("Handler", 90), ("Shape", 90), ("Timings", 220));
        if (_map == null) return;

        foreach (var m in _map.OfKind(SelectedKind))
        {
            if (!matches([m.Name, m.Id.ToString(), $"0x{m.Id:X}"])) continue;

            var row = new ListViewItem($"{m.Id}");
            row.SubItems.Add(m.Name ?? "");
            row.SubItems.Add($"0x{m.HandlerOffset:X6}");
            row.SubItems.Add(m.Chain?.Shape.ToString() ?? "?");
            row.SubItems.Add(string.Join(", ", m.Slots.Select(s => $"0x{s.Timing:X2}")));
            row.Tag = m;
            _list.Items.Add(row);
        }
    }

    /// <summary>
    /// The engine's event vocabulary: which timing bytes this ROM actually dispatches on, how
    /// heavily each is used, and what the corpus says each one means.
    /// </summary>
    private void FillTimings(Func<string[], bool> matches)
    {
        SetColumns(("Timing", 70), ("Uses", 60), ("Moves", 60), ("Abilities", 70), ("Items", 60), ("Meaning", 320));
        if (_map == null) return;

        var hist = _map.TimingHistogram();
        foreach (var (timing, count) in hist.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
        {
            string meaning = "";
            if (_db != null && _db.Timings.TryGetValue(timing, out var documented))
                meaning = documented?.Meaning ?? "";

            if (!matches([$"0x{timing:X2}", timing.ToString(), meaning])) continue;

            int Used(CustomMechanicKind k) => _map.OfKind(k).Count(m => m.Slots.Any(s => s.Timing == timing));

            var row = new ListViewItem($"0x{timing:X2}");
            row.SubItems.Add(count.ToString());
            row.SubItems.Add(Used(CustomMechanicKind.Move).ToString());
            row.SubItems.Add(Used(CustomMechanicKind.Ability).ToString());
            row.SubItems.Add(Used(CustomMechanicKind.Item).ToString());
            row.SubItems.Add(Flatten(meaning));
            row.Tag = timing;
            _list.Items.Add(row);
        }
    }

    /// <summary>
    /// The master tables themselves — where each one landed in this build, and how confidently.
    /// </summary>
    private void FillTables(Func<string[], bool> matches)
    {
        SetColumns(("Table", 260), ("Kind", 80), ("Address", 90), ("Entries", 70), ("Match", 90), ("Documented", 90));
        if (_map == null) return;

        foreach (var t in _map.Tables)
        {
            if (!matches([t.Name, t.Kind?.ToString(), $"0x{t.TableOffset:X6}"])) continue;

            var row = new ListViewItem(t.Name);
            row.SubItems.Add(t.Kind?.ToString() ?? "—");
            row.SubItems.Add($"0x{t.TableOffset:X6}");
            row.SubItems.Add(t.EntryCount.ToString("N0"));
            row.SubItems.Add(t.Compared == 0 ? "—" : $"{t.Matched}/{t.Compared}");
            row.SubItems.Add(t.DocumentedOffset == 0 ? "—" : $"0x{t.DocumentedOffset:X6}");
            row.Tag = t;
            _list.Items.Add(row);
        }
    }

    /// <summary>
    /// Every routine the corpus documents in this binary — the palette a custom function calls into.
    /// </summary>
    private void FillRoutines(Func<string[], bool> matches)
    {
        SetColumns(("Address", 90), ("Name", 480), ("Origin", 90));

        // Unfiltered this is well over a thousand rows of little use; the filter is the point.
        foreach (var (offset, fn) in _symbols.OrderBy(kv => kv.Key))
        {
            string name = Flatten(fn.Name ?? "");
            if (!matches([name, $"0x{offset:X6}"])) continue;

            var row = new ListViewItem($"0x{offset:X6}");
            row.SubItems.Add(name);
            row.SubItems.Add(fn.Origin.ToString());
            row.Tag = fn;
            _list.Items.Add(row);
        }
    }

    private void ShowDetail()
    {
        if (_list.SelectedItems.Count == 0) return;
        object tag = _list.SelectedItems[0].Tag;

        _detail.Text = string.Join(Environment.NewLine, tag switch
        {
            MappedMechanic m => DetailForMechanic(m),
            byte timing => DetailForTiming(timing),
            LocatedMechanicTable t => DetailForTable(t),
            ResearchFunction fn => DetailForRoutine(fn),
            _ => [],
        });
    }

    private List<string> DetailForMechanic(MappedMechanic m)
    {
        var lines = new List<string>
        {
            $"{m.Kind} {m.Id} (0x{m.Id:X})  {m.Name}",
            $"  master entry  0x{m.EntryOffset:X6}",
            $"  handler       0x{m.HandlerOffset:X6}   ({m.Chain?.Shape})",
        };
        if (m.Chain is { HasTimingTable: true })
            lines.Add($"  timing table  0x{m.Chain.TimingTableOffset:X6}   {m.Slots.Count} slot(s)");
        if (!string.IsNullOrWhiteSpace(m.Chain?.Note)) lines.Add($"  note          {m.Chain.Note}");

        foreach (var slot in m.Slots)
        {
            lines.Add("");
            string meaning = "";
            if (_db != null && _db.Timings.TryGetValue(slot.Timing, out var doc) && !string.IsNullOrWhiteSpace(doc.Meaning))
                meaning = "  " + Flatten(doc.Meaning);
            lines.Add($"--- timing 0x{slot.Timing:X2} -> 0x{slot.FunctionOffset:X6} ---{meaning}");
            lines.AddRange(Disassemble(slot.FunctionOffset, 48));
        }
        return lines;
    }

    private List<string> DetailForTiming(byte timing)
    {
        var lines = new List<string> { $"timing 0x{timing:X2} ({timing})" };

        if (_db != null && _db.Timings.TryGetValue(timing, out var doc))
        {
            if (!string.IsNullOrWhiteSpace(doc.Meaning)) lines.Add("  meaning   " + Flatten(doc.Meaning));
            if (!string.IsNullOrWhiteSpace(doc.Examples)) lines.Add("  examples  " + Flatten(doc.Examples));
        }
        else
        {
            lines.Add("  not documented in the corpus - the usage below is the only evidence for what it means.");
        }

        foreach (var kind in new[] { CustomMechanicKind.Move, CustomMechanicKind.Ability, CustomMechanicKind.Item })
        {
            var users = _map.OfKind(kind).Where(m => m.Slots.Any(s => s.Timing == timing)).ToList();
            if (users.Count == 0) continue;

            lines.Add("");
            lines.Add($"--- {users.Count} {kind.ToString().ToLowerInvariant()}(s) hook this timing ---");

            // Enough to recognise the pattern without turning the pane into a wall of ids.
            foreach (var u in users.Take(40))
            {
                uint fn = u.Slots.First(s => s.Timing == timing).FunctionOffset;
                lines.Add($"  {u.Id,5}  0x{fn:X6}  {u.Name}");
            }
            if (users.Count > 40) lines.Add($"  … and {users.Count - 40} more");
        }
        return lines;
    }

    private List<string> DetailForTable(LocatedMechanicTable t)
    {
        var lines = new List<string>
        {
            t.Name,
            $"  kind        {t.Kind?.ToString() ?? "not a move/ability/item table"}",
            $"  address     0x{t.TableOffset:X6} in this ROM",
            $"  entries     {t.EntryCount:N0}",
        };

        if (t.DocumentedOffset != 0)
        {
            long delta = (long)t.TableOffset - t.DocumentedOffset;
            lines.Add($"  documented  0x{t.DocumentedOffset:X6}" +
                      (delta == 0 ? "  (identical - this build matches the research notes)"
                                  : $"  (moved {(delta > 0 ? "+" : "")}0x{Math.Abs(delta):X} in this build)"));
        }

        lines.Add(t.Compared == 0
            ? "  match       located without an id comparison"
            : $"  match       {t.Matched}/{t.Compared} documented ids agree" +
              (t.Matched == t.Compared ? "  (exact)" : "  — anything built on a partial match can land in the wrong place"));

        if (t.Kind != null && _map != null)
        {
            var entries = _map.OfKind(t.Kind.Value).ToList();
            if (entries.Count > 0)
            {
                lines.Add("");
                lines.Add($"--- first entries ---");
                foreach (var m in entries.Take(24))
                    lines.Add($"  {m.Id,5}  0x{m.EntryOffset:X6} -> 0x{m.HandlerOffset:X6}  {m.Name}");
                if (entries.Count > 24) lines.Add($"  … and {entries.Count - 24} more");
            }
        }
        return lines;
    }

    private List<string> DetailForRoutine(ResearchFunction fn)
    {
        var lines = new List<string>
        {
            Flatten(fn.Name ?? "(unnamed)"),
            $"  address     0x{fn.Offset:X6}",
        };
        if (fn.LoadedAddress != 0) lines.Add($"  loaded at   0x{fn.LoadedAddress:X8}");
        lines.Add($"  origin      {fn.Origin}");
        if (!string.IsNullOrWhiteSpace(fn.Details)) lines.Add($"  details     {Flatten(fn.Details)}");

        lines.Add("");
        lines.Add("  Call it from a custom function with:");
        lines.Add($"      BL {{sym:{Flatten(fn.Name ?? "")}}}");
        lines.Add("");
        lines.Add($"--- 0x{fn.Offset:X6} ---");
        lines.AddRange(Disassemble(fn.Offset, 64));
        return lines;
    }

    /// <summary>
    /// Disassembles until the routine returns, annotating calls with documented names. Stopping at
    /// the return keeps the pane readable and avoids running off into the next function.
    /// </summary>
    /// <summary>
    /// Disassembles a routine in the same shape the research workbooks use: offset, hex, the
    /// instruction, then a note.
    /// </summary>
    private List<string> Disassemble(uint at, int maxWords)
    {
        var lines = new List<string>();
        if (_rom == null) return lines;

        lines.Add("  offset    hex        instruction");
        for (int i = 0; i < maxWords; i++)
        {
            uint a = at + (uint)(i * 4);
            if (a + 4 > _rom.Length) break;
            uint w = BitConverter.ToUInt32(_rom, (int)a);

            string note = "";
            if (ARMCodec.IsBranch(w))
            {
                uint t = ARMCodec.DecodeBranchTarget(w, a);
                if (_symbols.TryGetValue(t, out var fn) && !string.IsNullOrWhiteSpace(fn.Name))
                    note = "   ; " + Flatten(fn.Name);
            }
            if (note.Length == 0) note = AnnotateImmediate(w);

            string hex = string.Join("", BitConverter.GetBytes(w).Select(b => b.ToString("X2")));
            string asm = ARMCodec.DisassembleWord(w, a).Split(':').Last().Trim();
            lines.Add($"  0x{a:X6}  {hex}   {asm,-32}{note}");

            if ((w & 0x0FFFFFFF) == 0x012FFF1E) break;              // BX LR
            if ((w & 0x0FFF8000) == 0x08BD8000) break;              // POP {..., PC}
        }
        return lines;
    }

    /// <summary>
    /// What a compared-against constant probably means, when it matches an engine enumeration.
    /// </summary>
    private static string AnnotateImmediate(uint w)
    {
        // Data-processing immediate, opcode CMP (1010), S set.
        if ((w & 0x0FE00000) != 0x03400000 && (w & 0x0FF00000) != 0x03500000) return "";

        uint imm = w & 0xFF;
        int rot = (int)((w >> 8) & 0xF) * 2;
        uint value = rot == 0 ? imm : (imm >> rot) | (imm << (32 - rot));
        if (value > 0x11) return "";

        var readings = new List<string>();
        void Add(string set, IReadOnlyList<IdChoice> from)
        {
            foreach (var c in from)
                if (c.Value == (int)value) { readings.Add($"{set} {c.Name}"); return; }
        }
        Add("type", GameIds.Types);
        Add("weather", GameIds.Weather);
        Add("terrain", GameIds.Terrain);
        Add("status", GameIds.Status);

        return readings.Count == 0 ? "" : "   ; " + string.Join(" / ", readings);
    }

    private static string Flatten(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var f = s.Replace('\r', ' ').Replace('\n', ';').Trim();
        while (f.Contains("  ")) f = f.Replace("  ", " ");
        return f.Length <= 90 ? f : f[..87] + "...";
    }

    #endregion

    #region Install

    private CustomMechanicKind NewKind => (_newKind.SelectedItem as string) switch
    {
        "Ability" => CustomMechanicKind.Ability,
        "Item" => CustomMechanicKind.Item,
        _ => CustomMechanicKind.Move,
    };

    private string[] NameTableFor(CustomMechanicKind kind)
    {
        if (Main.Config == null) return null;
        try
        {
            return kind switch
            {
                CustomMechanicKind.Ability => Main.Config.GetText(TextName.AbilityNames),
                CustomMechanicKind.Item => Main.Config.GetText(TextName.ItemNames),
                _ => Main.Config.GetText(TextName.MoveNames),
            };
        }
        catch { return null; }
    }

    /// <summary>
    /// Reports what the entered id actually names in this build. Picking an id by reasoning rather
    /// than lookup is how two abilities once landed on Intrepid Sword and Dauntless Shield, so the
    /// answer is shown before anything is installed.
    /// </summary>
    private void DescribeId()
    {
        var note = _tabs.TabPages.Cast<TabPage>()
            .SelectMany(p => p.Controls.Cast<Control>().SelectMany(Descendants))
            .FirstOrDefault(c => c.Name == "idNote");
        if (note == null) return;

        if (!TryParseNumber(_newId.Text, out uint id)) { note.Text = ""; return; }

        var names = NameTableFor(NewKind);
        if (names == null) { note.Text = "(name table unavailable)"; note.ForeColor = Color.Gray; return; }
        if (id >= names.Length) { note.Text = $"past the end of the {NewKind} list ({names.Length})"; note.ForeColor = Color.Firebrick; return; }

        string named = (names[id] ?? "").Trim();
        bool free = named is "" or "???" or "-----" or "—";
        bool taken = _map?.Find(NewKind, id) != null;

        note.Text = taken ? $"'{named}' — already has an effect entry"
                  : free ? $"unnamed slot ('{named}') — free"
                  : $"'{named}'";
        note.ForeColor = taken ? Color.Firebrick : free ? Color.DarkGoldenrod : Color.DarkGreen;
    }

    private static IEnumerable<Control> Descendants(Control c)
    {
        yield return c;
        foreach (Control child in c.Controls)
            foreach (var d in Descendants(child)) yield return d;
    }

    private void ShowTimingUsage()
    {
        if (_map == null) { _installLog.Text = "Load a CRO first."; return; }
        if (!TryParseNumber(_newTiming.Text, out uint t)) { _installLog.Text = "Enter a timing byte first."; return; }

        byte timing = (byte)t;
        var hist = _map.TimingHistogram();
        var sameKind = _map.OfKind(NewKind).Where(m => m.Slots.Any(s => s.Timing == timing))
                           .Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(12).ToList();
        var others = _map.Mechanics.Where(m => m.Kind != NewKind && m.Slots.Any(s => s.Timing == timing))
                          .Select(m => $"{m.Kind} {m.Name}").Where(n => n.Length > 6).Distinct().Take(12).ToList();

        var lines = new List<string>
        {
            $"timing 0x{timing:X2} is used {hist.GetValueOrDefault(timing)} time(s) in this ROM.",
            "",
            "The timing vocabulary is largely undocumented, so the reliable way to choose one is to",
            "copy a mechanic that already fires when you want yours to.",
            "",
            $"On {NewKind}s: {(sameKind.Count == 0 ? "(none)" : string.Join(", ", sameKind))}",
            $"Elsewhere:   {(others.Count == 0 ? "(none)" : string.Join(", ", others))}",
        };
        _installLog.Text = string.Join(Environment.NewLine, lines);
    }

    private void DoInstall(bool commit)
    {
        if (_map?.Cro == null) { _installLog.Text = "Load a CRO first."; return; }
        if (!TryParseNumber(_newId.Text, out uint id)) { _installLog.Text = "Game id is not a number."; return; }
        if (!TryParseNumber(_newTiming.Text, out uint timing)) { _installLog.Text = "Timing is not a number."; return; }

        var effect = new MechanicEffect { Timing = (byte)timing, Name = _newName.Text };
        string body = _newBody.Text.Trim();

        try
        {
            switch (_newSource.SelectedIndex)
            {
                case 1:
                    effect.Code = Convert.FromHexString(new string(body.Where(Uri.IsHexDigit).ToArray()));
                    break;
                case 2:
                    if (!TryParseNumber(body, out uint existing)) { _installLog.Text = "Enter the address of the routine to reuse."; return; }
                    effect.ExistingFunction = existing;
                    break;
                default:
                    effect.Code = ARMCodec.Assemble(body);
                    break;
            }
        }
        catch (Exception ex) { _installLog.Text = "Could not build the effect code: " + ex.Message; return; }

        // Work on a copy so a failed plan never touches the loaded image.
        var working = CRODecompiler.DecompileStructure((byte[])_rom.Clone(), _romPath);
        var map = BattleMechanicMap.Build((byte[])_rom.Clone(), _db, _romPath);
        uint bump = _bump;

        var request = new NewMechanicRequest
        {
            Kind = NewKind, Id = id, Name = _newName.Text, NameTable = NameTableFor(NewKind),
            Effects = [effect],
        };

        var result = MechanicInstaller.AddMechanic(map.Cro, map, request, _reserve, ref bump);
        var lines = new List<string>(result.Log);
        lines.AddRange(result.Errors.Select(e => "ERROR: " + e));

        if (!result.Success) { _installLog.Text = string.Join(Environment.NewLine, lines); return; }

        byte[] rebuilt = CROCompiler.Compile(map.Cro);
        var check = CroVerifier.Verify(CRODecompiler.DecompileStructure((byte[])rebuilt.Clone(), _romPath));
        lines.Add("");
        lines.Add($"verify: {check.RelocationsChecked:N0} relocations, ok={check.Ok}");
        foreach (var f in check.Findings.Where(f => f.Severity != PlanSeverity.Info)) lines.Add("  " + f);

        // Nothing that already worked may change.
        int changed = _map.Mechanics.Count(before =>
        {
            var after = BattleMechanicMap.Build(rebuilt, _db, _romPath).Find(before.Kind, before.Id);
            return after == null || after.HandlerOffset != before.HandlerOffset;
        });

        if (!commit)
        {
            lines.Insert(0, "DRY RUN — nothing written.");
            _installLog.Text = string.Join(Environment.NewLine, lines);
            return;
        }

        if (!check.Ok)
        {
            lines.Insert(0, "REFUSED — verification failed, nothing written.");
            _installLog.Text = string.Join(Environment.NewLine, lines);
            return;
        }

        string backup = Backup(_romPath);
        File.WriteAllBytes(_romPath, rebuilt);
        lines.Insert(0, $"WROTE {Path.GetFileName(_romPath)} (previous state saved as {Path.GetFileName(backup)})");
        _installLog.Text = string.Join(Environment.NewLine, lines);
        Analyze();
    }

    #endregion

    #region Hooks

    private void DoHook(bool commit)
    {
        if (_rom == null) { _hookLog.Text = "Load a binary first."; return; }
        if (!TryParseNumber(_hookSite.Text, out uint site)) { _hookLog.Text = "Hook address is not a number."; return; }
        if (_reserve.Length <= 0) { _hookLog.Text = "No reserve space in this binary — expand it first."; return; }

        byte[] working = (byte[])_rom.Clone();
        uint bump = _bump;

        var hook = new CodeHook
        {
            Name = $"hook @0x{site:X6}",
            Site = site,
            Assembly = _hookAsm.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList(),
        };

        var result = CodePatchInstaller.InstallHook(working, hook, _reserve, ref bump);
        var lines = new List<string> { result.Describe() };

        if (result.Success)
        {
            lines.Add("");
            lines.Add("block as assembled:");
            for (int i = 0; i < result.BlockLength; i += 4)
            {
                uint a = result.BlockOffset + (uint)i;
                lines.Add($"  0x{a:X6}  {ARMCodec.DisassembleWord(BitConverter.ToUInt32(working, (int)a), a).Split(':').Last().Trim()}");
            }
        }

        if (!commit || !result.Success)
        {
            lines.Insert(0, result.Success ? "DRY RUN — nothing written." : "FAILED — nothing written.");
            _hookLog.Text = string.Join(Environment.NewLine, lines);
            return;
        }

        Commit(working, lines);
        _hookLog.Text = string.Join(Environment.NewLine, lines);
    }

    private void DoNop()
    {
        if (_rom == null) { _hookLog.Text = "Load a binary first."; return; }
        if (!TryParseNumber(_nopSite.Text, out uint site)) { _hookLog.Text = "NOP address is not a number."; return; }

        byte[] working = (byte[])_rom.Clone();
        int count = (int)_nopCount.Value;

        // Show what is about to be destroyed. Blanket-NOPing a range that happens to hold a
        // function prologue removes the argument save with it, which fails far from the patch.
        var before = new List<string> { "replacing:" };
        for (int i = 0; i < count; i++)
        {
            uint a = site + (uint)(i * 4);
            if (a + 4 > working.Length) break;
            before.Add($"  0x{a:X6}  {ARMCodec.DisassembleWord(BitConverter.ToUInt32(working, (int)a), a).Split(':').Last().Trim()}");
        }

        var result = CodePatchInstaller.Nop(working, [new CodeNop { Name = "manual", Offset = site, Length = count * 4 }]);
        before.Add("");
        before.Add(result.Describe());

        if (!result.Success) { _hookLog.Text = string.Join(Environment.NewLine, before); return; }

        if (MessageBox.Show(string.Join(Environment.NewLine, before) +
                            Environment.NewLine + Environment.NewLine + "Write these NOPs?",
                            "Confirm", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            before.Insert(0, "CANCELLED — nothing written.");
            _hookLog.Text = string.Join(Environment.NewLine, before);
            return;
        }

        Commit(working, before);
        _hookLog.Text = string.Join(Environment.NewLine, before);
    }

    #endregion


    #region Writing

    /// <summary>Backs up, writes, then re-verifies. A CRO that fails verification is rolled back.</summary>
    private void Commit(byte[] bytes, List<string> log)
    {
        string backup = Backup(_romPath);
        File.WriteAllBytes(_romPath, bytes);
        log.Insert(0, $"WROTE {Path.GetFileName(_romPath)} (previous state saved as {Path.GetFileName(backup)})");

        if (!_romPath.EndsWith("cro", StringComparison.OrdinalIgnoreCase)) { Analyze(); return; }

        var report = CroVerifier.Verify(CRODecompiler.DecompileStructure((byte[])bytes.Clone(), _romPath));
        if (!report.Ok)
        {
            File.Copy(backup, _romPath, true);
            log.Insert(0, "ROLLED BACK — the result failed verification:");
            log.InsertRange(1, report.Errors.Select(e => "  " + e));
        }
        Analyze();
    }

    private static string Backup(string path)
    {
        for (int i = 1; ; i++)
        {
            string candidate = $"{path}.bak{(i == 1 ? "" : i.ToString())}";
            if (File.Exists(candidate)) continue;
            File.Copy(path, candidate);
            return candidate;
        }
    }

    private static bool TryParseNumber(string text, out uint value)
    {
        value = 0;
        string t = (text ?? "").Trim();
        if (t.Length == 0) return false;
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return uint.TryParse(t, out value);
    }

    #endregion
}
