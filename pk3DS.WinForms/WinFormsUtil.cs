using pk3DS.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pk3DS.WinForms.Properties;

namespace pk3DS.WinForms;

public static class WinFormsUtil
{
    public enum VisualTheme { Dark, Grey, Light, GalaxyPurple }
    public static VisualTheme CurrentTheme { get; set; } = VisualTheme.Dark;
    public static bool IsCyberSlate => CurrentTheme == VisualTheme.Dark;
    public static bool ShowExtendedLogic { get; set; } = false;
    public static void ReportSave() => Main.Instance?.HandleFriendship(3);
    // Image Layering/Blending Utility
    public static Bitmap LayerImage(Image baseLayer, Image overLayer, int x, int y, double trans)
    {
        if (baseLayer == null)
            return overLayer as Bitmap;
        var img = new Bitmap(baseLayer.Width, baseLayer.Height);
        using var gr = Graphics.FromImage(img);
        gr.DrawImage(baseLayer, new Point(0, 0));
        var o = ChangeOpacity(overLayer, trans);
        gr.DrawImage(o, new Rectangle(x, y, overLayer.Width, overLayer.Height));
        return img;
    }

    public static Bitmap ChangeOpacity(Image img, double trans)
    {
        if (img == null)
            return null;
        if (img.PixelFormat.HasFlag(PixelFormat.Indexed))
            return (Bitmap)img;

        var bmp = (Bitmap)img.Clone();
        var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var ptr = bmpData.Scan0;

        int len = bmp.Width * bmp.Height * 4;
        byte[] data = new byte[len];

        Marshal.Copy(ptr, data, 0, len);

        for (int i = 0; i < data.Length; i += 4)
            data[i + 3] = (byte)(data[i + 3] * trans);

        Marshal.Copy(data, 0, ptr, len);
        bmp.UnlockBits(bmpData);

        return bmp;
    }

    private static ReadOnlySpan<int> GenderedSpecies => [592, 593, 521, 668];
    private static ReadOnlySpan<int> DefaultSprites => [778, 664, 665, 414, 493, 773];

    private static string _extractedSprites;
    private static bool _extractedSpritesProbed;

