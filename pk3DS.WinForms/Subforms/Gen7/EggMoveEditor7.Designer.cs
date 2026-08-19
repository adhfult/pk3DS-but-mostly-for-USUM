namespace pk3DS.WinForms;

partial class EggMoveEditor7
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
        this.tabControl1 = new System.Windows.Forms.TabControl();
        this.tp_editor = new System.Windows.Forms.TabPage();
        this.tp_changelog = new System.Windows.Forms.TabPage();
        this.RTB_Changelog = new System.Windows.Forms.RichTextBox();
        this.B_ImportJSON = new System.Windows.Forms.Button();
        this.B_ImportTS = new System.Windows.Forms.Button();

        this.dgv = new System.Windows.Forms.DataGridView();
        this.CB_Species = new System.Windows.Forms.ComboBox();
        this.L_Species = new System.Windows.Forms.Label();
        this.B_RandAll = new System.Windows.Forms.Button();
        this.B_Dump = new System.Windows.Forms.Button();
        this.B_AddMove = new System.Windows.Forms.Button();
        this.B_RemoveMove = new System.Windows.Forms.Button();
        this.B_Import = new System.Windows.Forms.Button();
        this.L_TotalMoves = new System.Windows.Forms.Label();
        this.L_STABCount = new System.Windows.Forms.Label();
        this.groupBox1 = new System.Windows.Forms.GroupBox();
        this.L_Moves = new System.Windows.Forms.Label();
        this.NUD_Moves = new System.Windows.Forms.NumericUpDown();
        this.CHK_Expand = new System.Windows.Forms.CheckBox();
        this.L_STAB = new System.Windows.Forms.Label();
        this.NUD_STAB = new System.Windows.Forms.NumericUpDown();
        this.CHK_STAB = new System.Windows.Forms.CheckBox();
        this.PB_MonSprite = new System.Windows.Forms.PictureBox();
        this.NUD_FormTable = new System.Windows.Forms.NumericUpDown();
        this.label1 = new System.Windows.Forms.Label();
        this.B_Goto = new System.Windows.Forms.Button();

        ((System.ComponentModel.ISupportInitialize)(this.dgv)).BeginInit();
        this.groupBox1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_Moves)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_STAB)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_MonSprite)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_FormTable)).BeginInit();
        this.tabControl1.SuspendLayout();
        this.tp_editor.SuspendLayout();
        this.tp_changelog.SuspendLayout();
        this.SuspendLayout();
        
        // 
        // tabControl1
        // 
        this.tabControl1.Controls.Add(this.tp_editor);
        this.tabControl1.Controls.Add(this.tp_changelog);
        this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tabControl1.Location = new System.Drawing.Point(0, 0);
        this.tabControl1.Name = "tabControl1";
        this.tabControl1.SelectedIndex = 0;
        this.tabControl1.Size = new System.Drawing.Size(445, 470);
        this.tabControl1.TabIndex = 100;

        // 
        // tp_editor
        // 
        this.tp_editor.Controls.Add(this.dgv);
        this.tp_editor.Controls.Add(this.CB_Species);
        this.tp_editor.Controls.Add(this.L_Species);
        this.tp_editor.Controls.Add(this.B_RandAll);
        this.tp_editor.Controls.Add(this.B_Dump);
        this.tp_editor.Controls.Add(this.groupBox1);
        this.tp_editor.Controls.Add(this.B_AddMove);
        this.tp_editor.Controls.Add(this.B_RemoveMove);
        this.tp_editor.Controls.Add(this.B_Import);
        this.tp_editor.Controls.Add(this.L_TotalMoves);
        this.tp_editor.Controls.Add(this.L_STABCount);
        this.tp_editor.Controls.Add(this.PB_MonSprite);
        this.tp_editor.Controls.Add(this.NUD_FormTable);
        this.tp_editor.Controls.Add(this.label1);
        this.tp_editor.Controls.Add(this.B_Goto);
        this.tp_editor.Controls.Add(this.B_ImportJSON);
        this.tp_editor.Controls.Add(this.B_ImportTS);
        this.tp_editor.Location = new System.Drawing.Point(4, 22);
        this.tp_editor.Name = "tp_editor";
        this.tp_editor.Padding = new System.Windows.Forms.Padding(3);
        this.tp_editor.Size = new System.Drawing.Size(437, 444);
        this.tp_editor.TabIndex = 0;
        this.tp_editor.Text = "Editor";
        this.tp_editor.UseVisualStyleBackColor = true;

        // 
        // tp_changelog
        // 
        this.tp_changelog.Controls.Add(this.RTB_Changelog);
        this.tp_changelog.Location = new System.Drawing.Point(4, 22);
        this.tp_changelog.Name = "tp_changelog";
        this.tp_changelog.Padding = new System.Windows.Forms.Padding(3);
        this.tp_changelog.Size = new System.Drawing.Size(642, 454);
        this.tp_changelog.TabIndex = 1;
        this.tp_changelog.Text = "Changelog";
        this.tp_changelog.UseVisualStyleBackColor = true;

        // 
        // RTB_Changelog
        // 
        this.RTB_Changelog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.RTB_Changelog.Location = new System.Drawing.Point(3, 3);
        this.RTB_Changelog.Name = "RTB_Changelog";
        this.RTB_Changelog.ReadOnly = true;
        this.RTB_Changelog.Size = new System.Drawing.Size(636, 448);
        this.RTB_Changelog.TabIndex = 0;
        this.RTB_Changelog.Text = "";
        // 
        // dgv
        // 
        this.dgv.AllowUserToResizeColumns = false;
        this.dgv.AllowUserToResizeRows = false;
        this.dgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dgv.Location = new System.Drawing.Point(10, 45);
        this.dgv.Name = "dgv";
        this.dgv.Size = new System.Drawing.Size(235, 390);
        this.dgv.TabIndex = 0;
        // 
        // CB_Species
        // 
        this.CB_Species.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
        this.CB_Species.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
        this.CB_Species.FormattingEnabled = true;
        this.CB_Species.Location = new System.Drawing.Point(65, 10);
        this.CB_Species.Name = "CB_Species";
        this.CB_Species.Size = new System.Drawing.Size(140, 21);
        this.CB_Species.TabIndex = 1;
        this.CB_Species.SelectedIndexChanged += new System.EventHandler(this.ChangeEntry);
        // 
        // L_Species
        // 
        this.L_Species.AutoSize = true;
        this.L_Species.Location = new System.Drawing.Point(10, 13);
        this.L_Species.Name = "L_Species";
        this.L_Species.Size = new System.Drawing.Size(48, 13);
        this.L_Species.TabIndex = 2;
        this.L_Species.Text = "Species:";
        // 
        // PB_MonSprite
        // 
        this.PB_MonSprite.Location = new System.Drawing.Point(210, 6);
        this.PB_MonSprite.Name = "PB_MonSprite";
        this.PB_MonSprite.Size = new System.Drawing.Size(35, 30);
        this.PB_MonSprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_MonSprite.TabIndex = 91;
        this.PB_MonSprite.TabStop = false;
        // 
        // L_TotalMoves
        // 
        this.L_TotalMoves.AutoSize = true;
        this.L_TotalMoves.Location = new System.Drawing.Point(255, 6);
        this.L_TotalMoves.Name = "L_TotalMoves";
        this.L_TotalMoves.Size = new System.Drawing.Size(80, 13);
        this.L_TotalMoves.Text = "Total Moves: 0";
        // 
        // L_STABCount
        // 
        this.L_STABCount.AutoSize = true;
        this.L_STABCount.Location = new System.Drawing.Point(255, 22);
        this.L_STABCount.Name = "L_STABCount";
        this.L_STABCount.Size = new System.Drawing.Size(80, 13);
        this.L_STABCount.Text = "STAB Moves: 0";
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(345, 6);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(30, 13);
        this.label1.Text = "Form";
        // 
        // NUD_FormTable
        // 
        this.NUD_FormTable.Location = new System.Drawing.Point(345, 20);
        this.NUD_FormTable.Name = "NUD_FormTable";
        this.NUD_FormTable.Size = new System.Drawing.Size(45, 20);
        this.NUD_FormTable.TabIndex = 92;
        // 
        // B_Goto
        // 
        this.B_Goto.Location = new System.Drawing.Point(392, 19);
        this.B_Goto.Name = "B_Goto";
        this.B_Goto.Size = new System.Drawing.Size(38, 22);
        this.B_Goto.TabIndex = 94;
        this.B_Goto.Text = "go";
        this.B_Goto.UseVisualStyleBackColor = true;
        this.B_Goto.Click += new System.EventHandler(this.B_Goto_Click);
        // 
        // groupBox1
        // 
        this.groupBox1.Controls.Add(this.L_Moves);
        this.groupBox1.Controls.Add(this.NUD_Moves);
        this.groupBox1.Controls.Add(this.CHK_Expand);
        this.groupBox1.Controls.Add(this.L_STAB);
        this.groupBox1.Controls.Add(this.NUD_STAB);
        this.groupBox1.Controls.Add(this.CHK_STAB);
        this.groupBox1.Location = new System.Drawing.Point(255, 45);
        this.groupBox1.Name = "groupBox1";
        this.groupBox1.Size = new System.Drawing.Size(175, 140);
        this.groupBox1.TabIndex = 7;
        this.groupBox1.TabStop = false;
        this.groupBox1.Text = "Options";
        // 
        // CHK_STAB
        // 
        this.CHK_STAB.AutoSize = true;
        this.CHK_STAB.Location = new System.Drawing.Point(8, 20);
        this.CHK_STAB.Name = "CHK_STAB";
        this.CHK_STAB.Size = new System.Drawing.Size(87, 17);
        this.CHK_STAB.TabIndex = 1;
        this.CHK_STAB.Text = "Bias by Type";
        this.CHK_STAB.UseVisualStyleBackColor = true;
        // 
        // L_STAB
        // 
        this.L_STAB.AutoSize = true;
        this.L_STAB.Location = new System.Drawing.Point(8, 43);
        this.L_STAB.Name = "L_STAB";
        this.L_STAB.Size = new System.Drawing.Size(46, 13);
        this.L_STAB.TabIndex = 3;
        this.L_STAB.Text = "% STAB";
        // 
        // NUD_STAB
        // 
        this.NUD_STAB.Location = new System.Drawing.Point(60, 41);
        this.NUD_STAB.Name = "NUD_STAB";
        this.NUD_STAB.Size = new System.Drawing.Size(40, 20);
        this.NUD_STAB.TabIndex = 2;
        this.NUD_STAB.Value = new decimal(new int[] { 32, 0, 0, 0 });
        // 
        // CHK_Expand
        // 
        this.CHK_Expand.AutoSize = true;
        this.CHK_Expand.Location = new System.Drawing.Point(8, 70);
        this.CHK_Expand.Name = "CHK_Expand";
        this.CHK_Expand.Size = new System.Drawing.Size(86, 17);
        this.CHK_Expand.TabIndex = 8;
        this.CHK_Expand.Text = "Expand Pool";
        this.CHK_Expand.UseVisualStyleBackColor = true;
        // 
        // L_Moves
        // 
        this.L_Moves.AutoSize = true;
        this.L_Moves.Location = new System.Drawing.Point(8, 95);
        this.L_Moves.Name = "L_Moves";
        this.L_Moves.Size = new System.Drawing.Size(42, 13);
        this.L_Moves.TabIndex = 10;
        this.L_Moves.Text = "Moves:";
        // 
        // NUD_Moves
        // 
        this.NUD_Moves.Location = new System.Drawing.Point(60, 93);
        this.NUD_Moves.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
        this.NUD_Moves.Minimum = new decimal(new int[] { 18, 0, 0, 0 });
        this.NUD_Moves.Name = "NUD_Moves";
        this.NUD_Moves.Size = new System.Drawing.Size(40, 20);
        this.NUD_Moves.TabIndex = 9;
        this.NUD_Moves.Value = new decimal(new int[] { 18, 0, 0, 0 });
        // 
        // B_RandAll
        // 
        this.B_RandAll.Location = new System.Drawing.Point(255, 195);
        this.B_RandAll.Name = "B_RandAll";
        this.B_RandAll.Size = new System.Drawing.Size(175, 25);
        this.B_RandAll.TabIndex = 4;
        this.B_RandAll.Text = "Randomize All";
        this.B_RandAll.UseVisualStyleBackColor = true;
        this.B_RandAll.Click += new System.EventHandler(this.B_RandAll_Click);
        // 
        // B_Dump
        // 
        this.B_Dump.Location = new System.Drawing.Point(255, 225);
        this.B_Dump.Name = "B_Dump";
        this.B_Dump.Size = new System.Drawing.Size(175, 25);
        this.B_Dump.TabIndex = 5;
        this.B_Dump.Text = "Dump All";
        this.B_Dump.UseVisualStyleBackColor = true;
        this.B_Dump.Click += new System.EventHandler(this.B_Dump_Click);
        // 
        // B_AddMove
        // 
        this.B_AddMove.Location = new System.Drawing.Point(255, 255);
        this.B_AddMove.Name = "B_AddMove";
        this.B_AddMove.Size = new System.Drawing.Size(84, 25);
        this.B_AddMove.Text = "+ Add";
        this.B_AddMove.Click += new System.EventHandler(this.B_AddMove_Click);
        // 
        // B_RemoveMove
        // 
        this.B_RemoveMove.Location = new System.Drawing.Point(345, 255);
        this.B_RemoveMove.Name = "B_RemoveMove";
        this.B_RemoveMove.Size = new System.Drawing.Size(85, 25);
        this.B_RemoveMove.Text = "- Remove";
        this.B_RemoveMove.Click += new System.EventHandler(this.B_RemoveMove_Click);
        // 
        // B_Import
        // 
        this.B_Import.Location = new System.Drawing.Point(255, 285);
        this.B_Import.Name = "B_Import";
        this.B_Import.Size = new System.Drawing.Size(84, 25);
        this.B_Import.Text = "Import";
        this.B_Import.Click += new System.EventHandler(this.B_Import_Click);
        //
        // B_ImportJSON
        // 
        this.B_ImportJSON.Location = new System.Drawing.Point(255, 315);
        this.B_ImportJSON.Name = "B_ImportJSON";
        this.B_ImportJSON.Size = new System.Drawing.Size(84, 25);
        this.B_ImportJSON.Text = "JSON";
        this.B_ImportJSON.Click += new System.EventHandler(this.B_ImportJSON_Click);
        // 
        // B_ImportTS
        // 
        this.B_ImportTS.Location = new System.Drawing.Point(345, 315);
        this.B_ImportTS.Name = "B_ImportTS";
        this.B_ImportTS.Size = new System.Drawing.Size(85, 25);
        this.B_ImportTS.Text = "TSV";
        this.B_ImportTS.Click += new System.EventHandler(this.B_ImportTS_Click);
        // 
        // EggMoveEditor7
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(445, 470);
        this.Controls.Add(this.tabControl1);
        this.MaximizeBox = false;
        this.MinimumSize = new System.Drawing.Size(322, 450);
        this.Name = "EggMoveEditor7";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Egg Move Editor";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
        ((System.ComponentModel.ISupportInitialize)(this.dgv)).EndInit();
        this.groupBox1.ResumeLayout(false);
        this.groupBox1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_Moves)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_STAB)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_MonSprite)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_FormTable)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TabControl tabControl1;
    private System.Windows.Forms.TabPage tp_editor;
    private System.Windows.Forms.TabPage tp_changelog;
    private System.Windows.Forms.RichTextBox RTB_Changelog;
    private System.Windows.Forms.Button B_ImportJSON;
    private System.Windows.Forms.Button B_ImportTS;

    private System.Windows.Forms.DataGridView dgv;
    private System.Windows.Forms.ComboBox CB_Species;
    private System.Windows.Forms.Label L_Species;
    private System.Windows.Forms.Button B_RandAll;
    private System.Windows.Forms.Button B_Dump;
    private System.Windows.Forms.GroupBox groupBox1;
    private System.Windows.Forms.Label L_Moves;
    private System.Windows.Forms.NumericUpDown NUD_Moves;
    private System.Windows.Forms.CheckBox CHK_Expand;
    private System.Windows.Forms.Label L_STAB;
    private System.Windows.Forms.NumericUpDown NUD_STAB;
    private System.Windows.Forms.CheckBox CHK_STAB;
    private System.Windows.Forms.PictureBox PB_MonSprite;
    private System.Windows.Forms.NumericUpDown NUD_FormTable;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.Button B_Goto;
    private System.Windows.Forms.Button B_AddMove;
    private System.Windows.Forms.Button B_RemoveMove;
    private System.Windows.Forms.Button B_Import;
    private System.Windows.Forms.Label L_TotalMoves;
    private System.Windows.Forms.Label L_STABCount;
}