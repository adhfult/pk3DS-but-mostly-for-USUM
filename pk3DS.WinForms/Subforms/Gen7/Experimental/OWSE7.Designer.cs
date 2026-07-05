namespace pk3DS.WinForms
{
    partial class OWSE7
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            L_Location = new System.Windows.Forms.Label();
            CB_LocationID = new System.Windows.Forms.ComboBox();
            tabControl1 = new System.Windows.Forms.TabControl();
            tab_7_ZS = new System.Windows.Forms.TabPage();
            L_7_Info = new System.Windows.Forms.Label();
            RTB_7_Parse = new System.Windows.Forms.RichTextBox();
            NUD_7_Count = new System.Windows.Forms.NumericUpDown();
            L_7_Count = new System.Windows.Forms.Label();
            RTB_7_Script = new System.Windows.Forms.RichTextBox();
            RTB_7_Raw = new System.Windows.Forms.RichTextBox();
            tab_8_ZI = new System.Windows.Forms.TabPage();
            L_8_Info = new System.Windows.Forms.Label();
            RTB_8_Parse = new System.Windows.Forms.RichTextBox();
            NUD_8_Count = new System.Windows.Forms.NumericUpDown();
            L_8_Count = new System.Windows.Forms.Label();
            RTB_8_Script = new System.Windows.Forms.RichTextBox();
            RTB_8_Raw = new System.Windows.Forms.RichTextBox();
            tabPage1 = new System.Windows.Forms.TabPage();
            button1 = new System.Windows.Forms.Button();
            dgv = new System.Windows.Forms.DataGridView();
            dgvIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            dgvItem = new System.Windows.Forms.DataGridViewComboBoxColumn();
            tabPage2 = new System.Windows.Forms.TabPage();
            path_label = new System.Windows.Forms.Label();
            button_Import = new System.Windows.Forms.Button();
            button_Export = new System.Windows.Forms.Button();
            hexPanel = new System.Windows.Forms.Panel();
            treeView1 = new System.Windows.Forms.TreeView();
            button_dump = new System.Windows.Forms.Button();
            button_Add = new System.Windows.Forms.Button();
            button_Remove = new System.Windows.Forms.Button();
            tab_Trainers = new System.Windows.Forms.TabPage();
            dgvTrainers = new System.Windows.Forms.DataGridView();
            NUD_Area = new System.Windows.Forms.NumericUpDown();
            L_Area = new System.Windows.Forms.Label();
            CB_UnusedTrainer = new System.Windows.Forms.ComboBox();
            B_ConvertTrainer = new System.Windows.Forms.Button();
            B_SaveTrainers = new System.Windows.Forms.Button();
            L_ConvertTrainer = new System.Windows.Forms.Label();
            tabControl1.SuspendLayout();
            tab_7_ZS.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_7_Count).BeginInit();
            tab_8_ZI.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_8_Count).BeginInit();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv).BeginInit();
            tabPage2.SuspendLayout();
            tab_Trainers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvTrainers).BeginInit();
            ((System.ComponentModel.ISupportInitialize)NUD_Area).BeginInit();
            SuspendLayout();
            // 
            // L_Location
            // 
            L_Location.AutoSize = true;
            L_Location.Location = new System.Drawing.Point(14, 10);
            L_Location.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_Location.Name = "L_Location";
            L_Location.Size = new System.Drawing.Size(29, 15);
            L_Location.TabIndex = 433;
            L_Location.Text = "Loc:";
            // 
            // CB_LocationID
            // 
            CB_LocationID.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            CB_LocationID.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            CB_LocationID.FormattingEnabled = true;
            CB_LocationID.Location = new System.Drawing.Point(54, 7);
            CB_LocationID.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            CB_LocationID.Name = "CB_LocationID";
            CB_LocationID.Size = new System.Drawing.Size(362, 23);
            CB_LocationID.TabIndex = 432;
            CB_LocationID.SelectedIndexChanged += CB_LocationID_SelectedIndexChanged;
            // 
            // tabControl1
            // 
            tabControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tabControl1.Controls.Add(tab_7_ZS);
            tabControl1.Controls.Add(tab_8_ZI);
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tab_Trainers);
            tabControl1.Location = new System.Drawing.Point(18, 38);
            tabControl1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new System.Drawing.Size(770, 538);
            tabControl1.TabIndex = 434;
            // 
            // tab_7_ZS
            // 
            tab_7_ZS.Controls.Add(L_7_Info);
            tab_7_ZS.Controls.Add(RTB_7_Parse);
            tab_7_ZS.Controls.Add(NUD_7_Count);
            tab_7_ZS.Controls.Add(L_7_Count);
            tab_7_ZS.Controls.Add(RTB_7_Script);
            tab_7_ZS.Controls.Add(RTB_7_Raw);
            tab_7_ZS.Location = new System.Drawing.Point(4, 24);
            tab_7_ZS.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tab_7_ZS.Name = "tab_7_ZS";
            tab_7_ZS.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tab_7_ZS.Size = new System.Drawing.Size(762, 510);
            tab_7_ZS.TabIndex = 0;
            tab_7_ZS.Text = "7.ZS";
            tab_7_ZS.UseVisualStyleBackColor = true;
            // 
            // L_7_Info
            // 
            L_7_Info.AutoSize = true;
            L_7_Info.Location = new System.Drawing.Point(160, 284);
            L_7_Info.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_7_Info.Name = "L_7_Info";
            L_7_Info.Size = new System.Drawing.Size(46, 15);
            L_7_Info.TabIndex = 438;
            L_7_Info.Text = "Count7";
            // 
            // RTB_7_Parse
            // 
            RTB_7_Parse.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            RTB_7_Parse.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_7_Parse.Location = new System.Drawing.Point(243, 239);
            RTB_7_Parse.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_7_Parse.Name = "RTB_7_Parse";
            RTB_7_Parse.ReadOnly = true;
            RTB_7_Parse.Size = new System.Drawing.Size(510, 259);
            RTB_7_Parse.TabIndex = 437;
            RTB_7_Parse.Text = "Script CMDs";
            // 
            // NUD_7_Count
            // 
            NUD_7_Count.Location = new System.Drawing.Point(163, 257);
            NUD_7_Count.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            NUD_7_Count.Name = "NUD_7_Count";
            NUD_7_Count.Size = new System.Drawing.Size(72, 23);
            NUD_7_Count.TabIndex = 436;
            NUD_7_Count.ValueChanged += NUD_7_Count_ValueChanged;
            // 
            // L_7_Count
            // 
            L_7_Count.AutoSize = true;
            L_7_Count.Location = new System.Drawing.Point(160, 239);
            L_7_Count.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_7_Count.Name = "L_7_Count";
            L_7_Count.Size = new System.Drawing.Size(46, 15);
            L_7_Count.TabIndex = 435;
            L_7_Count.Text = "Count7";
            // 
            // RTB_7_Script
            // 
            RTB_7_Script.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            RTB_7_Script.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_7_Script.Location = new System.Drawing.Point(7, 239);
            RTB_7_Script.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_7_Script.Name = "RTB_7_Script";
            RTB_7_Script.ReadOnly = true;
            RTB_7_Script.Size = new System.Drawing.Size(145, 259);
            RTB_7_Script.TabIndex = 431;
            RTB_7_Script.Text = "Script CMDs";
            // 
            // RTB_7_Raw
            // 
            RTB_7_Raw.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            RTB_7_Raw.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_7_Raw.Location = new System.Drawing.Point(7, 7);
            RTB_7_Raw.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_7_Raw.Name = "RTB_7_Raw";
            RTB_7_Raw.ReadOnly = true;
            RTB_7_Raw.Size = new System.Drawing.Size(746, 224);
            RTB_7_Raw.TabIndex = 430;
            RTB_7_Raw.Text = "Raw Data";
            // 
            // tab_8_ZI
            // 
            tab_8_ZI.Controls.Add(L_8_Info);
            tab_8_ZI.Controls.Add(RTB_8_Parse);
            tab_8_ZI.Controls.Add(NUD_8_Count);
            tab_8_ZI.Controls.Add(L_8_Count);
            tab_8_ZI.Controls.Add(RTB_8_Script);
            tab_8_ZI.Controls.Add(RTB_8_Raw);
            tab_8_ZI.Location = new System.Drawing.Point(4, 24);
            tab_8_ZI.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tab_8_ZI.Name = "tab_8_ZI";
            tab_8_ZI.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tab_8_ZI.Size = new System.Drawing.Size(762, 510);
            tab_8_ZI.TabIndex = 1;
            tab_8_ZI.Text = "8.ZI";
            tab_8_ZI.UseVisualStyleBackColor = true;
            // 
            // L_8_Info
            // 
            L_8_Info.AutoSize = true;
            L_8_Info.Location = new System.Drawing.Point(160, 284);
            L_8_Info.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_8_Info.Name = "L_8_Info";
            L_8_Info.Size = new System.Drawing.Size(46, 15);
            L_8_Info.TabIndex = 439;
            L_8_Info.Text = "Count8";
            // 
            // RTB_8_Parse
            // 
            RTB_8_Parse.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            RTB_8_Parse.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_8_Parse.Location = new System.Drawing.Point(243, 239);
            RTB_8_Parse.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_8_Parse.Name = "RTB_8_Parse";
            RTB_8_Parse.ReadOnly = true;
            RTB_8_Parse.Size = new System.Drawing.Size(510, 259);
            RTB_8_Parse.TabIndex = 438;
            RTB_8_Parse.Text = "Script CMDs";
            // 
            // NUD_8_Count
            // 
            NUD_8_Count.Location = new System.Drawing.Point(163, 257);
            NUD_8_Count.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            NUD_8_Count.Name = "NUD_8_Count";
            NUD_8_Count.Size = new System.Drawing.Size(72, 23);
            NUD_8_Count.TabIndex = 435;
            NUD_8_Count.ValueChanged += NUD_8_Count_ValueChanged;
            // 
            // L_8_Count
            // 
            L_8_Count.AutoSize = true;
            L_8_Count.Location = new System.Drawing.Point(160, 239);
            L_8_Count.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_8_Count.Name = "L_8_Count";
            L_8_Count.Size = new System.Drawing.Size(46, 15);
            L_8_Count.TabIndex = 434;
            L_8_Count.Text = "Count8";
            // 
            // RTB_8_Script
            // 
            RTB_8_Script.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            RTB_8_Script.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_8_Script.Location = new System.Drawing.Point(7, 239);
            RTB_8_Script.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_8_Script.Name = "RTB_8_Script";
            RTB_8_Script.ReadOnly = true;
            RTB_8_Script.Size = new System.Drawing.Size(145, 259);
            RTB_8_Script.TabIndex = 433;
            RTB_8_Script.Text = "Script CMDs";
            // 
            // RTB_8_Raw
            // 
            RTB_8_Raw.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            RTB_8_Raw.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RTB_8_Raw.Location = new System.Drawing.Point(7, 7);
            RTB_8_Raw.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            RTB_8_Raw.Name = "RTB_8_Raw";
            RTB_8_Raw.ReadOnly = true;
            RTB_8_Raw.Size = new System.Drawing.Size(746, 224);
            RTB_8_Raw.TabIndex = 432;
            RTB_8_Raw.Text = "Raw Data";
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(button1);
            tabPage1.Controls.Add(dgv);
            tabPage1.Location = new System.Drawing.Point(4, 24);
            tabPage1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Size = new System.Drawing.Size(762, 510);
            tabPage1.TabIndex = 2;
            tabPage1.Text = "Items";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            button1.Location = new System.Drawing.Point(7, 466);
            button1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(88, 35);
            button1.TabIndex = 3;
            button1.Text = "Save";
            button1.UseVisualStyleBackColor = true;
            button1.Click += buttonSave_Click;
            // 
            // dgv
            // 
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeColumns = false;
            dgv.AllowUserToResizeRows = false;
            dgv.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { dgvIndex, dgvItem });
            dgv.Location = new System.Drawing.Point(7, 7);
            dgv.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            dgv.Name = "dgv";
            dgv.Size = new System.Drawing.Size(747, 452);
            dgv.TabIndex = 2;
            // 
            // dgvIndex
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dgvIndex.DefaultCellStyle = dataGridViewCellStyle1;
            dgvIndex.HeaderText = "Index";
            dgvIndex.Name = "dgvIndex";
            dgvIndex.ReadOnly = true;
            dgvIndex.Width = 45;
            // 
            // dgvItem
            // 
            dgvItem.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            dgvItem.HeaderText = "Item";
            dgvItem.Name = "dgvItem";
            dgvItem.Width = 135;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(button_Remove);
            tabPage2.Controls.Add(button_Add);
            tabPage2.Controls.Add(path_label);
            tabPage2.Controls.Add(button_Import);
            tabPage2.Controls.Add(button_Export);
            tabPage2.Controls.Add(hexPanel);
            tabPage2.Controls.Add(treeView1);
            tabPage2.Location = new System.Drawing.Point(4, 24);
            tabPage2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Size = new System.Drawing.Size(762, 510);
            tabPage2.TabIndex = 3;
            tabPage2.Text = "Experimental";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // path_label
            // 
            path_label.AutoSize = true;
            path_label.Location = new System.Drawing.Point(267, 16);
            path_label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            path_label.Name = "path_label";
            path_label.Size = new System.Drawing.Size(38, 15);
            path_label.TabIndex = 4;
            path_label.Text = "label1";
            // 
            // button_Import
            // 
            button_Import.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            button_Import.Location = new System.Drawing.Point(561, 7);
            button_Import.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button_Import.Name = "button_Import";
            button_Import.Size = new System.Drawing.Size(93, 32);
            button_Import.TabIndex = 3;
            button_Import.Text = "Import";
            button_Import.UseVisualStyleBackColor = true;
            button_Import.Click += button_Import_Click;
            // 
            // button_Export
            // 
            button_Export.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            button_Export.Location = new System.Drawing.Point(660, 7);
            button_Export.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button_Export.Name = "button_Export";
            button_Export.Size = new System.Drawing.Size(93, 32);
            button_Export.TabIndex = 2;
            button_Export.Text = "Export";
            button_Export.UseVisualStyleBackColor = true;
            button_Export.Click += button_Export_Click;
            // 
            // hexPanel
            // 
            hexPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            hexPanel.Location = new System.Drawing.Point(267, 46);
            hexPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            hexPanel.Name = "hexPanel";
            hexPanel.Size = new System.Drawing.Size(486, 455);
            hexPanel.TabIndex = 1;
            // 
            // treeView1
            // 
            treeView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            treeView1.Location = new System.Drawing.Point(8, 46);
            treeView1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            treeView1.Name = "treeView1";
            treeView1.Size = new System.Drawing.Size(252, 455);
            treeView1.TabIndex = 0;
            treeView1.AfterSelect += treeView1_AfterSelect;
            treeView1.MouseDoubleClick += treeView1_MouseDoubleClick;
            // 
            // button_dump
            // 
            button_dump.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            button_dump.Location = new System.Drawing.Point(682, 7);
            button_dump.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button_dump.Name = "button_dump";
            button_dump.Size = new System.Drawing.Size(104, 28);
            button_dump.TabIndex = 435;
            button_dump.Text = "Dump";
            button_dump.UseVisualStyleBackColor = true;
            button_dump.Click += button_dump_Click;
            // 
            // button_Add
            // 
            button_Add.Location = new System.Drawing.Point(7, 7);
            button_Add.Name = "button_Add";
            button_Add.Size = new System.Drawing.Size(75, 33);
            button_Add.TabIndex = 5;
            button_Add.Text = "Add";
            button_Add.UseVisualStyleBackColor = true;
            button_Add.Click += button_Add_Click;
            // 
            // button_Remove
            // 
            button_Remove.Location = new System.Drawing.Point(88, 7);
            button_Remove.Name = "button_Remove";
            button_Remove.Size = new System.Drawing.Size(75, 33);
            button_Remove.TabIndex = 6;
            button_Remove.Text = "Remove";
            button_Remove.UseVisualStyleBackColor = true;
            button_Remove.Click += button_Remove_Click;
            // 
            // tab_Trainers
            // 
            tab_Trainers.Controls.Add(L_ConvertTrainer);
            tab_Trainers.Controls.Add(B_SaveTrainers);
            tab_Trainers.Controls.Add(B_ConvertTrainer);
            tab_Trainers.Controls.Add(CB_UnusedTrainer);
            tab_Trainers.Controls.Add(L_Area);
            tab_Trainers.Controls.Add(NUD_Area);
            tab_Trainers.Controls.Add(dgvTrainers);
            tab_Trainers.Location = new System.Drawing.Point(4, 24);
            tab_Trainers.Name = "tab_Trainers";
            tab_Trainers.Padding = new System.Windows.Forms.Padding(3);
            tab_Trainers.Size = new System.Drawing.Size(762, 510);
            tab_Trainers.TabIndex = 4;
            tab_Trainers.Text = "Trainers";
            tab_Trainers.UseVisualStyleBackColor = true;
            // 
            // dgvTrainers
            // 
            dgvTrainers.AllowUserToAddRows = false;
            dgvTrainers.AllowUserToDeleteRows = false;
            dgvTrainers.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgvTrainers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvTrainers.Location = new System.Drawing.Point(6, 35);
            dgvTrainers.Name = "dgvTrainers";
            dgvTrainers.Size = new System.Drawing.Size(750, 429);
            dgvTrainers.TabIndex = 0;
            // 
            // NUD_Area
            // 
            NUD_Area.Location = new System.Drawing.Point(45, 6);
            NUD_Area.Name = "NUD_Area";
            NUD_Area.Size = new System.Drawing.Size(43, 23);
            NUD_Area.TabIndex = 1;
            NUD_Area.ValueChanged += NUD_Area_ValueChanged;
            // 
            // L_Area
            // 
            L_Area.AutoSize = true;
            L_Area.Location = new System.Drawing.Point(5, 10);
            L_Area.Name = "L_Area";
            L_Area.Size = new System.Drawing.Size(34, 15);
            L_Area.TabIndex = 2;
            L_Area.Text = "Area:";
            // 
            // CB_UnusedTrainer
            // 
            CB_UnusedTrainer.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            CB_UnusedTrainer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_UnusedTrainer.FormattingEnabled = true;
            CB_UnusedTrainer.Location = new System.Drawing.Point(6, 480);
            CB_UnusedTrainer.Name = "CB_UnusedTrainer";
            CB_UnusedTrainer.Size = new System.Drawing.Size(200, 23);
            CB_UnusedTrainer.TabIndex = 3;
            // 
            // B_ConvertTrainer
            // 
            B_ConvertTrainer.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            B_ConvertTrainer.Location = new System.Drawing.Point(212, 479);
            B_ConvertTrainer.Name = "B_ConvertTrainer";
            B_ConvertTrainer.Size = new System.Drawing.Size(120, 25);
            B_ConvertTrainer.TabIndex = 4;
            B_ConvertTrainer.Text = "Convert to Trainer";
            B_ConvertTrainer.UseVisualStyleBackColor = true;
            B_ConvertTrainer.Click += B_ConvertTrainer_Click;
            // 
            // B_SaveTrainers
            // 
            B_SaveTrainers.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            B_SaveTrainers.Location = new System.Drawing.Point(668, 479);
            B_SaveTrainers.Name = "B_SaveTrainers";
            B_SaveTrainers.Size = new System.Drawing.Size(88, 25);
            B_SaveTrainers.TabIndex = 5;
            B_SaveTrainers.Text = "Save";
            B_SaveTrainers.UseVisualStyleBackColor = true;
            B_SaveTrainers.Click += B_SaveTrainers_Click;
            // 
            // L_ConvertTrainer
            // 
            L_ConvertTrainer.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            L_ConvertTrainer.AutoSize = true;
            L_ConvertTrainer.Location = new System.Drawing.Point(5, 465);
            L_ConvertTrainer.Name = "L_ConvertTrainer";
            L_ConvertTrainer.Size = new System.Drawing.Size(167, 15);
            L_ConvertTrainer.TabIndex = 6;
            L_ConvertTrainer.Text = "Select Unused Trainer to Copy:";
            // 
            // OWSE7
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(798, 590);
            Controls.Add(button_dump);
            Controls.Add(tabControl1);
            Controls.Add(L_Location);
            Controls.Add(CB_LocationID);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "OWSE7";
            Text = "OWSE7";
            tabControl1.ResumeLayout(false);
            tab_7_ZS.ResumeLayout(false);
            tab_7_ZS.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_7_Count).EndInit();
            tab_8_ZI.ResumeLayout(false);
            tab_8_ZI.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_8_Count).EndInit();
            tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv).EndInit();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            tab_Trainers.ResumeLayout(false);
            tab_Trainers.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvTrainers).EndInit();
            ((System.ComponentModel.ISupportInitialize)NUD_Area).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Label L_Location;
        private System.Windows.Forms.ComboBox CB_LocationID;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tab_7_ZS;
        private System.Windows.Forms.TabPage tab_8_ZI;
        private System.Windows.Forms.RichTextBox RTB_7_Script;
        private System.Windows.Forms.RichTextBox RTB_7_Raw;
        private System.Windows.Forms.RichTextBox RTB_8_Script;
        private System.Windows.Forms.RichTextBox RTB_8_Raw;
        private System.Windows.Forms.Label L_8_Count;
        private System.Windows.Forms.Label L_7_Count;
        private System.Windows.Forms.NumericUpDown NUD_8_Count;
        private System.Windows.Forms.NumericUpDown NUD_7_Count;
        private System.Windows.Forms.RichTextBox RTB_7_Parse;
        private System.Windows.Forms.RichTextBox RTB_8_Parse;
        private System.Windows.Forms.Label L_7_Info;
        private System.Windows.Forms.Label L_8_Info;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.DataGridView dgv;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgvIndex;
        private System.Windows.Forms.DataGridViewComboBoxColumn dgvItem;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Panel hexPanel;
        private System.Windows.Forms.Button button_Import;
        private System.Windows.Forms.Button button_Export;
        private System.Windows.Forms.Label path_label;
        private System.Windows.Forms.Button button_dump;
        private System.Windows.Forms.Button button_Add;
        private System.Windows.Forms.Button button_Remove;
        private System.Windows.Forms.TabPage tab_Trainers;
        private System.Windows.Forms.DataGridView dgvTrainers;
        private System.Windows.Forms.NumericUpDown NUD_Area;
        private System.Windows.Forms.Label L_Area;
        private System.Windows.Forms.ComboBox CB_UnusedTrainer;
        private System.Windows.Forms.Button B_ConvertTrainer;
        private System.Windows.Forms.Button B_SaveTrainers;
        private System.Windows.Forms.Label L_ConvertTrainer;
    }
}