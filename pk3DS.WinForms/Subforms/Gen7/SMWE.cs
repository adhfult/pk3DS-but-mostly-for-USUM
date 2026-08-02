using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public partial class SMWE : Form
{
    public SMWE(LazyGARCFile ed, LazyGARCFile zd, LazyGARCFile wd)
    {
        InitializeComponent();

        PB_DayIcon.Image = Properties.Resources.sun;
        PB_NightIcon.Image = Properties.Resources.moon;
        PB_DayIcon.SizeMode = PictureBoxSizeMode.CenterImage;
        PB_NightIcon.SizeMode = PictureBoxSizeMode.CenterImage;

        font = L_Location.Font;

        speciesList[0] = "(None)";
        var locationList = Main.Config.GetText(TextName.metlist_000000);
        locationList = GetGoodLocationList(locationList);

        nup_spec = LoadFormeNUD();
        cb_spec = LoadSpeciesComboBoxes();
        rate_spec = LoadRateNUD();

        encdata = ed;
        var areas = Area7.GetArray(ed, zd, wd, locationList);
        Areas = areas.Where(a => a.HasTables && a.Zones.Length > 0).OrderBy(a => a.Name).ToArray();
        locationlist = locationList;
        infiles = Areas.Select(a => a.Tables[0].Data).ToArray();
        files = infiles;

        LoadData();
        RandSettings.GetFormSettings(this, GB_Tweak.Controls);

        var weather = string.Format("If weather is active, create a random number.{0}If 0, use slot 0.{0}If <= 10, use slot 1.{0}Else, pick an SOS table and a slot.", Environment.NewLine);
        new ToolTip().SetToolTip(L_AddSOS, weather);
        var sos = new[] { L_SOS1, L_SOS2, L_SOS3, L_SOS4, L_SOS5, L_SOS6, L_SOS7 };
        var rates = new[] { 1, 1, 1, 10, 10, 10, 67 };
        for (int i = 0; i < sos.Length; i++)
            new ToolTip().SetToolTip(sos[i], $"Table Selection Rate: {rates[i]}%");

        // ExportEncounters("um", "uu");
    }

    private NumericUpDown[] LoadRateNUD()
    {
        var list = new[] { NUP_Rate1, NUP_Rate2, NUP_Rate3, NUP_Rate4, NUP_Rate5, NUP_Rate6, NUP_Rate7, NUP_Rate8, NUP_Rate9, NUP_Rate10 };
        foreach (var nup in list)
            nup.ValueChanged += UpdateEncounterRate;
        return list;
    }

    private ComboBox[][] LoadSpeciesComboBoxes()
    {
        var list = new[] {
            [CB_Enc01, CB_Enc02, CB_Enc03, CB_Enc04, CB_Enc05, CB_Enc06, CB_Enc07, CB_Enc08, CB_Enc09, CB_Enc10],
            [CB_Enc11, CB_Enc12, CB_Enc13, CB_Enc14, CB_Enc15, CB_Enc16, CB_Enc17, CB_Enc18, CB_Enc19, CB_Enc20],
            [CB_Enc21, CB_Enc22, CB_Enc23, CB_Enc24, CB_Enc25, CB_Enc26, CB_Enc27, CB_Enc28, CB_Enc29, CB_Enc30],
            [CB_Enc31, CB_Enc32, CB_Enc33, CB_Enc34, CB_Enc35, CB_Enc36, CB_Enc37, CB_Enc38, CB_Enc39, CB_Enc40],
            [CB_Enc41, CB_Enc42, CB_Enc43, CB_Enc44, CB_Enc45, CB_Enc46, CB_Enc47, CB_Enc48, CB_Enc49, CB_Enc50],
            [CB_Enc51, CB_Enc52, CB_Enc53, CB_Enc54, CB_Enc55, CB_Enc56, CB_Enc57, CB_Enc58, CB_Enc59, CB_Enc60],
            [CB_Enc61, CB_Enc62, CB_Enc63, CB_Enc64, CB_Enc65, CB_Enc66, CB_Enc67, CB_Enc68, CB_Enc69, CB_Enc70],
            [CB_Enc71, CB_Enc72, CB_Enc73, CB_Enc74, CB_Enc75, CB_Enc76, CB_Enc77, CB_Enc78, CB_Enc79, CB_Enc80],
            new[] {CB_WeatherEnc1, CB_WeatherEnc2, CB_WeatherEnc3, CB_WeatherEnc4, CB_WeatherEnc5, CB_WeatherEnc6},
        };
        foreach (var cb_l in list)
        {
            foreach (var cb in cb_l)
            {
                cb.Items.AddRange(speciesList);
                cb.SelectedIndex = 0;
                cb.SelectedIndexChanged += UpdateSpeciesForm;
            }
        }

        return list;
    }

    private NumericUpDown[][] LoadFormeNUD()
    {
        var list = new[] {
            [NUP_Forme01, NUP_Forme02, NUP_Forme03, NUP_Forme04, NUP_Forme05, NUP_Forme06, NUP_Forme07, NUP_Forme08, NUP_Forme09, NUP_Forme10,
            ],
            [NUP_Forme11, NUP_Forme12, NUP_Forme13, NUP_Forme14, NUP_Forme15, NUP_Forme16, NUP_Forme17, NUP_Forme18, NUP_Forme19, NUP_Forme20,
            ],
            [NUP_Forme21, NUP_Forme22, NUP_Forme23, NUP_Forme24, NUP_Forme25, NUP_Forme26, NUP_Forme27, NUP_Forme28, NUP_Forme29, NUP_Forme30,
            ],
            [NUP_Forme31, NUP_Forme32, NUP_Forme33, NUP_Forme34, NUP_Forme35, NUP_Forme36, NUP_Forme37, NUP_Forme38, NUP_Forme39, NUP_Forme40,
            ],
            [NUP_Forme41, NUP_Forme42, NUP_Forme43, NUP_Forme44, NUP_Forme45, NUP_Forme46, NUP_Forme47, NUP_Forme48, NUP_Forme49, NUP_Forme50,
            ],
            [NUP_Forme51, NUP_Forme52, NUP_Forme53, NUP_Forme54, NUP_Forme55, NUP_Forme56, NUP_Forme57, NUP_Forme58, NUP_Forme59, NUP_Forme60,
            ],
            [NUP_Forme61, NUP_Forme62, NUP_Forme63, NUP_Forme64, NUP_Forme65, NUP_Forme66, NUP_Forme67, NUP_Forme68, NUP_Forme69, NUP_Forme70,
            ],
            [NUP_Forme71, NUP_Forme72, NUP_Forme73, NUP_Forme74, NUP_Forme75, NUP_Forme76, NUP_Forme77, NUP_Forme78, NUP_Forme79, NUP_Forme80,
            ],
            new [] { NUP_WeatherForme1, NUP_WeatherForme2, NUP_WeatherForme3, NUP_WeatherForme4, NUP_WeatherForme5, NUP_WeatherForme6 },
        };

        foreach (var nup_l in list)
        {
            foreach (var nup in nup_l)
                nup.ValueChanged += UpdateSpeciesForm;
        }

        return list;
    }

    private readonly Area7[] Areas;
    private readonly LazyGARCFile encdata;
    private readonly string[] speciesList = Main.Config.GetText(TextName.SpeciesNames);
    private readonly Font font;
    private readonly NumericUpDown[][] nup_spec;
    private readonly ComboBox[][] cb_spec;
    private readonly NumericUpDown[] rate_spec;

    private int TotalEncounterRate => rate_spec.Sum(nup => (int)nup.Value);
    private bool loadingdata;
    private EncounterTable CurrentTable;
    private int lastLoc = -1;
    private int lastTable = -1;
    private readonly string[] locationlist;
    private readonly byte[][] infiles;
    private byte[][] evolutionFiles;

    private void AutoSave()
    {
        if (lastLoc == -1 || lastTable == -1 || CurrentTable == null) return;
        
        var area = Areas[lastLoc];
        if (!area.HasTables || lastTable >= area.Tables.Count) return;

        CurrentTable.Write();
        area.Tables[lastTable] = CurrentTable;

        // Set data back to memory immediately
        encdata[area.FileNumber] = Area7.GetDayNightTableBinary(area.Tables);
    }
    private void LoadData()
    {
        loadingdata = true;
        evolutionFiles = Main.Config.GetGARCData("evolution").Files;

        CB_LocationID.Items.Clear();
        CB_LocationID.Items.AddRange(Areas.Select(a => a.Name).ToArray());
        CB_LocationID.SelectedIndex = 0;

        vanillaFiles = infiles.Select(z => (byte[])z.Clone()).ToArray();
        UpdateEncounterChangelog();

        CB_SlotRand.SelectedIndex = 0;
        CB_LocationID.SelectedIndex = 0;

        loadingdata = false;
    }

    private byte[][] files;
    private byte[][] vanillaFiles;

    private void ChangeMap(object sender, EventArgs e)
    {
        if (loadingdata) return;
        loadingdata = true;
        CB_TableID.Items.Clear();
        var Map = Areas[CB_LocationID.SelectedIndex];
        for (int i = 0; i < Map.Tables.Count; i++)
            CB_TableID.Items.Add($"Table {i}");
        
        if (CB_TableID.Items.Count > 0) CB_TableID.SelectedIndex = 0;
        loadingdata = false;
        UpdatePanel(sender, e);
    }

    private void UpdateEncounterChangelog()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Encounter Changes ===");
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].SequenceEqual(vanillaFiles[i])) continue;
            sb.AppendLine($"\n[{Areas[i].Name}]");
            sb.AppendLine("Data modified from vanilla.");
        }
        // RTB_EncounterChangelog.Text = sb.ToString();
    }

    private void B_Exclusives_Click(object sender, EventArgs e)
    {
        Form f = new Form { Text = "Ultra Alola Field Guide - Version Exclusives & Rarities", Width = 800, Height = 700, StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(30, 30, 40) };
        var mainScroll = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
        
        int GetID(string name) => Array.FindIndex(speciesList, s => s.Equals(name, StringComparison.OrdinalIgnoreCase));

        void AddText(string text, Color color, bool bold = false, int size = 10)
        {
            var l = new Label { Text = text, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), AutoSize = true, Margin = new Padding(0, 5, 0, 5), MaximumSize = new Size(740, 0) };
            mainScroll.Controls.Add(l);
        }

        void AddIcons(string title, (string Name, int Form)[] species)
        {
            if (!string.IsNullOrEmpty(title)) AddText(title, Color.Gold, true, 11);
            var flow = new FlowLayoutPanel { Width = 740, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Margin = new Padding(0, 0, 0, 15) };
            foreach (var s in species)
            {
                int id = GetID(s.Name);
                if (id <= 0) continue;
                var pb = new PictureBox { Size = new Size(40, 30), SizeMode = PictureBoxSizeMode.CenterImage, Cursor = Cursors.Help };
                pb.Image = WinFormsUtil.GetSprite(id, s.Form, 0, 0, Main.Config);
                new ToolTip().SetToolTip(pb, $"{s.Name}{(s.Form > 0 ? $" (Form {s.Form})" : "")}");
                flow.Controls.Add(pb);
            }
            mainScroll.Controls.Add(flow);
        }

        AddText("1. Version Exclusive Pokémon", Color.Cyan, true, 14);
        AddText("These Pokémon are obtainable through normal gameplay in one version but require trading to acquire in the other.", Color.WhiteSmoke);

        // Ultra Sun
        AddText("--- Exclusive to Ultra Sun ---", Color.OrangeRed, true, 12);
        AddIcons("Standard Pokémon", new[] { 
            ("Vulpix", 1), ("Ninetales", 1), ("Houndour", 0), ("Houndoom", 0), ("Cranidos", 0), ("Rampardos", 0), 
            ("Cottonee", 0), ("Whimsicott", 0), ("Tirtouga", 0), ("Carracosta", 0), ("Rufflet", 0), ("Braviary", 0), 
            ("Golett", 0), ("Golurk", 0), ("Clauncher", 0), ("Clawitzer", 0), ("Tyrunt", 0), ("Tyrantrum", 0), 
            ("Passimian", 0), ("Turtonator", 0), ("Omanyte", 0), ("Omastar", 0), ("Anorith", 0), ("Armaldo", 0), ("Basculin", 0) 
        });
        AddIcons("Ultra Beasts", new[] { ("Buzzwole", 0), ("Kartana", 0), ("Blacephalon", 0) });
        AddIcons("Legendary Pokémon", new[] { 
            ("Raikou", 0), ("Ho-Oh", 0), ("Latios", 0), ("Groudon", 0), ("Dialga", 0), ("Heatran", 0), ("Reshiram", 0), ("Tornadus", 0), ("Xerneas", 0), ("Necrozma", 1) 
        });
        AddIcons("Totem-Sized Forms (In-Game Gifts)", new[] { 
            ("Gumshoos", 1), ("Marowak", 2), ("Lurantis", 1), ("Vikavolt", 1), ("Ribombee", 1) 
        });

        // Ultra Moon
        AddText("--- Exclusive to Ultra Moon ---", Color.DodgerBlue, true, 12);
        AddIcons("Standard Pokémon", new[] { 
            ("Sandshrew", 1), ("Sandslash", 1), ("Electrike", 0), ("Manectric", 0), ("Shieldon", 0), ("Bastiodon", 0), 
            ("Petilil", 0), ("Lilligant", 0), ("Archen", 0), ("Archeops", 0), ("Vullaby", 0), ("Mandibuzz", 0), 
            ("Baltoy", 0), ("Claydol", 0), ("Skrelp", 0), ("Dragalge", 0), ("Amaura", 0), ("Aurorus", 0), 
            ("Oranguru", 0), ("Drampa", 0), ("Kabuto", 0), ("Kabutops", 0), ("Lileep", 0), ("Cradily", 0), ("Basculin", 1) 
        });
        AddIcons("Ultra Beasts", new[] { ("Pheromosa", 0), ("Celesteela", 0), ("Stakataka", 0) });
        AddIcons("Legendary Pokémon", new[] { 
            ("Entei", 0), ("Lugia", 0), ("Latias", 0), ("Kyogre", 0), ("Palkia", 0), ("Regigigas", 0), ("Zekrom", 0), ("Thundurus", 0), ("Yveltal", 0), ("Necrozma", 2) 
        });
        AddIcons("Totem-Sized Forms (In-Game Gifts)", new[] { 
            ("Raticate", 1), ("Araquanid", 1), ("Salazzle", 1), ("Togedemaru", 1), ("Kommo-o", 1) 
        });

        AddIcons("Dual-Dependency Legendaries (Requires both mascots in party)", new[] { 
            ("Suicune", 0), ("Rayquaza", 0), ("Giratina", 0), ("Kyurem", 0), ("Landorus", 0) 
        });

        AddText("2. Event-Only Pokémon (Mythicals and Special Forms)", Color.Cyan, true, 14);
        AddIcons("", new[] { 
            ("Mew", 0), ("Celebi", 0), ("Jirachi", 0), ("Deoxys", 0), ("Phione", 0), ("Manaphy", 0), ("Darkrai", 0), ("Shaymin", 0), ("Arceus", 0), 
            ("Victini", 0), ("Keldeo", 0), ("Meloetta", 0), ("Genesect", 0), ("Diancie", 0), ("Hoopa", 0), ("Volcanion", 0), 
            ("Vivillon", 12), ("Vivillon", 18), ("Floette", 5), ("Magearna", 0), ("Marshadow", 0), ("Zeraora", 0), ("Greninja", 1), 
            ("Pikachu", 8), ("Pikachu", 9), ("Pikachu", 10), ("Pikachu", 11), ("Pikachu", 12), ("Pikachu", 13) 
        });

        AddText("3. Not Encounterable In-Game (Transfer Only / Missing from Alola Dex)", Color.Cyan, true, 14);
        
        void AddFamily(string gen, string[] members) => AddIcons(gen, members.Select(m => (m, 0)).ToArray());

        AddFamily("Generation 1", new[] { "Nidoran♀", "Nidorina", "Nidoqueen", "Nidoran♂", "Nidorino", "Nidoking", "Oddish", "Gloom", "Vileplume", "Bellossom", "Venonat", "Venomoth", "Ponyta", "Rapidash", "Farfetch'd", "Doduo", "Dodrio", "Krabby", "Kingler", "Voltorb", "Electrode", "Lickitung", "Lickilicky", "Koffing", "Weezing", "Tangela", "Tangrowth", "Tyrogue", "Hitmonlee", "Hitmonchan", "Hitmontop" });
        AddFamily("Generation 2", new[] { "Sentret", "Furret", "Hoppip", "Skiploom", "Jumpluff", "Sunkern", "Sunflora", "Unown", "Wynaut", "Wobbuffet", "Girafarig", "Dunsparce", "Gligar", "Gliscor", "Qwilfish", "Shuckle", "Teddiursa", "Ursaring", "Phanpy", "Donphan", "Stantler" });
        AddFamily("Generation 3", new[] { "Poochyena", "Mightyena", "Zigzagoon", "Linoone", "Wurmple", "Silcoon", "Beautifly", "Cascoon", "Dustox", "Taillow", "Swellow", "Shroomish", "Breloom", "Nincada", "Ninjask", "Shedinja", "Whismur", "Loudred", "Exploud", "Skitty", "Delcatty", "Plusle", "Minun", "Volbeat", "Illumise", "Gulpin", "Swalot", "Numel", "Camerupt", "Cacnea", "Cacturne", "Zangoose", "Seviper", "Lunatone", "Solrock", "Chingling", "Chimecho" });
        AddFamily("Generation 4", new[] { "Bidoof", "Bibarel", "Kricketot", "Kricketune", "Shinx", "Luxio", "Luxray", "Burmy", "Wormadam", "Mothim", "Combee", "Vespiquen", "Pachirisu", "Cherubi", "Cherrim", "Glameow", "Purugly", "Stunky", "Skuntank", "Bronzor", "Bronzong", "Chatot", "Spiritomb", "Croagunk", "Toxicroak", "Carnivine" });
        AddFamily("Generation 5", new[] { "Patrat", "Watchog", "Purrloin", "Liepard", "Pansage", "Simisage", "Pansear", "Simisear", "Panpour", "Simipour", "Munna", "Musharna", "Pidove", "Tranquill", "Unfezant", "Blitzle", "Zebstrika", "Woobat", "Swoobat", "Drilbur", "Excadrill", "Timburr", "Gurdurr", "Conkeldurr", "Tympole", "Palpitoad", "Seismitoad", "Throh", "Sawk", "Venipede", "Whirlipede", "Scolipede", "Darumaka", "Darmanitan", "Maractus", "Yamask", "Cofagrigus", "Deerling", "Sawsbuck", "Karrablast", "Escavalier", "Foongus", "Amoonguss", "Joltik", "Galvantula", "Ferroseed", "Ferrothorn", "Cubchoo", "Beartic", "Cryogonal", "Shelmet", "Accelgor", "Bouffalant", "Heatmor", "Durant" });
        AddFamily("Generation 6", new[] { "Bunnelby", "Diggersby", "Skiddo", "Gogoat", "Espurr", "Meowstic", "Spritzee", "Aromatisse", "Swirlix", "Slurpuff", "Pumpkaboo", "Gourgeist", "Bergmite", "Avalugg" });
        string[] speciesNames = Main.Config.GetText(TextName.SpeciesNames);
        if (Main.Config.MaxSpeciesID >= 905)
            AddFamily("Generation 8", Enumerable.Range(808, 905 - 808 + 1).Where(i => i < speciesNames.Length).Select(i => speciesNames[i]).ToArray());
        if (Main.Config.MaxSpeciesID >= 1025)
            AddFamily("Generation 9", Enumerable.Range(906, 1025 - 906 + 1).Where(i => i < speciesNames.Length).Select(i => speciesNames[i]).ToArray());

        f.Controls.Add(mainScroll);
        f.ShowDialog();
    }

    private Dictionary<int, List<int>> GetEvolutionFamilies()
    {
        int max = Main.Config.MaxSpeciesID;
        var adj = new Dictionary<int, List<int>>();
        for (int i = 1; i <= max; i++)
        {
            if (!adj.ContainsKey(i)) adj[i] = new List<int>();
            var evo = new EvolutionSet7(evolutionFiles[i]);
            foreach (var e in evo.PossibleEvolutions.Where(e => e.Species > 0 && e.Species <= max))
            {
                if (!adj.ContainsKey(e.Species)) adj[e.Species] = new List<int>();
                if (!adj[i].Contains(e.Species)) adj[i].Add(e.Species);
                if (!adj[e.Species].Contains(i)) adj[e.Species].Add(i);
            }
        }

        var res = new Dictionary<int, List<int>>();
        var visited = new HashSet<int>();
        for (int i = 1; i <= max; i++)
        {
            if (visited.Contains(i)) continue;
            var family = new List<int>();
            var q = new Queue<int>();
            q.Enqueue(i);
            visited.Add(i);
            while (q.Count > 0)
            {
                int curr = q.Dequeue();
                family.Add(curr);
                foreach (int next in adj[curr])
                {
                    if (visited.Add(next)) q.Enqueue(next);
                }
            }
            foreach (int f in family) res[f] = family;
        }
        return res;
    }

    private HashSet<int> GetCurrentlyInGameSpecies()
    {
        var set = new HashSet<int>();
        foreach (var area in Areas)
        {
            if (!area.HasTables) continue;
            foreach (var table in area.Tables)
            {
                foreach (var g in table.Encounter7s)
                    foreach (var s in g) if (s.Species > 0) set.Add((int)s.Species);
                foreach (var s in table.AdditionalSOS)
                    if (s.Species > 0) set.Add((int)s.Species);
            }
        }
        return set;
    }

    private HashSet<int> GetAlolaDexSpecies()
    {
        // Defined Alola Dex for USUM (403 Entries)
        // This is a representative subset for filtering; usually IDs 722-807 + Alolan Forms + specific Gen1-6 connections
        var dex = new HashSet<int> { 
            722,723,724, 725,726,727, 728,729,730, 731,732,733, 734,735, 19,20, 736,737,738, 172,25,26, 739,740, 741, 174,39,40, 41,42,169, 742,743, 744,745, 133,134,135,136,196,197,470,471,700, 447,448, 12,142, 746, 118,119, 129,130, 747,748, 749,750, 751,752, 753,754, 755,756, 757,758, 759,760, 761,762,763, 764, 765,766, 767,768, 769,770, 771, 772,773, 774, 775, 776, 777, 778, 779, 780, 781, 782,783,784, 785,786,787,788, 789,790,791,792, 793,794,795,796,797,798,799, 800, 801, 802, 803, 804, 805, 806, 807
        };
        // Adding Gen 1-6 families common in Alola
        dex.UnionWith([
            1,2,3, 4,5,6, 7,8,9, 10,11,12, 13,14,15, 21,22, 23,24, 27,28, 29,30,31, 32,33,34, 35,36, 37,38, 39,40, 41,42,169, 43,44,45,182, 46,47, 48,49, 50,51, 52,53, 54,55, 56,57, 58,59, 60,61,62,186, 63,64,65, 66,67,68, 69,70,71, 72,73, 74,75,76, 77,78, 79,80,199, 81,82,462, 83, 84,85, 86,87, 88,89, 90,91, 92,93,94, 95,208, 96,97, 98,99, 100,101, 102,103, 104,105, 106,107,237, 108,463, 109,110, 111,112,464, 113,242, 114,465, 115, 116,117,230, 120,121, 122,439, 123,212, 124,238, 125,239,466, 126,240,467, 127, 128, 131, 132, 137,233,474, 138,139, 140,141, 143,446, 147,148,149, 150
        ]);
        return dex;
    }

