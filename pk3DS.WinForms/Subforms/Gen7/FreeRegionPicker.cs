using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pk3DS.Core.Modding;

namespace pk3DS.WinForms;

/// <summary>
/// Shows where a patch could be written, so the offset does not have to be worked out by hand.
/// </summary>
public sealed class FreeRegionPicker : Form
{
    private readonly ListBox LB_Regions;

    /// <summary>The region the user chose, or null if they cancelled.</summary>
    public AbilityFlagPatcher.FreeRegion Selected { get; private set; }

    public FreeRegionPicker(List<AbilityFlagPatcher.FreeRegion> regions, int neededBytes, string fileName)
    {
        Text = $"Free Space in {fileName}";
        ClientSize = new Size(470, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;

        var lblIntro = new Label
        {
            Location = new Point(14, 12),
            Size = new Size(440, 34),
            Text = $"The table needs {neededBytes} bytes. These regions in {fileName} are large "
                 + "enough, best candidate first.",
        };

        LB_Regions = new ListBox
        {
            Location = new Point(14, 52),
            Size = new Size(440, 160),
            Font = new Font("Consolas", 9F),
        };
        foreach (var r in regions) LB_Regions.Items.Add(r);
        if (LB_Regions.Items.Count > 0) LB_Regions.SelectedIndex = 0;

        var lblNote = new Label
        {
            Location = new Point(14, 220),
            Size = new Size(440, 58),
            ForeColor = Color.IndianRed,
            Text = "A run of identical bytes is not proof the space is unused — zeroed game data "
                 + "looks the same. Prefer a previous table or trailing padding. The write is "
                 + "confirmed again before it happens, and the original is kept as .orig.",
        };

        var bOK = new Button { Location = new Point(250, 288), Size = new Size(95, 28), Text = "Use This" };
        var bCancel = new Button { Location = new Point(355, 288), Size = new Size(95, 28), Text = "Cancel", DialogResult = DialogResult.Cancel };

        bOK.Click += (_, _) =>
        {
            Selected = LB_Regions.SelectedItem as AbilityFlagPatcher.FreeRegion;
            if (Selected == null)
            {
                WinFormsUtil.Alert("Select a region first.");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange([lblIntro, LB_Regions, lblNote, bOK, bCancel]);
        AcceptButton = bOK;
        CancelButton = bCancel;
    }
}
