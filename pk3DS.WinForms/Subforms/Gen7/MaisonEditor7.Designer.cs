namespace pk3DS.WinForms;

partial class MaisonEditor7
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.CB_Trainer = new System.Windows.Forms.ComboBox();
        this.L_Trainer = new System.Windows.Forms.Label();
        this.DumpTRs = new System.Windows.Forms.Button();
        this.B_AssignList = new System.Windows.Forms.Button();
        this.L_Pokemon = new System.Windows.Forms.Label();
        this.CB_Pokemon = new System.Windows.Forms.ComboBox();
        this.B_DumpPKs = new System.Windows.Forms.Button();
        this.B_ImportPKs = new System.Windows.Forms.Button();
        this.GB_Trainer = new System.Windows.Forms.GroupBox();
        this.L_Class = new System.Windows.Forms.Label();
        this.CB_Class = new System.Windows.Forms.ComboBox();
        this.LB_Choices = new System.Windows.Forms.ListBox();
        this.B_Set = new System.Windows.Forms.Button();
        this.B_Remove = new System.Windows.Forms.Button();
        this.GB_Pokemon = new System.Windows.Forms.GroupBox();
        this.PB_PKM = new System.Windows.Forms.PictureBox();
        this.L_Species = new System.Windows.Forms.Label();
        this.CB_Species = new System.Windows.Forms.ComboBox();
        this.L_Moves = new System.Windows.Forms.Label();
        this.CB_Move1 = new System.Windows.Forms.ComboBox();
        this.CB_Move2 = new System.Windows.Forms.ComboBox();
        this.CB_Move3 = new System.Windows.Forms.ComboBox();
        this.CB_Move4 = new System.Windows.Forms.ComboBox();
        this.L_Nature = new System.Windows.Forms.Label();
        this.CB_Nature = new System.Windows.Forms.ComboBox();
        this.L_Item = new System.Windows.Forms.Label();
        this.CB_Item = new System.Windows.Forms.ComboBox();
        this.L_Form = new System.Windows.Forms.Label();
        this.NUD_Form = new System.Windows.Forms.NumericUpDown();
        this.CHK_HP = new System.Windows.Forms.CheckBox();
        this.CHK_ATK = new System.Windows.Forms.CheckBox();
        this.CHK_DEF = new System.Windows.Forms.CheckBox();
        this.CHK_SpA = new System.Windows.Forms.CheckBox();
        this.CHK_SpD = new System.Windows.Forms.CheckBox();
        this.CHK_Spe = new System.Windows.Forms.CheckBox();
        this.LB_StoredSets = new System.Windows.Forms.ListBox();
        this.B_ShowdownStorage = new System.Windows.Forms.Button();
        this.B_ShowdownImport = new System.Windows.Forms.Button();
        this.B_ShowdownBox = new System.Windows.Forms.Button();
        this.B_ExportTeam = new System.Windows.Forms.Button();
        this.PB_Mascot = new System.Windows.Forms.PictureBox();

        this.GB_Trainer.SuspendLayout();
        this.GB_Pokemon.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_Form)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_PKM)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_Mascot)).BeginInit();
        this.SuspendLayout();

        // 
        // L_Trainer & CB_Trainer
        // 
        this.L_Trainer.Location = new System.Drawing.Point(5, 10);
        this.L_Trainer.Size = new System.Drawing.Size(25, 13);
        this.L_Trainer.Text = "TR:";
        this.CB_Trainer.Location = new System.Drawing.Point(35, 7);
        this.CB_Trainer.Size = new System.Drawing.Size(120, 21);
        this.CB_Trainer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.CB_Trainer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Trainer.SelectedIndexChanged += new System.EventHandler(this.ChangeTrainer);

        this.DumpTRs.Location = new System.Drawing.Point(160, 6);
        this.DumpTRs.Size = new System.Drawing.Size(55, 23);
        this.DumpTRs.Text = "Dump";
        this.DumpTRs.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.DumpTRs.Click += new System.EventHandler(this.DumpTRs_Click);

        this.B_AssignList.Location = new System.Drawing.Point(220, 6);
        this.B_AssignList.Size = new System.Drawing.Size(55, 23);
        this.B_AssignList.Text = "Assign";
        this.B_AssignList.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_AssignList.Click += new System.EventHandler(this.B_AssignList_Click);

        // 
        // L_Pokemon & CB_Pokemon
        // 
        this.L_Pokemon.Location = new System.Drawing.Point(285, 10);
        this.L_Pokemon.Size = new System.Drawing.Size(25, 13);
        this.L_Pokemon.Text = "PK:";
        this.CB_Pokemon.Location = new System.Drawing.Point(315, 7);
        this.CB_Pokemon.Size = new System.Drawing.Size(120, 21);
        this.CB_Pokemon.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.CB_Pokemon.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Pokemon.SelectedIndexChanged += new System.EventHandler(this.ChangePokemon);

        this.B_DumpPKs.Location = new System.Drawing.Point(440, 6);
        this.B_DumpPKs.Size = new System.Drawing.Size(55, 23);
        this.B_DumpPKs.Text = "Dump";
        this.B_DumpPKs.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_DumpPKs.Click += new System.EventHandler(this.B_DumpPKs_Click);

        this.B_ImportPKs.Location = new System.Drawing.Point(500, 6);
        this.B_ImportPKs.Size = new System.Drawing.Size(55, 23);
        this.B_ImportPKs.Text = "Import";
        this.B_ImportPKs.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_ImportPKs.Click += new System.EventHandler(this.B_ImportPKs_Click);

        // 
        // GB_Trainer
        // 
        this.GB_Trainer.Controls.Add(this.L_Class);
        this.GB_Trainer.Controls.Add(this.CB_Class);
        this.GB_Trainer.Controls.Add(this.LB_Choices);
        this.GB_Trainer.Controls.Add(this.B_Set);
        this.GB_Trainer.Controls.Add(this.B_Remove);
        this.GB_Trainer.Location = new System.Drawing.Point(5, 35);
        this.GB_Trainer.Size = new System.Drawing.Size(300, 240);
        this.GB_Trainer.Text = "Trainer Data";

        this.L_Class.Location = new System.Drawing.Point(10, 23);
        this.L_Class.Size = new System.Drawing.Size(40, 13);
        this.L_Class.Text = "Class:";
        this.CB_Class.Location = new System.Drawing.Point(55, 20);
        this.CB_Class.Size = new System.Drawing.Size(235, 21);
        this.CB_Class.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

        this.LB_Choices.Location = new System.Drawing.Point(10, 50);
        this.LB_Choices.Size = new System.Drawing.Size(200, 180);
        this.LB_Choices.SelectedIndexChanged += new System.EventHandler(this.B_View_Click);
        this.B_Set.Location = new System.Drawing.Point(220, 50);
        this.B_Set.Size = new System.Drawing.Size(70, 30);
        this.B_Set.Text = "[<] Set";
        this.B_Set.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_Set.Click += new System.EventHandler(this.B_Set_Click);
        this.B_Remove.Location = new System.Drawing.Point(220, 200);
        this.B_Remove.Size = new System.Drawing.Size(70, 30);
        this.B_Remove.Text = "[X] Del";
        this.B_Remove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_Remove.Click += new System.EventHandler(this.B_Remove_Click);

        // 
        // GB_Pokemon
        // 
        this.GB_Pokemon.Controls.Add(this.PB_PKM);
        this.GB_Pokemon.Controls.Add(this.L_Species);
        this.GB_Pokemon.Controls.Add(this.CB_Species);
        this.GB_Pokemon.Controls.Add(this.L_Moves);
        this.GB_Pokemon.Controls.Add(this.CB_Move1);
        this.GB_Pokemon.Controls.Add(this.CB_Move2);
        this.GB_Pokemon.Controls.Add(this.CB_Move3);
        this.GB_Pokemon.Controls.Add(this.CB_Move4);
        this.GB_Pokemon.Controls.Add(this.L_Nature);
        this.GB_Pokemon.Controls.Add(this.CB_Nature);
        this.GB_Pokemon.Controls.Add(this.L_Item);
        this.GB_Pokemon.Controls.Add(this.CB_Item);
        this.GB_Pokemon.Controls.Add(this.L_Form);
        this.GB_Pokemon.Controls.Add(this.NUD_Form);
        this.GB_Pokemon.Controls.Add(this.CHK_HP);
        this.GB_Pokemon.Controls.Add(this.CHK_ATK);
        this.GB_Pokemon.Controls.Add(this.CHK_DEF);
        this.GB_Pokemon.Controls.Add(this.CHK_SpA);
        this.GB_Pokemon.Controls.Add(this.CHK_SpD);
        this.GB_Pokemon.Controls.Add(this.CHK_Spe);
        this.GB_Pokemon.Location = new System.Drawing.Point(310, 35);
        this.GB_Pokemon.Size = new System.Drawing.Size(305, 240);
        this.GB_Pokemon.Text = "Pokémon Data";

        this.PB_PKM.Location = new System.Drawing.Point(245, 10);
        this.PB_PKM.Size = new System.Drawing.Size(50, 40);
        this.PB_PKM.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;

        this.L_Species.Location = new System.Drawing.Point(10, 23);
        this.L_Species.Size = new System.Drawing.Size(40, 13);
        this.L_Species.Text = "Sp:";
        this.CB_Species.Location = new System.Drawing.Point(55, 20);
        this.CB_Species.Size = new System.Drawing.Size(180, 21);
        this.CB_Species.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Species.SelectedIndexChanged += new System.EventHandler(this.ChangeSpecies);

        this.L_Moves.Location = new System.Drawing.Point(10, 45);
        this.L_Moves.Size = new System.Drawing.Size(50, 13);
        this.L_Moves.Text = "Moves:";
        this.CB_Move1.Location = new System.Drawing.Point(10, 60);
        this.CB_Move1.Size = new System.Drawing.Size(140, 21);
        this.CB_Move1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Move2.Location = new System.Drawing.Point(155, 60);
        this.CB_Move2.Size = new System.Drawing.Size(140, 21);
        this.CB_Move2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Move3.Location = new System.Drawing.Point(10, 85);
        this.CB_Move3.Size = new System.Drawing.Size(140, 21);
        this.CB_Move3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.CB_Move4.Location = new System.Drawing.Point(155, 85);
        this.CB_Move4.Size = new System.Drawing.Size(140, 21);
        this.CB_Move4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

        this.L_Nature.Location = new System.Drawing.Point(10, 115);
        this.L_Nature.Size = new System.Drawing.Size(40, 13);
        this.L_Nature.Text = "Nat:";
        this.CB_Nature.Location = new System.Drawing.Point(55, 112);
        this.CB_Nature.Size = new System.Drawing.Size(95, 21);
        this.CB_Nature.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

        this.L_Item.Location = new System.Drawing.Point(155, 115);
        this.L_Item.Size = new System.Drawing.Size(40, 13);
        this.L_Item.Text = "Item:";
        this.CB_Item.Location = new System.Drawing.Point(195, 112);
        this.CB_Item.Size = new System.Drawing.Size(100, 21);
        this.CB_Item.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

        this.L_Form.Location = new System.Drawing.Point(10, 140);
        this.L_Form.Size = new System.Drawing.Size(40, 13);
        this.L_Form.Text = "Form:";
        this.NUD_Form.Location = new System.Drawing.Point(55, 138);
        this.NUD_Form.Size = new System.Drawing.Size(50, 20);
        this.NUD_Form.ValueChanged += new System.EventHandler(this.ChangeSpecies);

        this.CHK_HP.Location = new System.Drawing.Point(10, 165);
        this.CHK_HP.Size = new System.Drawing.Size(45, 17);
        this.CHK_HP.Text = "HP";
        this.CHK_ATK.Location = new System.Drawing.Point(60, 165);
        this.CHK_ATK.Size = new System.Drawing.Size(45, 17);
        this.CHK_ATK.Text = "Atk";
        this.CHK_DEF.Location = new System.Drawing.Point(110, 165);
        this.CHK_DEF.Size = new System.Drawing.Size(45, 17);
        this.CHK_DEF.Text = "Def";
        this.CHK_SpA.Location = new System.Drawing.Point(10, 190);
        this.CHK_SpA.Size = new System.Drawing.Size(45, 17);
        this.CHK_SpA.Text = "SpA";
        this.CHK_SpD.Location = new System.Drawing.Point(60, 190);
        this.CHK_SpD.Size = new System.Drawing.Size(45, 17);
        this.CHK_SpD.Text = "SpD";
        this.CHK_Spe.Location = new System.Drawing.Point(110, 190);
        this.CHK_Spe.Size = new System.Drawing.Size(45, 17);
        this.CHK_Spe.Text = "Spe";

        // 
        // Bottom Tools
        // 
        this.LB_StoredSets.Location = new System.Drawing.Point(5, 295);
        this.LB_StoredSets.Size = new System.Drawing.Size(455, 145);
        this.LB_StoredSets.SelectedIndexChanged += new System.EventHandler(this.LB_StoredSets_SelectedIndexChanged);
        this.LB_StoredSets.FormattingEnabled = true;

        this.L_StoredSets = new System.Windows.Forms.Label();
        this.L_StoredSets.Location = new System.Drawing.Point(5, 280);
        this.L_StoredSets.Size = new System.Drawing.Size(200, 13);
        this.L_StoredSets.Text = "Showdown Quick-Sets (Click to Apply):";
        this.L_StoredSets.ForeColor = System.Drawing.Color.Cyan;
        this.Controls.Add(this.L_StoredSets);

        this.B_ShowdownStorage.Location = new System.Drawing.Point(465, 280);
        this.B_ShowdownStorage.Size = new System.Drawing.Size(150, 30);
        this.B_ShowdownStorage.Text = "Showdown Storage";
        this.B_ShowdownStorage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_ShowdownStorage.Click += new System.EventHandler(this.B_ShowdownStorage_Click);

        this.B_ShowdownImport.Location = new System.Drawing.Point(465, 315);
        this.B_ShowdownImport.Size = new System.Drawing.Size(150, 30);
        this.B_ShowdownImport.Text = "Import (Set)";
        this.B_ShowdownImport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_ShowdownImport.Click += new System.EventHandler(this.B_ShowdownImport_Click);

        this.B_ShowdownBox.Location = new System.Drawing.Point(465, 350);
        this.B_ShowdownBox.Size = new System.Drawing.Size(150, 30);
        this.B_ShowdownBox.Text = "Import (Box)";
        this.B_ShowdownBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_ShowdownBox.Click += new System.EventHandler(this.B_ShowdownBox_Click);

        this.B_ExportTeam.Location = new System.Drawing.Point(465, 385);
        this.B_ExportTeam.Size = new System.Drawing.Size(150, 30);
        this.B_ExportTeam.Text = "Export Team";
        this.B_ExportTeam.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.B_ExportTeam.Click += new System.EventHandler(this.B_ExportTeam_Click);

        this.PB_Mascot.Location = new System.Drawing.Point(565, 425);
        this.PB_Mascot.Size = new System.Drawing.Size(50, 50);
        this.PB_Mascot.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;

        // 
        // Maison Editor Form
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(620, 480);
        this.Controls.Add(this.L_Trainer);
        this.Controls.Add(this.CB_Trainer);
        this.Controls.Add(this.DumpTRs);
        this.Controls.Add(this.B_AssignList);
        this.Controls.Add(this.L_Pokemon);
        this.Controls.Add(this.CB_Pokemon);
        this.Controls.Add(this.B_DumpPKs);
        this.Controls.Add(this.B_ImportPKs);
        this.Controls.Add(this.GB_Trainer);
        this.Controls.Add(this.GB_Pokemon);
        this.Controls.Add(this.LB_StoredSets);
        this.Controls.Add(this.B_ShowdownStorage);
        this.Controls.Add(this.B_ShowdownImport);
        this.Controls.Add(this.B_ShowdownBox);
        this.Controls.Add(this.B_ExportTeam);
        this.Controls.Add(this.PB_Mascot);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "MaisonEditor7";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Maison Editor";

        this.GB_Trainer.ResumeLayout(false);
        this.GB_Trainer.PerformLayout();
        this.GB_Pokemon.ResumeLayout(false);
        this.GB_Pokemon.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_Form)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_PKM)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_Mascot)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.ComboBox CB_Trainer;
    private System.Windows.Forms.Label L_Trainer;
    private System.Windows.Forms.Button DumpTRs;
    private System.Windows.Forms.Button B_AssignList;
    private System.Windows.Forms.Label L_Pokemon;
    private System.Windows.Forms.ComboBox CB_Pokemon;
    private System.Windows.Forms.Button B_DumpPKs;
    private System.Windows.Forms.Button B_ImportPKs;
    private System.Windows.Forms.GroupBox GB_Trainer;
    private System.Windows.Forms.Label L_Class;
    private System.Windows.Forms.ComboBox CB_Class;
    private System.Windows.Forms.ListBox LB_Choices;
    private System.Windows.Forms.Button B_Set;
    private System.Windows.Forms.Button B_Remove;
    private System.Windows.Forms.GroupBox GB_Pokemon;
    private System.Windows.Forms.PictureBox PB_PKM;
    private System.Windows.Forms.Label L_Species;
    private System.Windows.Forms.ComboBox CB_Species;
    private System.Windows.Forms.Label L_Moves;
    private System.Windows.Forms.ComboBox CB_Move1;
    private System.Windows.Forms.ComboBox CB_Move2;
    private System.Windows.Forms.ComboBox CB_Move3;
    private System.Windows.Forms.ComboBox CB_Move4;
    private System.Windows.Forms.Label L_Nature;
    private System.Windows.Forms.ComboBox CB_Nature;
    private System.Windows.Forms.Label L_Item;
    private System.Windows.Forms.ComboBox CB_Item;
    private System.Windows.Forms.Label L_Form;
    private System.Windows.Forms.NumericUpDown NUD_Form;
    private System.Windows.Forms.CheckBox CHK_HP;
    private System.Windows.Forms.CheckBox CHK_ATK;
    private System.Windows.Forms.CheckBox CHK_DEF;
    private System.Windows.Forms.CheckBox CHK_SpA;
    private System.Windows.Forms.CheckBox CHK_SpD;
    private System.Windows.Forms.CheckBox CHK_Spe;
    private System.Windows.Forms.ListBox LB_StoredSets;
    private System.Windows.Forms.Button B_ShowdownStorage;
    private System.Windows.Forms.Button B_ShowdownImport;
    private System.Windows.Forms.Button B_ShowdownBox;
    private System.Windows.Forms.Button B_ExportTeam;
    private System.Windows.Forms.PictureBox PB_Mascot;
    private System.Windows.Forms.Label L_StoredSets;
}