    /// <summary>
    /// Folder holding the extracted expanded-form sprites, or null when none is installed.
    /// </summary>
    public static string ExtractedSpritesFolder
    {
        get
        {
            if (_extractedSpritesProbed) return _extractedSprites;
            _extractedSpritesProbed = true;

            string profile = "";
            try { profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch { }

            var candidates = new List<string>
            {
                Path.Combine(Application.StartupPath, "extracted_sprites"),
                Path.Combine(Application.StartupPath, "Resources", "extracted_sprites"),
                Path.Combine(Directory.GetCurrentDirectory(), "extracted_sprites"),
            };
            if (!string.IsNullOrEmpty(profile))
            {
                candidates.Add(Path.Combine(profile, "Downloads", "Sprites", "extracted_sprites"));
                candidates.Add(Path.Combine(profile, "Downloads", "extracted_sprites"));
            }

            foreach (string c in candidates)
            {
                try { if (Directory.Exists(c)) { _extractedSprites = c; break; } } catch { }
            }
            return _extractedSprites;
        }
    }
    public static string GetResourceStringSprite(int species, int form, int gender, int generation)
    {
        if (DefaultSprites.Contains(species)) // Species who show their default sprite regardless of Form
            form = 0;

        string file = "_" + species;
        if (form > 0) // Alt Form Handling
            file += "_" + form;
        else if (gender == 1 && GenderedSpecies.Contains(species)) // Frillish & Jellicent, Unfezant & Pyroar
            file += "_" + gender;

        if (species == 25 && form > 0 && generation >= 7) // Pikachu
            file += "c"; // Cap

        return file;
    }

    public static Bitmap GetSprite(int species, int form, int gender, int item, GameConfig config, bool shiny = false)
    {
        if (species == 0)
            return Resources._0;
        
        // Resilience: Allow expanded species up to 1025 or config limit
        int maxSp = Math.Max(1025, config?.MaxSpeciesID ?? 1025);
        if (species > maxSp)
            return Resources.unknown;

        // Calculate personal entry index for forms (e.g. Mega Venusaur -> 1026 in expansion, 808 in vanilla)
        int formIdx = species;
        if (form > 0 && config?.Personal != null && species < config.Personal.Table.Length)
        {
            try
            {
                int pIdx = config.Personal.GetFormIndex(species, form);
                if (pIdx > 0 && pIdx < config.Personal.Table.Length)
                    formIdx = pIdx;
            }
            catch { }
        }

        var file = GetResourceStringSprite(species, form, gender, config?.Generation ?? 7);

        Bitmap baseImage = null;
        _ = ExtractedSpritesFolder;   // resolve once, before the fallback chain needs it

        // 1. Check CustomSprites directory
        if (baseImage == null)
        {
            List<string> customPaths = new List<string>();
            if (formIdx > species)
                customPaths.Add(Path.Combine(Application.StartupPath, "CustomSprites", $"_{formIdx}.png"));
            customPaths.Add(Path.Combine(Application.StartupPath, "CustomSprites", file + ".png"));

            foreach (var customPath in customPaths)
            {
                if (File.Exists(customPath))
                {
                    try
                    {
                        using (var tempImg = Image.FromFile(customPath))
                        {
                            baseImage = new Bitmap(tempImg);
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        // 2. Check vanilla Gen 7 alternative forms directory (highest vanilla priority)
        if (baseImage == null)
        {
            string spriteDir = Path.Combine(Application.StartupPath, "Resources", "img", "Pokemon Sprites");
            if (Directory.Exists(spriteDir))
            {
                string localName = file.Substring(1).Replace('_', '-') + ".png";
                string localPath = Path.Combine(spriteDir, localName);
                
                // Fallback to 'b' for gender if not found (e.g., 201-11b.png)
                if (!File.Exists(localPath) && localName.EndsWith("-1.png") && GenderedSpecies.Contains(species))
                {
                    localPath = Path.Combine(spriteDir, species.ToString() + "b.png");
                }
                
                // Fallback to base species if alt form not found
                if (!File.Exists(localPath) && form > 0)
                {
                    localPath = Path.Combine(spriteDir, species.ToString() + ".png");
                }

                if (File.Exists(localPath))
                {
                    try
                    {
                        using (var tempImg = Image.FromFile(localPath))
                        {
                            baseImage = new Bitmap(tempImg);
                        }
                    }
                    catch { }
                }
            }
        }

        // 3. Check extracted_sprites directory if available (for modded/expanded forms)
        string extractedFolder = ExtractedSpritesFolder;
        if (baseImage == null && extractedFolder != null)
        {
            int targetIdx = formIdx > 0 ? formIdx : species;
            string sPadded = Path.Combine(extractedFolder, $"sprite_{targetIdx:D5}.png");
            string sDirect = Path.Combine(extractedFolder, $"sprite_{targetIdx}.png");
            string spritePath = File.Exists(sPadded) ? sPadded : (File.Exists(sDirect) ? sDirect : null);

            if (spritePath != null)
            {
                try
                {
                    using (var tempImg = Image.FromFile(spritePath))
                    {
                        baseImage = new Bitmap(tempImg);
                    }
                }
                catch { }
            }
        }

        // 3. Check Resources.ResourceManager.GetObject(file) (e.g. "_722_1" or "_722")
        if (baseImage == null)
        {
            try
            {
                baseImage = (Bitmap)Resources.ResourceManager.GetObject(file);
            }
            catch { }
        }

        // 4. Fallback for Alt Forms in Resources: if "_722_1" wasn't found in Resources, try base "_722"
        if (baseImage == null && form > 0)
        {
            try
            {
                string baseFile = GetResourceStringSprite(species, 0, gender, config?.Generation ?? 7);
                baseImage = (Bitmap)Resources.ResourceManager.GetObject(baseFile);
            }
            catch { }
        }

        // 5. Totem logic fallback
        if (IsTotemForm(species, form))
        {
            form = GetTotemBaseForm(species, form);
            file = GetResourceStringSprite(species, form, gender, config?.Generation ?? 7);
            Bitmap totemImg = (Bitmap)Resources.ResourceManager.GetObject(file);
            if (totemImg != null)
                baseImage = ToGrayscale(totemImg);
        }

        // 6. Final fallback if still null
        if (baseImage == null)
        {
            Bitmap baseSprite = Resources._800 ?? Resources.unknown;
            baseImage = new Bitmap(Math.Max(32, baseSprite.Width), Math.Max(32, baseSprite.Height));
            using (Graphics g = Graphics.FromImage(baseImage))
            {
                g.DrawImage(baseSprite, 0, 0, baseImage.Width, baseImage.Height);
                if (species > 0)
                {
                    using Font font = new Font("Segoe UI", 7, FontStyle.Bold);
                    using Brush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
                    g.FillRectangle(bgBrush, 0, baseImage.Height - 12, baseImage.Width, 12);
                    g.DrawString($"#{species}", font, Brushes.Cyan, 1, baseImage.Height - 12);
                }
            }
        }

        if (shiny)
        {
            // Add shiny star to top left of image.
            baseImage = LayerImage(baseImage, Resources.rare_icon, 0, 0, 0.7);
        }
        if (item > 0)
        {
            Bitmap itemimg = (Bitmap)ItemSpriteCache.Get(ItemName(item, config))
                             ?? (Bitmap)(Resources.ResourceManager.GetObject("item_" + item) ?? Resources.helditem);
            if (itemimg != null && (itemimg.Width > 24 || itemimg.Height > 24))
                itemimg = FitWithin(itemimg, 24);
            // Redraw
            baseImage = LayerImage(baseImage, itemimg, 22 + ((15 - itemimg.Width) / 2), 15 + (15 - itemimg.Height), 1);
        }
        return baseImage;
    }

    public static bool IsTotemForm(int species, int form, int generation = 7)
    {
        if (generation != 7)
            return false;
        if (form == 0)
            return false;
        if (!Legal.Totem_USUM.Contains(species))
            return false;
        if (species == 778) // Mimikyu
            return form is 2 or 3;
        if (Legal.Totem_Alolan.Contains(species))
            return form == 2;
        return form == 1;
    }

    public static int GetTotemBaseForm(int species, int form)
    {
        if (species == 778) // Mimikyu
            return form - 2;
        return form - 1;
    }

    /// <summary>
    /// Item name for an ID, using a cached copy of the name table.
    /// <para>
    /// GameConfig.GetText clones the whole array on every call, which is fine once per editor but
    /// not once per icon drawn in a list. The cache is keyed on the array instance so it refreshes
    /// by itself when the ROM or language changes.
    /// </para>
    /// </summary>
    private static string[] _itemNameCache;
    private static string[][] _itemNameCacheSource;

    /// <summary>
    /// Assigns an image to a PictureBox, disposing whatever it held.
    /// <para>
    /// Setting PictureBox.Image does not release the previous one, and GetSprite returns a freshly
    /// allocated Bitmap every call. Anything that re-renders repeatedly - scrolling a list of
    /// trainers, stepping through entries - therefore leaks one bitmap per step, and a process is
    /// limited to about ten thousand GDI handles before allocation starts failing. That presents as
    /// an unrelated crash a long way from the leak.
    /// </para>
    /// </summary>
    public static void SetImage(PictureBox box, Image image)
    {
        if (box == null) return;
        var previous = box.Image;
        box.Image = image;
        if (!ReferenceEquals(previous, image)) previous?.Dispose();
    }

    /// <summary>Scales an image down to fit a square, preserving aspect ratio. Never scales up.</summary>
    private static Bitmap FitWithin(Bitmap source, int max)
    {
        if (source == null || (source.Width <= max && source.Height <= max)) return source;
        float scale = Math.Min(max / (float)source.Width, max / (float)source.Height);
        int w = Math.Max(1, (int)(source.Width * scale));
        int h = Math.Max(1, (int)(source.Height * scale));

        var scaled = new Bitmap(w, h);
        using var g = Graphics.FromImage(scaled);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.DrawImage(source, 0, 0, w, h);
        return scaled;
    }

    private static string ItemName(int item, GameConfig config)
    {
        if (config?.GameTextStrings == null || item < 0) return null;
        if (!ReferenceEquals(_itemNameCacheSource, config.GameTextStrings))
        {
            _itemNameCache = config.GetText(TextName.ItemNames);
            _itemNameCacheSource = config.GameTextStrings;
        }
        return _itemNameCache != null && item < _itemNameCache.Length ? _itemNameCache[item] : null;
    }

    /// <summary>
    /// The icon for an item: downloaded art first, then the embedded resource, then the generic
    /// held-item picture.
    /// <para>
    /// Name before ID deliberately. The ID-keyed resource is only correct while item numbering is
    /// fixed, and this project moves it - the TM expansion claims 1024-1051, and a randomizer run
    /// reshuffles the rest. An ID lookup then returns some other item's picture and never fails,
    /// which is the worst way for it to be wrong.
    /// </para>
    /// </summary>
    public static Bitmap getIcon(int item, int _, GameConfig config)
    {
        var byName = ItemSpriteCache.Get(ItemName(item, config));
        if (byName != null) return (Bitmap)byName;
        return (Bitmap)Resources.ResourceManager.GetObject("item_" + item) ?? Resources.helditem;
    }

    public static Bitmap GetFriendshipIcon(int level)
    {
        // Simple procedural hearts for now or colors
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color c = level switch
        {
            1 => Color.LightPink,
            2 => Color.DeepPink,
            3 => Color.Red,
            _ => Color.Gray
        };
        if (level == 0) return bmp; // Empty
        
        // Draw heart
        using var brush = new SolidBrush(c);
        g.FillEllipse(brush, 4, 8, 12, 12);
        g.FillEllipse(brush, 16, 8, 12, 12);
        Point[] points = [new Point(4,16), new Point(28,16), new Point(16,28)];
        g.FillPolygon(brush, points);
        return bmp;
    }

    private static Dictionary<string, Color[]> Gradients;
    public static void LoadGradients()
    {
        if (Gradients != null) return;
                Gradients = new Dictionary<string, Color[]>
        {
            ["Xerneas"] = [ColorTranslator.FromHtml("#1C4CB8"), ColorTranslator.FromHtml("#3866D1"), ColorTranslator.FromHtml("#5480EA"), ColorTranslator.FromHtml("#779EF7"), ColorTranslator.FromHtml("#9EBAF9"), ColorTranslator.FromHtml("#C5D7FC"), ColorTranslator.FromHtml("#E6EEFF")],
            ["Yveltal"] = [ColorTranslator.FromHtml("#B81424"), ColorTranslator.FromHtml("#C92A39"), ColorTranslator.FromHtml("#DA404F"), ColorTranslator.FromHtml("#EB5765"), ColorTranslator.FromHtml("#F57884"), ColorTranslator.FromHtml("#FA9EA7"), ColorTranslator.FromHtml("#FCE1E4")],
            ["Groudon"] = [ColorTranslator.FromHtml("#D9381E"), ColorTranslator.FromHtml("#E6532B"), ColorTranslator.FromHtml("#F27038"), ColorTranslator.FromHtml("#FA8D4B"), ColorTranslator.FromHtml("#FCAB65"), ColorTranslator.FromHtml("#FCC788"), ColorTranslator.FromHtml("#FEF0D6")],
            ["Kyogre"] = [ColorTranslator.FromHtml("#0F52BA"), ColorTranslator.FromHtml("#1F68CE"), ColorTranslator.FromHtml("#347FE2"), ColorTranslator.FromHtml("#5097F4"), ColorTranslator.FromHtml("#73B1F9"), ColorTranslator.FromHtml("#9DCCFC"), ColorTranslator.FromHtml("#DBEEFF")],
            ["Solgaleo"] = [ColorTranslator.FromHtml("#E69500"), ColorTranslator.FromHtml("#F2A81D"), ColorTranslator.FromHtml("#FCBD3C"), ColorTranslator.FromHtml("#FCD15D"), ColorTranslator.FromHtml("#FDE283"), ColorTranslator.FromHtml("#FEEFB0"), ColorTranslator.FromHtml("#FFFBE0")],
            ["Lunala"] = [ColorTranslator.FromHtml("#5E2CA5"), ColorTranslator.FromHtml("#743EC2"), ColorTranslator.FromHtml("#8D53DE"), ColorTranslator.FromHtml("#A66CF7"), ColorTranslator.FromHtml("#BF8AFF"), ColorTranslator.FromHtml("#D7B0FF"), ColorTranslator.FromHtml("#F3E8FF")],
            ["Dusk Mane Necrozma"] = [ColorTranslator.FromHtml("#D9822B"), ColorTranslator.FromHtml("#E69943"), ColorTranslator.FromHtml("#F2B05E"), ColorTranslator.FromHtml("#F9C67B"), ColorTranslator.FromHtml("#FCDA9C"), ColorTranslator.FromHtml("#FDEBBE"), ColorTranslator.FromHtml("#FFF9E6")],
            ["Dawn Wings Necrozma"] = [ColorTranslator.FromHtml("#17A2B8"), ColorTranslator.FromHtml("#2EBBC5"), ColorTranslator.FromHtml("#4AD2D2"), ColorTranslator.FromHtml("#6DE6DE"), ColorTranslator.FromHtml("#96F2E8"), ColorTranslator.FromHtml("#C2FBF5"), ColorTranslator.FromHtml("#E8FFFF")],
            ["Necrozma"] = [ColorTranslator.FromHtml("#3A3D52"), ColorTranslator.FromHtml("#535770"), ColorTranslator.FromHtml("#6E738F"), ColorTranslator.FromHtml("#8B91AE"), ColorTranslator.FromHtml("#AAB0CE"), ColorTranslator.FromHtml("#CBD0ED"), ColorTranslator.FromHtml("#F0F2FF")],
            ["Ultra Necrozma"] = [ColorTranslator.FromHtml("#FFB300"), ColorTranslator.FromHtml("#FFC425"), ColorTranslator.FromHtml("#FFD44D"), ColorTranslator.FromHtml("#FFE377"), ColorTranslator.FromHtml("#FFEFA3"), ColorTranslator.FromHtml("#FFF7CC"), ColorTranslator.FromHtml("#FFFDF0")],
            ["Rayquaza"] = [ColorTranslator.FromHtml("#0D8A5F"), ColorTranslator.FromHtml("#1EA374"), ColorTranslator.FromHtml("#33BC8B"), ColorTranslator.FromHtml("#50D4A3"), ColorTranslator.FromHtml("#79E8BD"), ColorTranslator.FromHtml("#AAFDCF"), ColorTranslator.FromHtml("#E1FCF1")],
            ["Deoxys"] = [ColorTranslator.FromHtml("#E64A19"), ColorTranslator.FromHtml("#F46835"), ColorTranslator.FromHtml("#F98752"), ColorTranslator.FromHtml("#FBA772"), ColorTranslator.FromHtml("#F9C696"), ColorTranslator.FromHtml("#EEDFB8"), ColorTranslator.FromHtml("#E0F2F1")],
            ["Zygarde"] = [ColorTranslator.FromHtml("#1B8036"), ColorTranslator.FromHtml("#289945"), ColorTranslator.FromHtml("#37B357"), ColorTranslator.FromHtml("#50CC6F"), ColorTranslator.FromHtml("#74E08F"), ColorTranslator.FromHtml("#9DF2B3"), ColorTranslator.FromHtml("#DCFCE6")],
            ["Magearna"] = [ColorTranslator.FromHtml("#C97C8D"), ColorTranslator.FromHtml("#D691A0"), ColorTranslator.FromHtml("#E3A7B4"), ColorTranslator.FromHtml("#EDBDC8"), ColorTranslator.FromHtml("#F5D2DC"), ColorTranslator.FromHtml("#FAE4EB"), ColorTranslator.FromHtml("#FFF5F8")],
            ["Zeraora"] = [ColorTranslator.FromHtml("#F5B800"), ColorTranslator.FromHtml("#F7CA28"), ColorTranslator.FromHtml("#F9DC50"), ColorTranslator.FromHtml("#FAED7A"), ColorTranslator.FromHtml("#FBF6A4"), ColorTranslator.FromHtml("#E2F9D8"), ColorTranslator.FromHtml("#D0F8FF")],
            ["Marshadow"] = [ColorTranslator.FromHtml("#434853"), ColorTranslator.FromHtml("#5A606E"), ColorTranslator.FromHtml("#73798A"), ColorTranslator.FromHtml("#8E95A7"), ColorTranslator.FromHtml("#ABAEC4"), ColorTranslator.FromHtml("#CACFE2"), ColorTranslator.FromHtml("#EAEDF7")],
            ["Incineroar"] = [ColorTranslator.FromHtml("#D32F2F"), ColorTranslator.FromHtml("#E54937"), ColorTranslator.FromHtml("#F46343"), ColorTranslator.FromHtml("#FB7F53"), ColorTranslator.FromHtml("#FC9C68"), ColorTranslator.FromHtml("#FDBB83"), ColorTranslator.FromHtml("#FEEBD2")],
            ["Decidueye"] = [ColorTranslator.FromHtml("#2E7D32"), ColorTranslator.FromHtml("#3D9140"), ColorTranslator.FromHtml("#4EA651"), ColorTranslator.FromHtml("#63BC64"), ColorTranslator.FromHtml("#7ED17E"), ColorTranslator.FromHtml("#A1E3A1"), ColorTranslator.FromHtml("#E3FAE3")],
            ["Primarina"] = [ColorTranslator.FromHtml("#00ACC1"), ColorTranslator.FromHtml("#26C6DA"), ColorTranslator.FromHtml("#4DD0E1"), ColorTranslator.FromHtml("#80DEEA"), ColorTranslator.FromHtml("#B2EBF2"), ColorTranslator.FromHtml("#E1BEE7"), ColorTranslator.FromHtml("#F3E5F5")],
            ["Latias"] = [ColorTranslator.FromHtml("#E80606"), ColorTranslator.FromHtml("#EA2B2B"), ColorTranslator.FromHtml("#EC4F4F"), ColorTranslator.FromHtml("#EE7474"), ColorTranslator.FromHtml("#F09999"), ColorTranslator.FromHtml("#F2BDBD"), ColorTranslator.FromHtml("#F4E2E2")],
            ["Latios"] = [ColorTranslator.FromHtml("#6A3DFE"), ColorTranslator.FromHtml("#8159F9"), ColorTranslator.FromHtml("#9874F5"), ColorTranslator.FromHtml("#AF90F0"), ColorTranslator.FromHtml("#C6ABEB"), ColorTranslator.FromHtml("#DDC7E7"), ColorTranslator.FromHtml("#F4E2E2")],
            ["Jirachi"] = [ColorTranslator.FromHtml("#F6F39F"), ColorTranslator.FromHtml("#DEE9AE"), ColorTranslator.FromHtml("#C6DFBD"), ColorTranslator.FromHtml("#AED5CC"), ColorTranslator.FromHtml("#96CBDB"), ColorTranslator.FromHtml("#7EC1EA"), ColorTranslator.FromHtml("#66B7F9")],
            ["Diancie"] = [ColorTranslator.FromHtml("#9EA5B1"), ColorTranslator.FromHtml("#ADB0BC"), ColorTranslator.FromHtml("#BDBAC7"), ColorTranslator.FromHtml("#CCC5D2"), ColorTranslator.FromHtml("#DBD0DC"), ColorTranslator.FromHtml("#EBDAE7"), ColorTranslator.FromHtml("#FAE5F2")],
            ["Hoopa"] = [ColorTranslator.FromHtml("#FA6FA0"), ColorTranslator.FromHtml("#F680AC"), ColorTranslator.FromHtml("#F191B7"), ColorTranslator.FromHtml("#EDA2C3"), ColorTranslator.FromHtml("#E8B3CF"), ColorTranslator.FromHtml("#E4C4DA"), ColorTranslator.FromHtml("#DFD5E6")],
        };

        string data = GetInternalText("graidents.txt");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (var line in data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length < 2) continue;
                var colors = parts[1].Split(',');
                if (colors.Length < 2) continue;
                try {
                    Gradients[parts[0].Trim()] = colors.Select(c => ColorTranslator.FromHtml(c.Trim())).ToArray();
                } catch { }
            }
        }
    }

    public static System.Drawing.Drawing2D.LinearGradientBrush CreateGradientBrush(Rectangle rect, string name, float angle = 45f)
    {
        LoadGradients();
        if (rect.Width <= 0 || rect.Height <= 0) return null;
        if (!Gradients.TryGetValue(name, out var colors) || colors == null || colors.Length == 0) return null;

        var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, colors[0], colors[colors.Length - 1], angle);
        if (colors.Length > 2)
        {
            var blend = new System.Drawing.Drawing2D.ColorBlend(colors.Length);
            for (int i = 0; i < colors.Length; i++)
            {
                blend.Positions[i] = (float)i / (colors.Length - 1);
                blend.Colors[i] = colors[i];
            }
            brush.InterpolationColors = blend;
        }
        return brush;
    }

    public static void ApplyGradient(Form f, string name)
    {
        LoadGradients();
        if (!Gradients.ContainsKey(name)) return;
        
        void UpdateBackground()
        {
            if (f.ClientRectangle.Width <= 0 || f.ClientRectangle.Height <= 0) return;
            var bmp = new Bitmap(f.ClientRectangle.Width, f.ClientRectangle.Height);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = CreateGradientBrush(f.ClientRectangle, name, 45f))
            {
                if (brush != null)
                    g.FillRectangle(brush, f.ClientRectangle);
            }
            if (f.BackgroundImage != null) f.BackgroundImage.Dispose();
            f.BackgroundImageLayout = ImageLayout.None;
            f.BackgroundImage = bmp;
        }

        UpdateBackground();
        f.Resize += (s, e) => UpdateBackground();
    }
    
    public static void ApplyMidnightTheme(Control c) => ApplyCyberSlateTheme(c, VisualTheme.Dark);

    public static Bitmap ScaleImage(Bitmap rawImg, int s)
    {
        var bigImg = new Bitmap(rawImg.Width * s, rawImg.Height * s);
        for (int x = 0; x < bigImg.Width; x++)
        {
            for (int y = 0; y < bigImg.Height; y++)
                bigImg.SetPixel(x, y, rawImg.GetPixel(x / s, y / s));
        }

        return bigImg;
    }

    public static Bitmap ToGrayscale(Image img)
    {
        if (img == null)
            return null;
        if (img.PixelFormat.HasFlag(PixelFormat.Indexed))
            return (Bitmap)img;

        var bmp = (Bitmap)img.Clone();
        GetBitmapData(bmp, out BitmapData bmpData, out IntPtr ptr, out byte[] data);

        Marshal.Copy(ptr, data, 0, data.Length);
        SetAllColorToGrayScale(data);
        Marshal.Copy(data, 0, ptr, data.Length);
        bmp.UnlockBits(bmpData);

        return bmp;
    }

    private static void GetBitmapData(Bitmap bmp, out BitmapData bmpData, out IntPtr ptr, out byte[] data)
    {
        bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        ptr = bmpData.Scan0;
        data = new byte[bmp.Width * bmp.Height * 4];
    }

    private static void SetAllColorToGrayScale(byte[] data)
    {
        for (int i = 0; i < data.Length; i += 4)
        {
            if (data[i + 3] == 0)
                continue;
            byte greyS = (byte)(((0.3 * data[i + 2]) + (0.59 * data[i + 1]) + (0.11 * data[i + 0])) / 3);
            data[i + 0] = greyS;
            data[i + 1] = greyS;
            data[i + 2] = greyS;
        }
    }

    public static string GetInternalText(string filename)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(filename));
            if (resourceName == null) return null;

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch { return null; }
    }

