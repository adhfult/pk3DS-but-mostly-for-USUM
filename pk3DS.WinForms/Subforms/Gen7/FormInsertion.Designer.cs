namespace pk3DS.WinForms;

partial class FormInsertion
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.L_TargetSpecies = new System.Windows.Forms.Label();
        this.CB_TargetSpecies = new System.Windows.Forms.ComboBox();
        this.L_FormCount = new System.Windows.Forms.Label();
        this.NUD_FormCount = new System.Windows.Forms.NumericUpDown();
        this.L_CopyFrom = new System.Windows.Forms.Label();
        this.CB_CopyFrom = new System.Windows.Forms.ComboBox();
        this.B_Insert = new System.Windows.Forms.Button();
        this.B_Cancel = new System.Windows.Forms.Button();
        this.CHK_Batch = new System.Windows.Forms.CheckBox();
        this.CB_TargetSpeciesEnd = new System.Windows.Forms.ComboBox();
        this.L_To = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_FormCount)).BeginInit();
        this.SuspendLayout();

        // L_TargetSpecies
        this.L_TargetSpecies.Location = new System.Drawing.Point(12, 15);
        this.L_TargetSpecies.Size = new System.Drawing.Size(100, 13);
        this.L_TargetSpecies.Text = "Target Species:";

        // CB_TargetSpecies
        this.CB_TargetSpecies.Location = new System.Drawing.Point(120, 12);
        this.CB_TargetSpecies.Size = new System.Drawing.Size(140, 21);
        this.CB_TargetSpecies.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
        this.CB_TargetSpecies.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;

        // L_To
        this.L_To.Location = new System.Drawing.Point(265, 15);
        this.L_To.Size = new System.Drawing.Size(20, 13);
        this.L_To.Text = "to";
        this.L_To.Visible = false;

        // CB_TargetSpeciesEnd
        this.CB_TargetSpeciesEnd.Location = new System.Drawing.Point(290, 12);
        this.CB_TargetSpeciesEnd.Size = new System.Drawing.Size(140, 21);
        this.CB_TargetSpeciesEnd.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
        this.CB_TargetSpeciesEnd.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
        this.CB_TargetSpeciesEnd.Visible = false;

        // L_FormCount
        this.L_FormCount.Location = new System.Drawing.Point(12, 45);
        this.L_FormCount.Size = new System.Drawing.Size(100, 13);
        this.L_FormCount.Text = "Forms to Add:";

        // NUD_FormCount
        this.NUD_FormCount.Location = new System.Drawing.Point(120, 42);
        this.NUD_FormCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.NUD_FormCount.Value = new decimal(new int[] { 1, 0, 0, 0 });

        // L_CopyFrom
        this.L_CopyFrom.Location = new System.Drawing.Point(12, 75);
        this.L_CopyFrom.Size = new System.Drawing.Size(100, 13);
        this.L_CopyFrom.Text = "Copy Template:";

        // CB_CopyFrom
        this.CB_CopyFrom.Location = new System.Drawing.Point(120, 72);
        this.CB_CopyFrom.Size = new System.Drawing.Size(200, 21);
        this.CB_CopyFrom.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
        this.CB_CopyFrom.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;


        // CHK_Batch
        this.CHK_Batch.Location = new System.Drawing.Point(120, 125);
        this.CHK_Batch.Size = new System.Drawing.Size(150, 24);
        this.CHK_Batch.Text = "Batch Mode (Range)";
        this.CHK_Batch.CheckedChanged += (s, e) => {
            this.L_To.Visible = this.CB_TargetSpeciesEnd.Visible = this.CHK_Batch.Checked;
            this.CB_TargetSpecies.Width = this.CHK_Batch.Checked ? 140 : 200;
            if (this.CHK_Batch.Checked) this.CHK_BatchList.Checked = false;
        };

        // CHK_BatchList
        this.CHK_BatchList = new System.Windows.Forms.CheckBox();
        this.CHK_BatchList.Location = new System.Drawing.Point(270, 125);
        this.CHK_BatchList.Size = new System.Drawing.Size(150, 24);
        this.CHK_BatchList.Text = "Batch Mode (List)";
        this.CHK_BatchList.CheckedChanged += (s, e) => {
            this.RTB_BatchList.Visible = this.CHK_BatchList.Checked;
            this.ClientSize = new System.Drawing.Size(450, this.CHK_BatchList.Checked ? 420 : 215);
            if (this.CHK_BatchList.Checked) this.CHK_Batch.Checked = false;
        };

        // RTB_BatchList
        this.RTB_BatchList = new System.Windows.Forms.RichTextBox();
        this.RTB_BatchList.Location = new System.Drawing.Point(12, 160);
        this.RTB_BatchList.Size = new System.Drawing.Size(426, 200);
        this.RTB_BatchList.Visible = false;

        // B_Insert
        this.B_Insert.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
        this.B_Insert.Location = new System.Drawing.Point(145, 175);
        this.B_Insert.Size = new System.Drawing.Size(75, 25);
        this.B_Insert.Text = "Insert";
        this.B_Insert.Click += new System.EventHandler(this.B_Insert_Click);

        // B_Cancel
        this.B_Cancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
        this.B_Cancel.Location = new System.Drawing.Point(230, 175);
        this.B_Cancel.Size = new System.Drawing.Size(75, 25);
        this.B_Cancel.Text = "Cancel";
        this.B_Cancel.Click += (s, e) => this.Close();

        this.ClientSize = new System.Drawing.Size(450, 215);
        this.Controls.Add(this.CHK_BatchList);
        this.Controls.Add(this.RTB_BatchList);
        this.Controls.Add(this.L_TargetSpecies);
        this.Controls.Add(this.CB_TargetSpecies);
        this.Controls.Add(this.L_To);
        this.Controls.Add(this.CB_TargetSpeciesEnd);
        this.Controls.Add(this.L_FormCount);
        this.Controls.Add(this.NUD_FormCount);
        this.Controls.Add(this.L_CopyFrom);
        this.Controls.Add(this.CB_CopyFrom);
        this.Controls.Add(this.CHK_Batch);
        this.Controls.Add(this.B_Insert);
        this.Controls.Add(this.B_Cancel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "FormInsertion";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Form Insertion Tool";
        ((System.ComponentModel.ISupportInitialize)(this.NUD_FormCount)).EndInit();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.Label L_TargetSpecies;
    private System.Windows.Forms.ComboBox CB_TargetSpecies;
    private System.Windows.Forms.Label L_To;
    private System.Windows.Forms.ComboBox CB_TargetSpeciesEnd;
    private System.Windows.Forms.Label L_FormCount;
    private System.Windows.Forms.NumericUpDown NUD_FormCount;
    private System.Windows.Forms.Label L_CopyFrom;
    private System.Windows.Forms.ComboBox CB_CopyFrom;
    private System.Windows.Forms.Button B_Insert;
    private System.Windows.Forms.Button B_Cancel;
    private System.Windows.Forms.CheckBox CHK_Batch;
    private System.Windows.Forms.CheckBox CHK_BatchList;
    private System.Windows.Forms.RichTextBox RTB_BatchList;
}