private void UpdatePanel(object sender, EventArgs e)
    {
        if (loadingdata) return;
        
        AutoSave(); // Save previous table
        loadingdata = true;

        lastLoc = CB_LocationID.SelectedIndex;
        lastTable = CB_TableID.SelectedIndex;

        if (lastLoc < 0 || lastTable < 0)
        {
            loadingdata = false;
            return;
        }

        var Map = Areas[lastLoc];
        GB_Encounters.Enabled = Map.HasTables;
        if (!Map.HasTables || lastTable >= Map.Tables.Count)
        {
            CurrentTable = null;
            loadingdata = false;
            return;
        }
        
        CurrentTable = new EncounterTable(Map.Tables[lastTable].Data);
        LoadTable(CurrentTable);

        loadingdata = false;
        RefreshTableImages(Map);
    }

    private void RefreshTableImages(Area7 Map)
    {
        int base_id = CB_TableID.SelectedIndex / 2;
        base_id *= 2;
        PB_DayTable.Image = Map.Tables[base_id].GetTableImg(font);
        PB_NightTable.Image = Map.Tables[base_id + 1].GetTableImg(font);
    }

    private void LoadTable(EncounterTable table)
    {
        NUP_Min.Value = table.MinLevel;
        NUP_Max.Minimum = table.MinLevel;
        NUP_Max.Value = table.MaxLevel;
        for (int slot = 0; slot < table.Encounter7s.Length; slot++)
        {
            for (int i = 0; i < table.Encounter7s[slot].Length; i++)
            {
                var sl = table.Encounter7s[slot];
                if (slot == 8)
                    sl = table.AdditionalSOS;
                rate_spec[i].Value = table.Rates[i];
                cb_spec[slot][i].SelectedIndex = (int)sl[i].Species;
                nup_spec[slot][i].Value = (int)sl[i].Forme;
            }
        }
        UpdateMascot();
    }

    private void UpdateMinMax(object sender, EventArgs e)
    {
        if (loadingdata)
            return;
        loadingdata = true;
        int min = (int)NUP_Min.Value;
        int max = (int)NUP_Max.Value;
        if (max < min)
        {
            max = min;
            NUP_Max.Value = max;
            NUP_Max.Minimum = min;
        }
        CurrentTable.MinLevel = min;
        CurrentTable.MaxLevel = max;
        loadingdata = false;
    }

    private void UpdateSpeciesForm(object sender, EventArgs e)
    {
        if (loadingdata)
            return;

        var cur_pb = CB_TableID.SelectedIndex % 2 == 0 ? PB_DayTable : PB_NightTable;
        var cur_img = cur_pb.Image;

        var source = sender is NumericUpDown ? (object[][])nup_spec : cb_spec;
        int table = Array.FindIndex(source, t => t.Contains(sender));
        int slot = Array.IndexOf(source[table], sender);
        
        UpdateMascot(cb_spec[table][slot].SelectedIndex, (int)nup_spec[table][slot].Value);

        var cb_l = cb_spec[table];
        var nup_l = nup_spec[table];
        var species = (uint)cb_l[slot].SelectedIndex;
        var form = (uint)nup_l[slot].Value;
        if (table == 8)
        {
            CurrentTable.AdditionalSOS[slot].Species = species;
            CurrentTable.AdditionalSOS[slot].Forme = form;
        }
        CurrentTable.Encounter7s[table][slot].Species = species;
        CurrentTable.Encounter7s[table][slot].Forme = form;

        using (var g = Graphics.FromImage(cur_img))
        {
            int x = 40 * slot;
            int y = 30 * (table + 1);
            if (table == 8)
            {
                x = (40 * slot) + 60;
                y = 270;
            }
            var pnt = new Point(x, y);
            g.SetClip(new Rectangle(pnt.X, pnt.Y, 40, 30), CombineMode.Replace);
            g.Clear(Color.Transparent);

            var enc = CurrentTable.Encounter7s[table][slot];
            g.DrawImage(enc.Species == 0 ? Properties.Resources.empty : WinFormsUtil.GetSprite((int)enc.Species, (int)enc.Forme, 0, 0, Main.Config), pnt);
        }

        cur_pb.Image = cur_img;
    }

    private void UpdateEncounterRate(object sender, EventArgs e)
    {
        if (loadingdata)
            return;

        var cur_pb = CB_TableID.SelectedIndex % 2 == 0 ? PB_DayTable : PB_NightTable;
        var cur_img = cur_pb.Image;

        int slot = Array.IndexOf(rate_spec, sender);
        int rate = (int)((NumericUpDown)sender).Value;
        CurrentTable.Rates[slot] = rate;

        var textColor = WinFormsUtil.IsCyberSlate ? Brushes.WhiteSmoke : Brushes.White;
        using (var g = Graphics.FromImage(cur_img))
        {
            var pnt = new PointF((40 * slot) + 10, 10);
            g.SetClip(new Rectangle((int)pnt.X, (int)pnt.Y, 40, 14), CombineMode.Replace);
            g.Clear(Color.Transparent);
            g.DrawString($"{rate}%", font, textColor, pnt);
        }

        cur_pb.Image = cur_img;

        var sum = TotalEncounterRate;
        GB_Encounters.Text = $"Encounters ({sum}%)";
    }

    private byte[] CopyTable;
    private int CopyCount;

    private void B_Copy_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables)
        {
            WinFormsUtil.Alert("No tables to copy.");
            return;
        }
        CurrentTable.Write();
        CopyTable = (byte[])CurrentTable.Data.Clone();
        CopyCount = CurrentTable.Encounter7s[0].Count(z => z.Species != 0);
        B_Paste.Enabled = B_PasteAll.Enabled = true;
        WinFormsUtil.Alert("Copied table data.");
    }