    // Strings and Paths

    public static string[] GetSimpleStringList(string f)
    {
        object txt = Resources.ResourceManager.GetObject(f); // Fetch File, \n to list.
        List<string> rawlist = [.. ((string)txt).Split('\n')];

        string[] stringdata = new string[rawlist.Count];
        for (int i = 0; i < rawlist.Count; i++)
            stringdata[i] = rawlist[i].Trim();

        return stringdata;
    }

    // Data Retrieval
    public static int ToInt32(TextBox tb)
    {
        string value = tb.Text;
        return Util.ToInt32(value);
    }

    public static uint ToUInt32(TextBox tb)
    {
        string value = tb.Text;
        return Util.ToUInt32(value);
    }

    public static int ToInt32(MaskedTextBox tb)
    {
        string value = tb.Text;
        return Util.ToInt32(value);
    }

    public static uint ToUInt32(MaskedTextBox tb)
    {
        string value = tb.Text;
        return Util.ToUInt32(value);
    }

    public static int GetIndex(ComboBox cb)
    {
        int val;
        if (cb.SelectedValue == null)
            return 0;

        try
        { val = (int)cb.SelectedValue; }
        catch
        { val = cb.SelectedIndex; if (val < 0) val = 0; }
        return val;
    }

    public static string GetOnlyHex(string str)
    {
        if (str == null)
            return "0";

        string s = "";

        foreach (char t in str)
        {
            var c = t;
            // filter for hex
            if ((c < 0x0047 && c > 0x002F) || (c < 0x0067 && c > 0x0060))
                s += c;
            else
                System.Media.SystemSounds.Beep.Play();
        }
        if (s.Length == 0)
            s = "0";
        return s;
    }

