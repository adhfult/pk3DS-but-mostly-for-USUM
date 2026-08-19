namespace pk3DS.WinForms;

partial class TextEditor
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

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.B_AddLine = new System.Windows.Forms.Button();
        this.B_AddLineBefore = new System.Windows.Forms.Button();
        this.B_RemoveLine = new System.Windows.Forms.Button();
        this.B_Export = new System.Windows.Forms.Button();
        this.B_Import = new System.Windows.Forms.Button();
        this.L_Search = new System.Windows.Forms.Label();
        this.TB_Search = new System.Windows.Forms.TextBox();
        this.B_Search = new System.Windows.Forms.Button();
        this.B_SearchPrev = new System.Windows.Forms.Button();
        this.B_BatchReplace = new System.Windows.Forms.Button();
        this.B_Randomize = new System.Windows.Forms.Button();
        this.L_Visualizer = new System.Windows.Forms.Label();
        this.RTB_Visualizer = new System.Windows.Forms.RichTextBox();
        this.CB_Entry = new System.Windows.Forms.ComboBox();
        this.dgv = new System.Windows.Forms.DataGridView();

        ((System.ComponentModel.ISupportInitialize)(this.dgv)).BeginInit();
        this.SuspendLayout();
        // 
        // CB_Entry
        // 
        this.CB_Entry.FormattingEnabled = true;
        this.CB_Entry.Location = new System.Drawing.Point(68, 7);
        this.CB_Entry.Name = "CB_Entry";
        this.CB_Entry.Size = new System.Drawing.Size(220, 21);
        this.CB_Entry.TabIndex = 5;
        this.CB_Entry.SelectedIndexChanged += new System.EventHandler(this.ChangeEntry);
        // 
        // dgv
        // 
        this.dgv.AllowUserToAddRows = false;
        this.dgv.AllowUserToDeleteRows = false;
        this.dgv.AllowUserToResizeRows = false;
        this.dgv.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
                                                                 | System.Windows.Forms.AnchorStyles.Left) 
                                                                | System.Windows.Forms.AnchorStyles.Right)));
        this.dgv.BackgroundColor = System.Drawing.SystemColors.Control;
        this.dgv.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
        this.dgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dgv.Location = new System.Drawing.Point(12, 33);
        this.dgv.Name = "dgv";
        this.dgv.RowHeadersVisible = false;
        this.dgv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
        this.dgv.ShowEditingIcon = false;
        this.dgv.Size = new System.Drawing.Size(976, 250);
        this.dgv.TabIndex = 0;
        // 

        // 
        // B_AddLineBefore
        // 
        this.B_AddLineBefore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.B_AddLineBefore.Location = new System.Drawing.Point(640, 6);
        this.B_AddLineBefore.Name = "B_AddLineBefore";
        this.B_AddLineBefore.Size = new System.Drawing.Size(100, 23);
        this.B_AddLineBefore.TabIndex = 12;
        this.B_AddLineBefore.Text = "Add Line Before";
        this.B_AddLineBefore.UseVisualStyleBackColor = true;
        this.B_AddLineBefore.Click += new System.EventHandler(this.B_AddLineBefore_Click);
        // 
        // B_AddLine
        // 
        this.B_AddLine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.B_AddLine.Location = new System.Drawing.Point(750, 6);
        this.B_AddLine.Name = "B_AddLine";
        this.B_AddLine.Size = new System.Drawing.Size(100, 23);
        this.B_AddLine.TabIndex = 6;
        this.B_AddLine.Text = "Add Line After";
        this.B_AddLine.UseVisualStyleBackColor = true;
        this.B_AddLine.Click += new System.EventHandler(this.B_AddLine_Click);
        // 
        // B_RemoveLine
        // 
        this.B_RemoveLine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.B_RemoveLine.Location = new System.Drawing.Point(860, 6);
        this.B_RemoveLine.Name = "B_RemoveLine";
        this.B_RemoveLine.Size = new System.Drawing.Size(100, 23);
        this.B_RemoveLine.TabIndex = 7;
        this.B_RemoveLine.Text = "Remove Line";
        this.B_RemoveLine.UseVisualStyleBackColor = true;
        this.B_RemoveLine.Click += new System.EventHandler(this.B_RemoveLine_Click);
        // 
        // L_Search
        // 
        this.L_Search.AutoSize = true;
        this.L_Search.Location = new System.Drawing.Point(12, 410);
        this.L_Search.Name = "L_Search";
        this.L_Search.Size = new System.Drawing.Size(44, 13);
        this.L_Search.Text = "Search:";
        this.L_Search.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        // 
        // TB_Search
        // 
        this.TB_Search.Location = new System.Drawing.Point(62, 407);
        this.TB_Search.Name = "TB_Search";
        this.TB_Search.Size = new System.Drawing.Size(150, 20);
        this.TB_Search.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        // 
        // B_Search
        // 
        this.B_Search.Location = new System.Drawing.Point(300, 405);
        this.B_Search.Name = "B_Search";
        this.B_Search.Size = new System.Drawing.Size(75, 23);
        this.B_Search.TabIndex = 6;
        this.B_Search.Text = "Find Next";
        this.B_Search.UseVisualStyleBackColor = true;
        this.B_Search.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_Search.Click += new System.EventHandler(this.B_Search_Click);
        // 
        // B_SearchPrev
        // 
        this.B_SearchPrev.Location = new System.Drawing.Point(220, 405);
        this.B_SearchPrev.Name = "B_SearchPrev";
        this.B_SearchPrev.Size = new System.Drawing.Size(75, 23);
        this.B_SearchPrev.TabIndex = 7;
        this.B_SearchPrev.Text = "Find Prev";
        this.B_SearchPrev.UseVisualStyleBackColor = true;
        this.B_SearchPrev.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_SearchPrev.Click += new System.EventHandler(this.B_SearchPrev_Click);
        // 
        // B_BatchReplace
        // 
        // B_Randomize_Click was fully implemented but had no control to invoke it.
        this.B_Randomize.Location = new System.Drawing.Point(485, 405);
        this.B_Randomize.Name = "B_Randomize";
        this.B_Randomize.Size = new System.Drawing.Size(95, 23);
        this.B_Randomize.Text = "Randomize";
        this.B_Randomize.UseVisualStyleBackColor = true;
        this.B_Randomize.Click += new System.EventHandler(this.B_Randomize_Click);
        this.B_BatchReplace.Location = new System.Drawing.Point(380, 405);
        this.B_BatchReplace.Name = "B_BatchReplace";
        this.B_BatchReplace.Size = new System.Drawing.Size(95, 23);
        this.B_BatchReplace.TabIndex = 8;
        this.B_BatchReplace.Text = "Batch Replace";
        this.B_BatchReplace.UseVisualStyleBackColor = true;
        this.B_BatchReplace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_BatchReplace.Click += new System.EventHandler(this.B_BatchReplace_Click);
        // 
        // B_Export
        // 
        this.B_Export.Location = new System.Drawing.Point(300, 6);
        this.B_Export.Name = "B_Export";
        this.B_Export.Size = new System.Drawing.Size(90, 23);
        this.B_Export.TabIndex = 8;
        this.B_Export.Text = "Export All (.txt)";
        this.B_Export.UseVisualStyleBackColor = true;
        this.B_Export.Click += new System.EventHandler(this.B_Export_Click);
        // 
        // B_Import
        // 
        this.B_Import.Location = new System.Drawing.Point(400, 6);
        this.B_Import.Name = "B_Import";
        this.B_Import.Size = new System.Drawing.Size(90, 23);
        this.B_Import.TabIndex = 10;
        this.B_Import.Text = "Import All (.txt)";
        this.B_Import.UseVisualStyleBackColor = true;
        this.B_Import.Click += new System.EventHandler(this.B_Import_Click);
        // 
        // L_Visualizer
        // 
        this.L_Visualizer.AutoSize = true;
        this.L_Visualizer.Location = new System.Drawing.Point(12, 330);
        this.L_Visualizer.Name = "L_Visualizer";
        this.L_Visualizer.Size = new System.Drawing.Size(110, 13);
        this.L_Visualizer.TabIndex = 14;
        this.L_Visualizer.Text = "In-Game Visualizer:";
        this.L_Visualizer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        // 
        // RTB_Visualizer
        // 
        this.RTB_Visualizer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this.RTB_Visualizer.BackColor = System.Drawing.Color.Black;
        this.RTB_Visualizer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.RTB_Visualizer.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.RTB_Visualizer.ForeColor = System.Drawing.Color.White;
        this.RTB_Visualizer.Location = new System.Drawing.Point(12, 345);
        this.RTB_Visualizer.Name = "RTB_Visualizer";
        this.RTB_Visualizer.Size = new System.Drawing.Size(876, 50);
        this.RTB_Visualizer.TabIndex = 15;
        this.RTB_Visualizer.Text = "";
        // 
        // TextEditor
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1000, 480);
        this.Controls.Add(this.L_Search);
        this.Controls.Add(this.B_BatchReplace);
        this.Controls.Add(this.B_Randomize);
        this.Controls.Add(this.B_SearchPrev);
        this.Controls.Add(this.B_Search);
        this.Controls.Add(this.TB_Search);
        this.Controls.Add(this.B_Import);
        this.Controls.Add(this.B_Export);

        this.Controls.Add(this.B_RemoveLine);
        this.Controls.Add(this.B_AddLineBefore);
        this.Controls.Add(this.B_AddLine);
        this.Controls.Add(this.dgv);
        this.Controls.Add(this.CB_Entry);
        this.Controls.Add(this.L_Visualizer);
        this.Controls.Add(this.RTB_Visualizer);
        this.MinimumSize = new System.Drawing.Size(500, 450);
        this.Name = "TextEditor";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Text Editor";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TextEditor_FormClosing);
        ((System.ComponentModel.ISupportInitialize)(this.dgv)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.ComboBox CB_Entry;
    private System.Windows.Forms.DataGridView dgv;

    private System.Windows.Forms.Button B_AddLineBefore;
    private System.Windows.Forms.Button B_AddLine;
    private System.Windows.Forms.Button B_RemoveLine;
    private System.Windows.Forms.Button B_Export;
    private System.Windows.Forms.Button B_Import;
    private System.Windows.Forms.Label L_Search;
    private System.Windows.Forms.TextBox TB_Search;
    private System.Windows.Forms.Button B_SearchPrev;
    private System.Windows.Forms.Button B_BatchReplace;
    private System.Windows.Forms.Button B_Randomize;
    private System.Windows.Forms.Button B_Search;
    private System.Windows.Forms.RichTextBox RTB_Visualizer;
    private System.Windows.Forms.Label L_Visualizer;
}