private void B_Paste_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables || CopyTable == null) return;
        CurrentTable.Reset(CopyTable);
        loadingdata = true;
        LoadTable(CurrentTable);
        var area = Areas[CB_LocationID.SelectedIndex];
        area.Tables[CB_TableID.SelectedIndex] = CurrentTable;
        loadingdata = false;
        RefreshTableImages(Map);
        System.Media.SystemSounds.Asterisk.Play();
        AutoSave(); 
    }

private void B_PasteAll_Click(object sender, EventArgs e)
    {
        var Map = Areas[CB_LocationID.SelectedIndex];
        if (!Map.HasTables || CopyTable == null) return;
        AutoSave();
        B_Paste_Click(sender, e);
        foreach (var t in Map.Tables.Where(t => CopyCount == t.Encounter7s[0].Count(z => z.Species != 0)))
        {
            t.Reset(CopyTable);
            t.Write();
        }
        encdata[Map.FileNumber] = Area7.GetDayNightTableBinary(Map.Tables);
        lastLoc = -1; 
        UpdatePanel(sender, e);
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        AutoSave();
        WinFormsUtil.Alert("Current table auto-saved to memory.");
    }

    private void B_Export_Click(object sender, EventArgs e)
    {
        B_Save_Click(sender, e);

        Directory.CreateDirectory("encdata");
        foreach (var Map in Areas)
        {
            var packed = Area7.GetDayNightTableBinary(Map.Tables);
            File.WriteAllBytes(Path.Combine("encdata", Map.FileNumber.ToString()), packed);
        }
        WinFormsUtil.Alert("Exported all tables!");
    }

    private void B_Import_Click(object sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() != DialogResult.OK) return;

        string folder = fbd.SelectedPath;
        string[] files = Directory.GetFiles(folder);

        int count = 0;
        foreach (string file in files)
        {
            if (!int.TryParse(Path.GetFileName(file), out int fileNum)) continue;

            var area = Areas.FirstOrDefault(a => a.FileNumber == fileNum);
            if (area == null) continue;

            byte[] data = File.ReadAllBytes(file);
            encdata[fileNum] = data;

            area.Tables.Clear();
            byte[][] tables = Mini.UnpackMini(data, "EA");
            foreach (var t in tables)
            {
                if (t.Length < 0x168) continue;
                area.Tables.Add(new EncounterTable(t.Skip(4).Take(0x164).ToArray()));
                area.Tables.Add(new EncounterTable(t.Skip(0x168).ToArray()));
            }
            area.HasTables = area.Tables.Any(t => t.Data.Length > 0);
            count++;
        }

        if (count > 0)
        {
            lastLoc = -1;
            UpdatePanel(null, null);
            WinFormsUtil.Alert($"Imported {count} tables!");
        }
    }

    private void DumpTables(object sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { FileName = "EncounterTables.txt" };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        var sb = new StringBuilder();
        foreach (var Map in Areas)
            sb.Append(Map.GetSummary(speciesList));
        File.WriteAllText(sfd.FileName, sb.ToString());
    }

    // Randomization & Bulk Modification
    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all? Cannot undo.", "Double check Randomization settings at the bottom left.")) return;
        Enabled = false;
        AutoSave();
        ExecuteRandomization();
        lastLoc = -1; // Block UI overwrite
        UpdatePanel(null, null);
        Enabled = true;
        WinFormsUtil.Alert("Randomized all Wild Encounters according to specification!", "Press the Dump Tables button to view the new Wild Encounter information!");
    }

    private void ExecuteRandomization()
    {
        var rnd = new SpeciesRandomizer(Main.Config)
        {
            G1 = CHK_G1.Checked,
            G2 = CHK_G2.Checked,
            G3 = CHK_G3.Checked,
            G4 = CHK_G4.Checked,
            G5 = CHK_G5.Checked,
            G6 = CHK_G6.Checked,
            G7 = CHK_G7.Checked,
            G8 = CHK_G8.Checked,
            G9 = CHK_G9.Checked,

            E = CHK_E.Checked,
            L = CHK_L.Checked,
            rBST = CHK_BST.Checked,
        };
        rnd.Initialize();
        var form = new FormRandomizer(Main.Config)
        {
            AllowMega = CHK_MegaForm.Checked,
            AllowAlolanForm = true,
        };
        var wild7 = new Wild7Randomizer
        {
            RandSpec = rnd,
            RandForm = form,
            TableRandomizationOption = CB_SlotRand.SelectedIndex,
            LevelAmplifier = NUD_LevelAmp.Value,
            ModifyLevel = CHK_Level.Checked,
        };
        wild7.Execute(Areas, encdata);
    }