    public static void ApplyTheme(Form f) => ApplyCyberSlateTheme(f, CurrentTheme);
    public static void RefreshAllThemes()
    {
        foreach (Form f in Application.OpenForms)
            ApplyCyberSlateTheme(f, CurrentTheme);
    }

    public static void ApplyCyberSlateTheme(Form form, VisualTheme theme)
    {
        bool dark = theme == VisualTheme.Dark;
        bool grey = theme == VisualTheme.Grey;
        bool light = theme == VisualTheme.Light;
        bool purple = theme == VisualTheme.GalaxyPurple;

        if (form.Name != "Main")
        {
            if (light) form.BackColor = Color.FromArgb(245, 246, 248);
            else if (purple) form.BackColor = Color.FromArgb(40, 22, 44);
            else form.BackColor = dark ? Color.FromArgb(18, 18, 24) : Color.FromArgb(45, 45, 48);
        }
        if (light) form.ForeColor = Color.FromArgb(33, 37, 41);
        else if (purple) form.ForeColor = Color.FromArgb(245, 235, 250);
        else form.ForeColor = dark ? Color.FromArgb(230, 230, 240) : Color.FromArgb(241, 241, 241);
        form.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular, GraphicsUnit.Point);
        
        foreach (Control c in form.Controls)
            ApplyCyberSlateTheme(c, theme);
            
