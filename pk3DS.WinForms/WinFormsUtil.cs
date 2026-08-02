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

        // 1. Check extracted_sprites directory for Gen 8/9 expansion species (808 to 1025)
        string extractedFolder = @"C:\Users\fulto\Downloads\Sprites\extracted_sprites";
        if (Directory.Exists(extractedFolder))
        {
            string spritePath = null;
            int targetIdx = (species >= 808 && species <= 1025) ? species : ((formIdx >= 808 && formIdx <= 1025) ? formIdx : 0);
            if (targetIdx >= 808 && targetIdx <= 1025)
            {
                string sPadded = Path.Combine(extractedFolder, $"sprite_{targetIdx:D5}.png");
                string sDirect = Path.Combine(extractedFolder, $"sprite_{targetIdx}.png");
                if (File.Exists(sPadded)) spritePath = sPadded;
                else if (File.Exists(sDirect)) spritePath = sDirect;
            }

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

        // 2. Check CustomSprites directory
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

        // 3. Check Assembly Manifest Embedded Resources
        if (baseImage == null)
        {
            try
            {
                var asm = typeof(WinFormsUtil).Assembly;
                List<string> searchNames = new List<string>();
                if (formIdx > species)
                    searchNames.Add($"_{formIdx}.png");
                searchNames.Add($"{file}.png");

                var allRes = asm.GetManifestResourceNames();
                foreach (var searchName in searchNames)
                {
                    string targetRes = allRes.FirstOrDefault(r => r.EndsWith(searchName, StringComparison.OrdinalIgnoreCase));
                    if (targetRes != null)
                    {
                        using var stream = asm.GetManifestResourceStream(targetRes);
                        if (stream != null)
                        {
                            using var tempImg = Image.FromStream(stream);
                            baseImage = new Bitmap(tempImg);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        // 4. Redrawing & Totem logic fallback
        if (baseImage == null)
            baseImage = (Bitmap)Resources.ResourceManager.GetObject(file);
        if (IsTotemForm(species, form))
        {
            form = GetTotemBaseForm(species, form);
            file = GetResourceStringSprite(species, form, gender, Main.Config.Generation);
            baseImage = (Bitmap)Resources.ResourceManager.GetObject(file);
            baseImage = ToGrayscale(baseImage);
        }
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
            Bitmap itemimg = (Bitmap)(Resources.ResourceManager.GetObject("item_" + item) ?? Resources.helditem);
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

    public static Bitmap getIcon(int item, int _, GameConfig config)
    {
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

    private static Dictionary<string, (Color, Color)> Gradients;
    public static void LoadGradients()
    {
        if (Gradients != null) return;
        Gradients = new Dictionary<string, (Color, Color)>
        {
            ["Xerneas"] = (Color.FromArgb(30, 60, 100), Color.FromArgb(10, 20, 40)),
            ["Yveltal"] = (Color.FromArgb(100, 20, 30), Color.FromArgb(40, 10, 10)),
            ["Groudon"] = (Color.FromArgb(120, 40, 20), Color.FromArgb(50, 20, 10)),
            ["Kyogre"] = (Color.FromArgb(20, 60, 120), Color.FromArgb(10, 30, 60)),
            ["Solgaleo"] = (Color.FromArgb(140, 100, 40), Color.FromArgb(60, 40, 10)),
            ["Lunala"] = (Color.FromArgb(60, 40, 100), Color.FromArgb(20, 10, 40)),
            ["Dusk Mane Necrozma"] = (ColorTranslator.FromHtml("#F5E9D0"), ColorTranslator.FromHtml("#625D53")),
            ["Dawn Wings Necrozma"] = (ColorTranslator.FromHtml("#B2DAE2"), ColorTranslator.FromHtml("#47575A")),
            ["Necrozma"] = (ColorTranslator.FromHtml("#4A4B56"), ColorTranslator.FromHtml("#1D1E22")),
            ["Ultra Necrozma"] = (Color.FromArgb(160, 140, 60), Color.FromArgb(80, 70, 20)),
            ["Rayquaza"] = (Color.FromArgb(40, 100, 60), Color.FromArgb(10, 40, 20)),
            ["Deoxys"] = (Color.FromArgb(100, 40, 100), Color.FromArgb(40, 10, 40)),
            ["Zygarde"] = (Color.FromArgb(60, 100, 40), Color.FromArgb(20, 40, 10)),
            ["Magearna"] = (ColorTranslator.FromHtml("#D3B6B9"), ColorTranslator.FromHtml("#54484A")),
            ["Zeraora"] = (ColorTranslator.FromHtml("#F6D035"), ColorTranslator.FromHtml("#625315")),
            ["Marshadow"] = (ColorTranslator.FromHtml("#4A4B56"), ColorTranslator.FromHtml("#1D1E22")),
            ["Incineroar"] = (ColorTranslator.FromHtml("#CC2121"), ColorTranslator.FromHtml("#510D0D")),
            ["Decidueye"] = (ColorTranslator.FromHtml("#155C41"), ColorTranslator.FromHtml("#08241A")),
            ["Primarina"] = (ColorTranslator.FromHtml("#54B3D4"), ColorTranslator.FromHtml("#214754")),
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
                    Gradients[parts[0].Trim()] = (ColorTranslator.FromHtml(colors[0].Trim()), ColorTranslator.FromHtml(colors[1].Trim()));
                } catch { }
            }
        }
    }

    public static void ApplyGradient(Form f, string name)
    {
        LoadGradients();
        if (!Gradients.TryGetValue(name, out var colors)) return;
        
        void UpdateBackground()
        {
            if (f.ClientRectangle.Width <= 0 || f.ClientRectangle.Height <= 0) return;
            var bmp = new Bitmap(f.ClientRectangle.Width, f.ClientRectangle.Height);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(f.ClientRectangle, colors.Item1, colors.Item2, 45f))
            {
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
    public static DialogResult Error(params string[] lines)
    {
        System.Media.SystemSounds.Exclamation.Play();
        string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
        return MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static DialogResult Alert(params string[] lines)
    {
        System.Media.SystemSounds.Asterisk.Play();
        string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
        return MessageBox.Show(msg, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static DialogResult Prompt(MessageBoxButtons btn, params string[] lines)
    {
        System.Media.SystemSounds.Question.Play();
        string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
        return MessageBox.Show(msg, "Prompt", btn, MessageBoxIcon.Asterisk);
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