private void CopySOS_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Copy initial species to SOS slots?", "Cannot undo.") != DialogResult.Yes) return;
        for (int i = 1; i < nup_spec.Length - 1; i++)
        {
            for (int s = 0; s < nup_spec[i].Length; s++) 
            {
                nup_spec[i][s].Value = nup_spec[0][s].Value;
                cb_spec[i][s].SelectedIndex = cb_spec[0][s].SelectedIndex;
            }
        }
        AutoSave();
        WinFormsUtil.Alert("All initial species copied to SOS slots!");
    }

    private void ModifyAllLevelRanges(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Modify all current Level ranges?", "Cannot undo.")) return;
        Enabled = false;
        AutoSave();
        var amp = NUD_LevelAmp.Value;
        foreach (var area in Areas)
        {
            if (!area.HasTables) continue;
            var tables = area.Tables;
            foreach (var table in tables)
            {
                table.MinLevel = Randomizer.GetModifiedLevel(table.MinLevel, amp);
                table.MaxLevel = Randomizer.GetModifiedLevel(table.MaxLevel, amp);
                table.Write();
            }
            encdata[area.FileNumber] = Area7.GetDayNightTableBinary(tables);
        }
        Enabled = true;
        WinFormsUtil.Alert("Modified all Level ranges according to specification!");
        lastLoc = -1;
        UpdatePanel(sender, e);
    }

    // Utility
    /// <summary>
    /// Moves the sub-location names into the location name string entry.
    /// </summary>
    /// <param name="list">Raw location list</param>
    /// <returns>Cleaned location list</returns>
    public static string[] GetGoodLocationList(string[] list)
    {
        var bad = list;
        var good = (string[])bad.Clone();
        for (int i = 0; i < bad.Length; i += 2)
        {
            var nextLoc = bad[i + 1];
            if (!string.IsNullOrWhiteSpace(nextLoc) && nextLoc[0] != '[')
                good[i] += $" ({nextLoc})";
            if (i > 0 && !string.IsNullOrWhiteSpace(good[i]) && good.Take(i - 1).Contains(good[i]))
                good[i] += $" ({good.Take(i - 1).Count(s => s == good[i]) + 1})";
        }
        return good;
    }

    public void ExportEncounters(string gameID, string ident, bool sm)
    {
        var reg = Gen7SlotDumper.GetRegularBinary(Areas, sm);
        var sos = Gen7SlotDumper.GetSOSBinary(Areas, Main.Config.Personal, sm);

        File.WriteAllBytes($"encounter_{gameID}.pkl", Mini.PackMini(reg, ident));
        File.WriteAllBytes($"encounter_{gameID}_sos.pkl", Mini.PackMini(sos, ident));
    }
    private void B_ExportTSV_Click(object sender, EventArgs e)
    {
        AutoSave();
        var lines = new List<string> { "LocIndex\tLocation\tTableIndex\tTableGroup\tSlotIndex\tMinLvl\tMaxLvl\tRate\tSpecies\tForm" };

        for (int a = 0; a < Areas.Length; a++)
        {
            var Map = Areas[a];
            if (!Map.HasTables) continue;
            string locName = CB_LocationID.Items[a].ToString();
            
            for (int t = 0; t < Map.Tables.Count; t++)
            {
                var table = Map.Tables[t];
                int min = table.MinLevel;
                int max = table.MaxLevel;
                
                for (int g = 0; g < 8; g++)
                {
                    for (int s = 0; s < 10; s++)
                    {
                        var slot = table.Encounter7s[g][s];
                        int rate = table.Rates[s];
                        lines.Add($"{a}\t{locName}\t{t}\t{g}\t{s}\t{min}\t{max}\t{rate}\t{speciesList[slot.Species]}\t{slot.Forme}");
                    }
                }
                for (int s = 0; s < 6; s++)
                {
                    var slot = table.AdditionalSOS[s];
                    lines.Add($"{a}\t{locName}\t{t}\t8\t{s}\t{min}\t{max}\t0\t{speciesList[slot.Species]}\t{slot.Forme}");
                }
            }
        }

        using var sfd = new SaveFileDialog { FileName = "EncounterTables_TSV.txt", Filter = "Text File|*.txt" };
        System.Media.SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK)
            File.WriteAllLines(sfd.FileName, lines, Encoding.Unicode);
    }

    private void B_ImportTSV_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        AutoSave();

        string[] lines = File.ReadAllLines(ofd.FileName);
        int count = 0;

        foreach (string line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 10) continue;
            
            if (!int.TryParse(parts[0], out int locIdx) || locIdx < 0 || locIdx >= Areas.Length) continue;
            if (!int.TryParse(parts[2], out int tableIdx)) continue;
            if (!int.TryParse(parts[3], out int groupIdx)) continue;
            if (!int.TryParse(parts[4], out int slotIdx)) continue;
            
            var Map = Areas[locIdx];
            if (!Map.HasTables || tableIdx >= Map.Tables.Count) continue;
            var table = Map.Tables[tableIdx];
            
            int.TryParse(parts[5], out int min);
            int.TryParse(parts[6], out int max);
            int.TryParse(parts[7], out int rate);
            
            string specName = parts[8].Trim();
            int specIdx = Array.IndexOf(speciesList, specName);
            if (specIdx < 0) specIdx = 0; 
            
            int.TryParse(parts[9], out int form);
            
            table.MinLevel = min;
            table.MaxLevel = max;
            
            if (groupIdx < 8 && slotIdx < 10)
            {
                table.Rates[slotIdx] = rate;
                table.Encounter7s[groupIdx][slotIdx].Species = (uint)specIdx;
                table.Encounter7s[groupIdx][slotIdx].Forme = (uint)form;
            }
            else if (groupIdx == 8 && slotIdx < 6)
            {
                table.AdditionalSOS[slotIdx].Species = (uint)specIdx;
                table.AdditionalSOS[slotIdx].Forme = (uint)form;
            }
            
            count++;
        }
        
        foreach (var area in Areas)
        {
            if (area.HasTables)
            {
                foreach (var table in area.Tables) table.Write();
                encdata[area.FileNumber] = Area7.GetDayNightTableBinary(area.Tables);
            }
        }
        
        WinFormsUtil.Alert($"Imported {count} encounter slots successfully!");
        lastLoc = -1; // Force reload UI cleanly
        UpdatePanel(sender, e);
    }

    private void SMWE_FormClosing(object sender, FormClosingEventArgs e)
    {
        AutoSave();
        RandSettings.SetFormSettings(this, GB_Tweak.Controls);
    }

    private void UpdateMascot(int species = -1, int form = 0)
    {
        if (species == -1)
        {
            species = (int)cb_spec[0][0].SelectedIndex;
            form = (int)nup_spec[0][0].Value;
        }
        PB_Mascot.Image = WinFormsUtil.GetSprite(species, form, 0, 0, Main.Config);
    }
}