        // Handle MenuStrips
        foreach (MenuStrip ms in form.Controls.OfType<MenuStrip>())
        {
            if (light) {
                ms.BackColor = Color.FromArgb(235, 238, 242);
                ms.ForeColor = Color.FromArgb(33, 37, 41);
            } else if (purple) {
                ms.BackColor = Color.FromArgb(32, 16, 36);
                ms.ForeColor = Color.FromArgb(245, 235, 250);
            } else {
                ms.BackColor = dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(60, 60, 70);
                ms.ForeColor = Color.WhiteSmoke;
            }
            foreach (ToolStripMenuItem item in ms.Items)
                ApplyThemeToMenuItem(item, theme);
        }
    }

    private static bool _filterAdded = false;

    private static void ApplyCyberSlateTheme(Control c, VisualTheme theme)
    {
        if (!_filterAdded)
        {
            Application.AddMessageFilter(new ComboBoxArrowKeyFilter());
            _filterAdded = true;
        }

        bool dark = theme == VisualTheme.Dark;
        bool grey = theme == VisualTheme.Grey;
        bool light = theme == VisualTheme.Light;
        bool purple = theme == VisualTheme.GalaxyPurple;

        if ((c is Panel || c is GroupBox || c is TabPage) && c.Name != "PNL_Sidebar")
        {
            if (light) { c.BackColor = Color.FromArgb(255, 255, 255); c.ForeColor = Color.FromArgb(33, 37, 41); }
            else if (purple) { c.BackColor = Color.FromArgb(52, 28, 58); c.ForeColor = Color.FromArgb(245, 235, 250); }
            else { c.BackColor = dark ? Color.FromArgb(20, 20, 30) : Color.FromArgb(55, 55, 60); c.ForeColor = Color.WhiteSmoke; }
            if (c is TabPage tp) tp.UseVisualStyleBackColor = false;
        }
        else if (c is Label lbl)
        {
            if (light) { lbl.ForeColor = Color.FromArgb(33, 37, 41); }
            else if (purple) { lbl.ForeColor = Color.FromArgb(245, 235, 250); }
            else { lbl.ForeColor = dark ? Color.FromArgb(220, 220, 220) : Color.FromArgb(235, 235, 235); }
        }
        else if (c is PictureBox pb)
        {
            pb.BackColor = Color.Transparent;
        }
        else if (c is CheckBox chk)
        {
            chk.BackColor = Color.Transparent;
        }
        else if (c is Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            if (light) {
                btn.BackColor = Color.FromArgb(235, 238, 242);
                btn.ForeColor = Color.FromArgb(33, 37, 41);
                btn.FlatAppearance.BorderColor = Color.FromArgb(206, 212, 218);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 249, 250);
            } else if (purple) {
                btn.BackColor = Color.FromArgb(68, 36, 76);
                btn.ForeColor = Color.FromArgb(245, 235, 250);
                btn.FlatAppearance.BorderColor = Color.FromArgb(102, 53, 114);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(88, 48, 98);
            } else {
                btn.BackColor = dark ? Color.FromArgb(32, 34, 46) : Color.FromArgb(70, 70, 75);
                btn.ForeColor = Color.WhiteSmoke;
                btn.FlatAppearance.BorderColor = dark ? Color.FromArgb(60, 65, 85) : Color.FromArgb(90, 90, 95);
                btn.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(50, 55, 75) : Color.FromArgb(85, 85, 90);
            }
            btn.FlatAppearance.BorderSize = 1;
            btn.Cursor = Cursors.Hand;
        }
        else if (c is TextBoxBase tb)
        {
            if (light) {
                tb.BackColor = Color.FromArgb(255, 255, 255);
                tb.ForeColor = Color.FromArgb(33, 37, 41);
            } else if (purple) {
                tb.BackColor = Color.FromArgb(32, 16, 36);
                tb.ForeColor = Color.FromArgb(245, 235, 250);
            } else {
                tb.BackColor = dark ? Color.FromArgb(12, 12, 18) : Color.FromArgb(40, 40, 45);
                tb.ForeColor = Color.FromArgb(220, 220, 230);
            }
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is TreeView tv)
        {
            // TreeView was not handled at all, so it kept the system's white background whatever the
            // theme - the OWSE script tree being the visible case.
            if (light) {
                tv.BackColor = SystemColors.Window;
                tv.ForeColor = SystemColors.WindowText;
                tv.LineColor = SystemColors.ControlDark;
            } else if (purple) {
                tv.BackColor = Color.FromArgb(32, 16, 36);
                tv.ForeColor = Color.FromArgb(245, 235, 250);
                tv.LineColor = Color.FromArgb(142, 73, 128);
            } else {
                tv.BackColor = dark ? Color.FromArgb(12, 12, 18) : Color.FromArgb(40, 40, 45);
                tv.ForeColor = Color.FromArgb(220, 220, 230);
                tv.LineColor = Color.FromArgb(90, 95, 115);
            }
            tv.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is ListControl lc)
        {
            if (light) {
                lc.BackColor = SystemColors.Window;
                lc.ForeColor = SystemColors.WindowText;
            } else if (purple) {
                lc.BackColor = Color.FromArgb(82, 42, 74);
                lc.ForeColor = Color.WhiteSmoke;
            } else {
                lc.BackColor = dark ? Color.FromArgb(12, 12, 18) : Color.FromArgb(40, 40, 45);
                lc.ForeColor = Color.FromArgb(220, 220, 230);
            }
        }
        else if (c is ComboBox cb)
        {
            if (light) {
                cb.BackColor = SystemColors.Window;
                cb.ForeColor = SystemColors.WindowText;
            } else if (purple) {
                cb.BackColor = Color.FromArgb(92, 47, 82);
                cb.ForeColor = Color.WhiteSmoke;
            } else {
                cb.BackColor = dark ? Color.FromArgb(25, 25, 35) : Color.FromArgb(50, 50, 55);
                cb.ForeColor = Color.WhiteSmoke;
            }
            cb.FlatStyle = FlatStyle.Flat;
        }
        else if (c is ListBox lb)
        {
            if (light) {
                lb.BackColor = SystemColors.Window;
                lb.ForeColor = SystemColors.WindowText;
            } else if (purple) {
                lb.BackColor = Color.FromArgb(82, 42, 74);
                lb.ForeColor = Color.WhiteSmoke;
            } else {
                lb.BackColor = dark ? Color.FromArgb(15, 15, 25) : Color.FromArgb(45, 45, 50);
                lb.ForeColor = Color.WhiteSmoke;
            }
            lb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is DataGridView dgv)
        {
            if (light) {
                dgv.BackgroundColor = SystemColors.ControlDark;
                dgv.DefaultCellStyle.BackColor = SystemColors.Window;
                dgv.DefaultCellStyle.ForeColor = SystemColors.WindowText;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
                dgv.GridColor = SystemColors.ControlDark;
            } else if (purple) {
                dgv.BackgroundColor = Color.FromArgb(122, 63, 110);
                dgv.DefaultCellStyle.BackColor = Color.FromArgb(102, 53, 92);
                dgv.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(142, 73, 128);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
                dgv.GridColor = Color.FromArgb(162, 83, 146);
            } else {
                dgv.BackgroundColor = dark ? Color.FromArgb(18, 18, 24) : Color.FromArgb(45, 45, 50);
                dgv.DefaultCellStyle.BackColor = dark ? Color.FromArgb(24, 26, 36) : Color.FromArgb(50, 50, 55);
                dgv.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(40, 44, 60) : Color.FromArgb(65, 65, 70);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
                dgv.GridColor = dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(70, 70, 75);
            }
            dgv.EnableHeadersVisualStyles = false;
        }
        
        if (c is TabControl tc)
        {
            tc.Appearance = TabAppearance.Normal;
            SetDoubleBuffered(tc);
            ApplyOwnerDrawnTabs(tc, theme);
        }

        // Glassmorphism effect for panels
        if (c is Panel p && p.Name.Contains("Glass"))
        {
            if (light) p.BackColor = Color.FromArgb(180, 240, 240, 240);
            else if (purple) p.BackColor = Color.FromArgb(180, 122, 63, 110);
            else p.BackColor = dark ? Color.FromArgb(180, 25, 25, 35) : Color.FromArgb(180, 50, 50, 60);
        }

        foreach (Control child in c.Controls)
            ApplyCyberSlateTheme(child, theme);
    }

    public static void ApplyThemeToMenuItem(ToolStripMenuItem item, VisualTheme theme)
    {
        bool dark = theme == VisualTheme.Dark;
        bool grey = theme == VisualTheme.Grey;
        bool light = theme == VisualTheme.Light;
        bool purple = theme == VisualTheme.GalaxyPurple;

        if (light) {
            item.BackColor = SystemColors.Control;
            item.ForeColor = SystemColors.ControlText;
        } else if (purple) {
            item.BackColor = Color.FromArgb(122, 63, 110);
            item.ForeColor = Color.WhiteSmoke;
        } else {
            item.BackColor = dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(60, 60, 70);
            item.ForeColor = Color.WhiteSmoke;
        }

        foreach (ToolStripItem sub in item.DropDownItems)
        {
            if (light) {
                sub.BackColor = SystemColors.Control;
                sub.ForeColor = SystemColors.ControlText;
            } else if (purple) {
                sub.BackColor = Color.FromArgb(122, 63, 110);
                sub.ForeColor = Color.WhiteSmoke;
            } else {
                sub.BackColor = dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(60, 60, 70);
                sub.ForeColor = Color.WhiteSmoke;
            }

            if (sub is ToolStripMenuItem tsmi)
                ApplyThemeToMenuItem(tsmi, theme);
        }
    }

    /// <summary>
    /// Double-buffers a control and every container beneath it.
    /// <para>
    /// Buffering a TabControl on its own does nothing for tab switching: the tearing is between the
    /// page and the panels and buttons it holds, not inside the tab strip. The recursion is what
    /// makes this worth calling at all.
    /// </para>
    /// </summary>
    /// <summary>
    /// Draws a TabControl's strip ourselves, in the theme's colours.
    /// <para>
    /// Left to Windows, the strip is painted by the OS in the system theme - which is why it stayed
    /// light against a dark window - and the OS repaints a tab whenever the mouse enters or leaves
    /// it. That hot-tracking repaint is what flickered on hover: the strip is not opaque to the
    /// form behind it, so each repaint exposed the parent and forced the gradient through again.
    /// Owner-drawing fills every tab opaquely and removes the hot-track pass entirely, so hovering
    /// no longer repaints anything.
    /// </para>
    /// </summary>
    private static void ApplyOwnerDrawnTabs(TabControl tc, VisualTheme theme)
    {
        bool light = theme == VisualTheme.Light;
        bool purple = theme == VisualTheme.GalaxyPurple;

        Color strip = light ? Color.FromArgb(235, 235, 240)
                    : purple ? Color.FromArgb(38, 20, 44)
                    : Color.FromArgb(20, 20, 30);
        Color selected = light ? Color.White
                       : purple ? Color.FromArgb(72, 40, 82)
                       : Color.FromArgb(45, 45, 60);
        Color text = light ? Color.FromArgb(33, 37, 41) : Color.WhiteSmoke;

        tc.DrawMode = TabDrawMode.OwnerDrawFixed;

        // Replace rather than add: theming runs again on every theme switch, and stacking handlers
        // would draw each tab once per switch.
        tc.DrawItem -= TabDrawHandler;
        tc.DrawItem += TabDrawHandler;
        tc.Paint -= TabStripPaintHandler;
        tc.Paint += TabStripPaintHandler;
        TabPalette[tc] = (strip, selected, text);

        // Attached once per control; theming runs again on every switch.
        if (!TabOverlays.ContainsKey(tc))
            TabOverlays[tc] = new TabStripOverlay(tc);

        // The strip's own background, behind and beside the tabs.
        tc.BackColor = strip;
    }

    private static readonly Dictionary<TabControl, (Color Strip, Color Selected, Color Text)> TabPalette = new();

    /// <summary>
    /// Supplies the window's painted background so child controls can continue it rather than
    /// guess at a matching flat colour. Set by the form that owns the gradient.
    /// </summary>
    public static Func<Bitmap> BackgroundProvider { get; set; }

    /// <summary>
    /// The form <see cref="BackgroundProvider"/> belongs to.
    /// <para>
    /// The provider is a single static, so without this every themed window borrowed the main
    /// window's gradient for its tab strip - a gradient that is not behind it, producing a strip
    /// that matched nothing. Tab strips on any other form fall back to the flat theme colour.
    /// </para>
    /// </summary>
    public static Form BackgroundOwner { get; set; }

    /// <summary>
    /// Fills the part of the tab strip no tab covers.
    /// <para>
    /// Owner-drawing supplies the tab items only; the run of strip to the right of the last tab is
    /// still painted by the control, which is why it stayed light against a dark window. Paint runs
    /// after the items are drawn, so filling everything outside their bounds here leaves the tabs
    /// intact and replaces only the gap.
    /// </para>
    /// <para>
    /// Where the owning form exposes its background, the matching slice of it is drawn so the strip
    /// continues the gradient instead of approximating it with a flat colour that only matches at
    /// one point.
    /// </para>
    /// </summary>
    private static void TabStripPaintHandler(object sender, PaintEventArgs e)
    {
        if (sender is not TabControl tc) return;
        PaintStrip(tc, e.Graphics);
    }

    /// <summary>
    /// Repaints the tab strip after the native control has drawn it.
    /// <para>
    /// A TabControl is a native common control that paints its own strip during WM_PAINT. Relying on
    /// the managed Paint event to cover that is unreliable - the ordering depends on double
    /// buffering and on whether visual styles drew the strip in the theme pass - which is how the
    /// strip kept coming back as a light band across a dark window. Painting from the message loop
    /// instead, immediately after the default handler has run, is deterministic.
    /// </para>
    /// </summary>
    private sealed class TabStripOverlay : NativeWindow
    {
        private readonly TabControl tc;

        public TabStripOverlay(TabControl control)
        {
            tc = control;
            if (tc.IsHandleCreated)
                AssignHandle(tc.Handle);
            tc.HandleCreated += (_, _) => AssignHandle(tc.Handle);
            tc.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            if (m.Msg != WM_PAINT || tc.IsDisposed || !tc.IsHandleCreated)
                return;

            try
            {
                using var g = Graphics.FromHwnd(tc.Handle);
                PaintStrip(tc, g);
            }
            catch (Exception) { /* the control can go away mid-paint; the strip is cosmetic */ }
        }
    }

    private static readonly Dictionary<TabControl, TabStripOverlay> TabOverlays = new();

    private static void PaintStrip(TabControl tc, Graphics graphics)
    {
        if (!TabPalette.TryGetValue(tc, out var palette)) return;

        int stripHeight = tc.ItemSize.Height + 4;
        var strip = new Rectangle(0, 0, tc.Width, Math.Min(stripHeight, tc.Height));

        // Everything in the strip that is not a tab.
        using var region = new Region(strip);
        for (int i = 0; i < tc.TabCount; i++)
        {
            try { region.Exclude(tc.GetTabRect(i)); }
            catch { /* tab rects are unavailable mid-layout; the fill just covers more */ }
        }

        var clip = graphics.Clip;
        graphics.Clip = region;

        var form = tc.FindForm();
        var background = ReferenceEquals(form, BackgroundOwner) ? BackgroundProvider?.Invoke() : null;
        if (background != null)
        {
            // The strip's position within the form, so the gradient lines up seamlessly.
            var origin = tc.PointToScreen(Point.Empty);
            var formOrigin = form?.PointToScreen(Point.Empty) ?? origin;
            int dx = origin.X - formOrigin.X;
            int dy = origin.Y - formOrigin.Y;
            graphics.DrawImage(background, new Rectangle(-dx, -dy, background.Width, background.Height));
        }
        else
        {
            using var brush = new SolidBrush(palette.Strip);
            graphics.FillRectangle(brush, strip);
        }

        graphics.Clip = clip;
    }

    private static void TabDrawHandler(object sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tc) return;
        if (!TabPalette.TryGetValue(tc, out var palette)) return;
        if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;

        bool isSelected = e.Index == tc.SelectedIndex;
        var bounds = e.Bounds;

        using (var back = new SolidBrush(isSelected ? palette.Selected : palette.Strip))
            e.Graphics.FillRectangle(back, bounds);

        // A selected tab gets a top accent so it reads as active without relying on the OS.
        if (isSelected)
        {
            using var accent = new SolidBrush(Color.FromArgb(90, 140, 220));
            e.Graphics.FillRectangle(accent, bounds.X, bounds.Y, bounds.Width, 2);
        }

        TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, bounds, palette.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
    }

    public static void SetDoubleBufferedTree(Control c)
    {
        if (c == null) return;
        SetDoubleBuffered(c);
        foreach (Control child in c.Controls)
            SetDoubleBufferedTree(child);
    }

    public static void SetDoubleBuffered(Control c)
    {
        if (SystemInformation.TerminalServerSession) return;
        System.Reflection.PropertyInfo propertyInfo = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        propertyInfo?.SetValue(c, true, null);
    }

    // Form Translation
    public static void TranslateInterface(Control form, string lang)
    {
        // Check to see if a the translation file exists in the same folder as the executable
        string externalLangPath = "lang_" + lang + ".txt";
        string[] rawlist;
        if (File.Exists(externalLangPath))
        {
            rawlist = File.ReadAllLines(externalLangPath);
        }
        else
        {
            object txt = Resources.ResourceManager.GetObject("lang_" + lang);
            if (txt == null) return; // Translation file does not exist as a resource; abort this function and don't translate UI.
            rawlist = ((string)txt).Split(["\n"], StringSplitOptions.None);
            rawlist = rawlist.Select(i => i.Trim()).ToArray(); // Remove trailing spaces
        }

        string[] stringdata = new string[rawlist.Length];
        int itemsToRename = 0;
        for (int i = 0; i < rawlist.Length; i++)
        {
            // Find our starting point
            if (!rawlist[i].Contains("! " + form.Name)) continue;

            // Allow renaming of the Window Title
            string[] WindowName = rawlist[i].Split([" = "], StringSplitOptions.None);
            if (WindowName.Length > 1) form.Text = WindowName[1];
            // Copy our Control Names and Text to a new array for later processing.
            for (int j = i + 1; j < rawlist.Length; j++)
            {
                if (rawlist[j].Length == 0) continue; // Skip Over Empty Lines, errhandled
                if (rawlist[j][0].ToString() == "-") continue; // Keep translating if line is a comment line
                if (rawlist[j][0].ToString() == "!") // Stop if we have reached the end of translation
                    goto rename;
                stringdata[itemsToRename] = rawlist[j]; // Add the entry to process later.
                itemsToRename++;
            }
        }
        return; // Not Found

        // Now that we have our items to rename in: Control = Text format, let's execute the changes!
        rename:
        for (int i = 0; i < itemsToRename; i++)
        {
            string[] SplitString = stringdata[i].Split([" = "], StringSplitOptions.None);
            if (SplitString.Length < 2)
                continue; // Error in Input, errhandled
            string ctrl = SplitString[0]; // Control to change the text of...
            string text = SplitString[1]; // Text to set Control.Text to...
            Control[] controllist = form.Controls.Find(ctrl, true);
            if (controllist.Length != 0) // If Control is found
            { controllist[0].Text = text; goto next; }

            // Check MenuStrips
            foreach (MenuStrip menu in form.Controls.OfType<MenuStrip>())
            {
                // Menu Items aren't in the Form's Control array. Find within the menu's Control array.
                ToolStripItem[] TSI = menu.Items.Find(ctrl, true);
                if (TSI.Length == 0) continue;

                TSI[0].Text = text; goto next;
            }
            // Check ContextMenuStrips
            foreach (ContextMenuStrip cs in FindContextMenuStrips(form.Controls.OfType<Control>()).Distinct())
            {
                ToolStripItem[] TSI = cs.Items.Find(ctrl, true);
                if (TSI.Length == 0) continue;

                TSI[0].Text = text; goto next;
            }

            next:;
        }
    }

    public static List<ContextMenuStrip> FindContextMenuStrips(IEnumerable<Control> c)
    {
        List<ContextMenuStrip> cs = [];
        foreach (Control control in c)
        {
            if (control.ContextMenuStrip != null)
                cs.Add(control.ContextMenuStrip);
            else if (control.Controls.Count > 0)
                cs.AddRange(FindContextMenuStrips(control.Controls.OfType<Control>()));
        }
        return cs;
    }

    // Message Displays
    //
    // Both of these run their text through the same redaction the crash window uses. Only that
    // window was redacting before, so the far more common path - a caught exception handed to
    // Error() as e.ToString() - printed the full stack trace, and with it the account name and the
    // whole directory layout, straight into a message box. Doing it here covers every call site at
    // once rather than relying on each one to remember.
    public static DialogResult Error(params string[] lines)
    {
        System.Media.SystemSounds.Exclamation.Play();
        string msg = ErrorWindow.Redact(string.Join(Environment.NewLine + Environment.NewLine, lines));
        return MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static DialogResult Alert(params string[] lines)
    {
        System.Media.SystemSounds.Asterisk.Play();
        string msg = ErrorWindow.Redact(string.Join(Environment.NewLine + Environment.NewLine, lines));
        return MessageBox.Show(msg, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static DialogResult Prompt(MessageBoxButtons btn, params string[] lines)
    {
        System.Media.SystemSounds.Question.Play();
        string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
        return MessageBox.Show(msg, "Prompt", btn, MessageBoxIcon.Asterisk);
    }

    /// <summary>
    /// Asks the user to choose between the Regular and Competitive randomizer up front, before
    /// the Universal Randomizer window itself opens. Returns true for Competitive, false for
    /// Regular, or null if the user closed the prompt without choosing.
    /// </summary>
    public static bool? PromptRandomizerMode()
    {
        using var dialog = new Form
        {
            Text = "Choose Randomizer Mode",
            // Shorter now that the two captions under the buttons are gone.
            Size = new System.Drawing.Size(420, 215),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        ApplyTheme(dialog);

        var label = new Label
        {
            Text = "How would you like to randomize?",
            Location = new System.Drawing.Point(20, 20),
            Size = new System.Drawing.Size(380, 25),
            Font = new System.Drawing.Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var btnRegular = new Button
        {
            Text = "Regular",
            Location = new System.Drawing.Point(30, 70),
            Size = new System.Drawing.Size(160, 90),
            DialogResult = DialogResult.No,
        };

        var btnCompetitive = new Button
        {
            Text = "Competitive",
            Location = new System.Drawing.Point(220, 70),
            Size = new System.Drawing.Size(160, 90),
            DialogResult = DialogResult.Yes,
        };

        dialog.Controls.AddRange(new Control[] { label, btnRegular, btnCompetitive });

        var result = dialog.ShowDialog();
        return result switch
        {
            DialogResult.Yes => true,
            DialogResult.No => false,
            _ => null,
        };
    }

    public static string PromptInput(string title, string prompt, string defaultText = "") => GetInput(title, prompt, defaultText);

    public static string GetInput(string title, string prompt, string defaultText = "")
    {
        Form form = new Form();
        Label label = new Label();
        TextBox textBox = new TextBox();
        Button buttonOk = new Button();
        Button buttonCancel = new Button();

        form.Text = title;
        label.Text = prompt;
        textBox.Text = defaultText;

        buttonOk.Text = "OK";
        buttonCancel.Text = "Cancel";
        buttonOk.DialogResult = DialogResult.OK;
        buttonCancel.DialogResult = DialogResult.Cancel;

        label.SetBounds(9, 20, 372, 13);
        textBox.SetBounds(12, 36, 372, 20);
        buttonOk.SetBounds(228, 72, 75, 23);
        buttonCancel.SetBounds(309, 72, 75, 23);

        label.AutoSize = true;
        textBox.Anchor |= AnchorStyles.Right;
        buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        form.ClientSize = new Size(396, 107);
        form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
        form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        DialogResult dialogResult = form.ShowDialog();
        return dialogResult == DialogResult.OK ? textBox.Text : null;
    }

    public static List<ComboItem> GetCBList(string textfile, string lang)
    {
        // Set up
        string[] inputCSV = GetSimpleStringList(textfile);

        // Get Language we're fetching for
        int index = Array.IndexOf(["ja", "en", "fr", "de", "it", "es", "ko", "zh"], lang);

        // Set up our Temporary Storage
        string[] unsortedList = new string[inputCSV.Length - 1];
        int[] indexes = new int[inputCSV.Length - 1];

        // Gather our data from the input file
        for (int i = 1; i < inputCSV.Length; i++)
        {
            string[] countryData = inputCSV[i].Split(',');
            indexes[i - 1] = Convert.ToInt32(countryData[0]);
            unsortedList[i - 1] = countryData[index + 1];
        }

        // Sort our input data
        string[] sortedList = new string[inputCSV.Length - 1];
        Array.Copy(unsortedList, sortedList, unsortedList.Length);
        Array.Sort(sortedList);

        // Arrange the input data based on original number
        return sortedList.Select(t => new ComboItem
        {
            Text = t,
            Value = indexes[Array.IndexOf(unsortedList, t)],
        }).ToList();
    }

    public static List<ComboItem> GetCBList(string[] inStrings, params int[][] allowed)
    {
        List<ComboItem> cbList = [];
        allowed ??= [Enumerable.Range(0, inStrings.Length).ToArray()];

        foreach (int[] list in allowed)
        {
            // Sort the Rest based on String Name
            string[] unsortedChoices = new string[list.Length];
            for (int i = 0; i < list.Length; i++)
                unsortedChoices[i] = inStrings[list[i]];

            string[] sortedChoices = new string[unsortedChoices.Length];
            Array.Copy(unsortedChoices, sortedChoices, unsortedChoices.Length);
            Array.Sort(sortedChoices);

            // Add the rest of the items
            cbList.AddRange(sortedChoices.Select(t => new ComboItem
            {
                Text = t,
                Value = list[Array.IndexOf(unsortedChoices, t)],
            }));
        }
        return cbList;
    }

    public static List<ComboItem> GetOffsetCBList(List<ComboItem> cbList, string[] inStrings, int offset, int[] allowed)
    {
        allowed ??= Enumerable.Range(0, inStrings.Length).ToArray();

        int[] list = (int[])allowed.Clone();
        for (int i = 0; i < list.Length; i++)
            list[i] -= offset;

        {
            // Sort the Rest based on String Name
            string[] unsortedChoices = new string[allowed.Length];
            for (int i = 0; i < allowed.Length; i++)
                unsortedChoices[i] = inStrings[list[i]];

            string[] sortedChoices = new string[unsortedChoices.Length];
            Array.Copy(unsortedChoices, sortedChoices, unsortedChoices.Length);
            Array.Sort(sortedChoices);

            // Add the rest of the items
            cbList.AddRange(sortedChoices.Select(t => new ComboItem
            {
                Text = t,
                Value = allowed[Array.IndexOf(unsortedChoices, t)],
            }));
        }
        return cbList;
    }

    // Misc
    public static int HighlightText(RichTextBox RTB, string word, Color hlColor)
    {
        int ctr = 0;
        int s_start = RTB.SelectionStart, startIndex = 0, index;

        while ((index = RTB.Text.IndexOf(word, startIndex, StringComparison.Ordinal)) != -1)
        {
            RTB.Select(index, word.Length);
            RTB.SelectionColor = hlColor;

            startIndex = index + word.Length;
            ctr++;
        }

        RTB.SelectionStart = s_start;
        RTB.SelectionLength = 0;
        RTB.SelectionColor = Color.Black;
        return ctr;
    }

    // http://stackoverflow.com/questions/4820212/automatically-trim-a-bitmap-to-minimum-size
    public static Bitmap TrimBitmap(Bitmap source)
    {
        Rectangle srcRect;
        BitmapData data = null;
        try
        {
            data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] buffer = new byte[data.Height * data.Stride];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            int xMin = int.MaxValue,
                xMax = int.MinValue,
                yMin = int.MaxValue,
                yMax = int.MinValue;

            bool foundPixel = false;

            // Find xMin
            for (int x = 0; x < data.Width; x++)
            {
                bool stop = false;
                for (int y = 0; y < data.Height; y++)
                {
                    byte alpha = buffer[(y * data.Stride) + (4 * x) + 3];
                    if (alpha != 0)
                    {
                        xMin = x;
                        stop = true;
                        foundPixel = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            // Image is empty...
            if (!foundPixel)
                return null;

            // Find yMin
            for (int y = 0; y < data.Height; y++)
            {
                bool stop = false;
                for (int x = xMin; x < data.Width; x++)
                {
                    byte alpha = buffer[(y * data.Stride) + (4 * x) + 3];
                    if (alpha != 0)
                    {
                        yMin = y;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            // Find xMax
            for (int x = data.Width - 1; x >= xMin; x--)
            {
                bool stop = false;
                for (int y = yMin; y < data.Height; y++)
                {
                    byte alpha = buffer[(y * data.Stride) + (4 * x) + 3];
                    if (alpha != 0)
                    {
                        xMax = x;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            // Find yMax
            for (int y = data.Height - 1; y >= yMin; y--)
            {
                bool stop = false;
                for (int x = xMin; x <= xMax; x++)
                {
                    byte alpha = buffer[(y * data.Stride) + (4 * x) + 3];
                    if (alpha != 0)
                    {
                        yMax = y;
                        stop = true;
                        break;
                    }
                }
                if (stop)
                    break;
            }

            srcRect = Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1); // fixed; was cropping 1px too much on the max end
        }
        finally
        {
            if (data != null)
                source.UnlockBits(data);
        }

        var dest = new Bitmap(srcRect.Width, srcRect.Height);
        var destRect = srcRect with { X = 0, Y = 0 };
        using var graphics = Graphics.FromImage(dest);
        graphics.DrawImage(source, destRect, srcRect, GraphicsUnit.Pixel);
        return dest;
    }
}

// DataSource Providing
public class ComboItem
{
    public string Text { get; set; }
    public object Value { get; set; }
    
    public override string ToString() => Text;
}

public class ComboBoxArrowKeyFilter : IMessageFilter
{
    private const int WM_KEYDOWN = 0x0100;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_KEYDOWN)
            return false;

        Keys key = (Keys)m.WParam.ToInt32();
        if (key != Keys.Up && key != Keys.Down)
            return false;

        // Try to find a ComboBox from the focused window handle.
        // When autocomplete is active, the focused window is a native EDIT child
        // that Control.FromHandle returns null for. Walk up via GetParent.
        ComboBox cb = FindComboBox(m.HWnd);
        if (cb != null)
            return HandleComboBoxNavigation(cb, key);

        return false;
    }

    private static ComboBox FindComboBox(IntPtr hWnd)
    {
        // First check if the handle itself is a managed ComboBox
        Control c = Control.FromHandle(hWnd);
        if (c is ComboBox cb)
            return cb;
        if (c != null && c.Parent is ComboBox parentCb)
            return parentCb;

        // Walk up the native window hierarchy to find the ComboBox
        // (handles the case where hWnd is the internal EDIT control)
        IntPtr parent = GetParent(hWnd);
        if (parent != IntPtr.Zero)
        {
            Control parentCtrl = Control.FromHandle(parent);
            if (parentCtrl is ComboBox pcb)
                return pcb;
        }

        return null;
    }

    private bool HandleComboBoxNavigation(ComboBox cb, Keys key)
    {
        if (cb.DroppedDown) return false;

        int newIndex = cb.SelectedIndex;
        if (key == Keys.Up && newIndex > 0) newIndex--;
        else if (key == Keys.Down && newIndex < cb.Items.Count - 1) newIndex++;

        if (newIndex != cb.SelectedIndex)
        {
            cb.SelectedIndex = newIndex;
            if (cb.DropDownStyle != ComboBoxStyle.DropDownList)
            {
                // Select all text so the next typed character replaces, 
                // preventing the old autocomplete prefix from sticking
                cb.SelectionStart = 0;
                cb.SelectionLength = cb.Text.Length;
            }
            return true; // Suppress message — we handled navigation
        }
        return true; // Suppress even at boundaries to prevent autocomplete filter
    }
}
