using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Modding;

namespace pk3DS.WinForms;

/// <summary>
/// Assigns an unused move flag to an ability and a power multiplier.
/// </summary>
public sealed class AbilityFlagBindingDialog : Form
{
    private readonly ComboBox CB_Trigger;
    private readonly ComboBox CB_Ability;
    private readonly TextBox TB_Label;
    private readonly NumericUpDown NUD_Multiplier;
    private readonly CheckBox CHK_NameOnly;
    private readonly string[] abilities;
    private readonly string[] items;

    public FlagTrigger Trigger { get; private set; } = FlagTrigger.Ability;
    public int TriggerId { get; private set; } = -1;
    public string TriggerName { get; private set; } = "";
    public double Multiplier { get; private set; } = 1.3;
    public string Label { get; private set; } = "";
    public bool ClearBinding { get; private set; }

    public AbilityFlagBindingDialog(int bit, string[] abilityList, string[] itemList)
    {
        abilities = abilityList ?? [];
        items = itemList ?? [];
        Text = $"Bind Move Flag F{bit + 1}";
        ClientSize = new Size(430, 262);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;

        var lblIntro = new Label
        {
            Location = new Point(14, 12),
            Size = new Size(400, 32),
            Text = "Moves carrying this flag form a category. The ability below reads that "
                 + "category and multiplies the move's power.",
        };

        var lblName = new Label { Location = new Point(14, 56), Size = new Size(110, 20), Text = "Category name:" };
        TB_Label = new TextBox { Location = new Point(130, 53), Size = new Size(280, 22) };

        var lblTrigger = new Label { Location = new Point(14, 88), Size = new Size(110, 20), Text = "Read by:" };
        CB_Trigger = new ComboBox
        {
            Location = new Point(130, 85),
            Size = new Size(140, 22),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        CB_Trigger.Items.AddRange(["Ability", "Held item"]);
        CB_Trigger.SelectedIndex = 0;

        var lblAbility = new Label { Location = new Point(14, 120), Size = new Size(110, 20), Text = "Name:" };
        CB_Ability = new ComboBox
        {
            Location = new Point(130, 117),
            Size = new Size(280, 22),
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
        };

        // Swapping the trigger repopulates the list, since an ability index and an item index are
        // not interchangeable — keeping the old selection would bind to the wrong thing entirely.
        void FillTriggerList()
        {
            string previous = CB_Ability.Text;
            CB_Ability.Items.Clear();
            var source = CB_Trigger.SelectedIndex == 1 ? items : abilities;
            CB_Ability.Items.AddRange(source.Cast<object>().ToArray());
            CB_Ability.Text = CB_Ability.Items.Cast<string>().Any(s => s == previous) ? previous : "";
        }
        CB_Trigger.SelectedIndexChanged += (_, _) => FillTriggerList();
        FillTriggerList();

        var lblMult = new Label { Location = new Point(14, 152), Size = new Size(110, 20), Text = "Power multiplier:" };
        NUD_Multiplier = new NumericUpDown
        {
            Location = new Point(130, 149),
            Size = new Size(80, 22),
            DecimalPlaces = 2,
            Increment = 0.05M,
            Minimum = 0.10M,
            Maximum = 4.00M,
            Value = 1.30M,
        };

        CHK_NameOnly = new CheckBox
        {
            Location = new Point(130, 180),
            Size = new Size(280, 20),
            Text = "Name only (nothing acts on it)",
        };
        CHK_NameOnly.CheckedChanged += (_, _) =>
        {
            CB_Trigger.Enabled = !CHK_NameOnly.Checked;
            CB_Ability.Enabled = !CHK_NameOnly.Checked;
            NUD_Multiplier.Enabled = !CHK_NameOnly.Checked;
        };

        var bOK = new Button { Location = new Point(150, 212), Size = new Size(85, 28), Text = "Save" };
        var bClear = new Button { Location = new Point(245, 212), Size = new Size(85, 28), Text = "Unbind" };
        var bCancel = new Button { Location = new Point(340, 212), Size = new Size(75, 28), Text = "Cancel", DialogResult = DialogResult.Cancel };

        bOK.Click += (_, _) =>
        {
            Label = TB_Label.Text.Trim();
            if (Label.Length == 0)
            {
                WinFormsUtil.Alert("Give the category a name.");
                return;
            }

            if (CHK_NameOnly.Checked)
            {
                Trigger = FlagTrigger.None;
                TriggerId = -1;
                TriggerName = "";
            }
            else
            {
                Trigger = CB_Trigger.SelectedIndex == 1 ? FlagTrigger.Item : FlagTrigger.Ability;

                // Match the typed text against the list rather than trusting SelectedIndex, so a
                // name completed by the autocomplete still resolves.
                string wanted = CB_Ability.Text.Trim();
                TriggerId = CB_Ability.Items.Cast<string>()
                    .Select((s, i) => (s, i))
                    .Where(t => string.Equals(t.s, wanted, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.i)
                    .DefaultIfEmpty(-1)
                    .First();

                if (TriggerId < 0)
                {
                    WinFormsUtil.Alert($"Pick a{(Trigger == FlagTrigger.Item ? "n item" : "n ability")} from the list.",
                        "Tick \"Name only\" if this flag should just carry a label for now.");
                    return;
                }
                TriggerName = CB_Ability.Items[TriggerId].ToString();
            }

            Multiplier = (double)NUD_Multiplier.Value;
            DialogResult = DialogResult.OK;
            Close();
        };

        bClear.Click += (_, _) =>
        {
            ClearBinding = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange([lblIntro, lblName, TB_Label, lblTrigger, CB_Trigger, lblAbility, CB_Ability,
                           lblMult, NUD_Multiplier, CHK_NameOnly, bOK, bClear, bCancel]);
        AcceptButton = bOK;
        CancelButton = bCancel;

        // Preload whatever this flag is already set to.
        var existing = AbilityMoveFlags.Get(bit);
        if (existing == null) return;

        TB_Label.Text = existing.Label;
        NUD_Multiplier.Value = (decimal)Math.Clamp(existing.Multiplier, 0.10, 4.00);
        CHK_NameOnly.Checked = !existing.IsBound;
        if (existing.IsBound)
        {
            CB_Trigger.SelectedIndex = existing.Trigger == FlagTrigger.Item ? 1 : 0;
            FillTriggerList();
            if (existing.TriggerId < CB_Ability.Items.Count)
                CB_Ability.SelectedIndex = existing.TriggerId;
        }
    }
}