public static class Extensions
{
    public static Bitmap GetTableImg(this EncounterTable table, Font font)
    {
        var img = new Bitmap(10 * 40, 10 * 30);
        using var g = Graphics.FromImage(img);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        var brush = WinFormsUtil.IsCyberSlate ? Brushes.White : Brushes.Black;
        for (int i = 0; i < table.Rates.Length; i++)
            g.DrawString($"{table.Rates[i]}%", font, brush, new PointF((40 * i) + 10, 10));
        g.DrawString("Weather: ", font, brush, new PointF(10, 280));

        // Draw Sprites
        for (int i = 0; i < table.Encounter7s.Length - 1; i++)
        {
            for (int j = 0; j < table.Encounter7s[i].Length; j++)
            {
                var slot = table.Encounter7s[i][j];
                var sprite = GetSprite((int)slot.Species, (int)slot.Forme);
                g.DrawImage(sprite, new Point(40 * j, 30 * (i + 1)));
            }
        }

        for (int i = 0; i < table.AdditionalSOS.Length; i++)
        {
            var slot = table.AdditionalSOS[i];
            var sprite = GetSprite((int)slot.Species, (int)slot.Forme);
            g.DrawImage(sprite, new Point((40 * i) + 60, 270));
        }

        static Bitmap GetSprite(int species, int form)
        {
            return species == 0
                ? Properties.Resources.empty
                : WinFormsUtil.GetSprite(species, form, 0, 0, Main.Config);
        }

        return img;
    }

}

