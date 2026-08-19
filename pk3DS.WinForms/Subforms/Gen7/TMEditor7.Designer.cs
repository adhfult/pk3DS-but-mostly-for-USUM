namespace pk3DS.WinForms;

partial class TMEditor7
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
        this.dgvTM = new System.Windows.Forms.DataGridView();
        this.L_TM = new System.Windows.Forms.Label();
        this.B_RTM = new System.Windows.Forms.Button();
        this.B_ExportTxt = new System.Windows.Forms.Button();
        this.B_ImportTxt = new System.Windows.Forms.Button();
        this.B_UpdateDesc = new System.Windows.Forms.Button();
        this.TB_Offset = new System.Windows.Forms.TextBox();
        this.L_Offset = new System.Windows.Forms.Label();
        this.NUD_TMCount = new System.Windows.Forms.NumericUpDown();
        this.L_TMCount = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.dgvTM)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_TMCount)).BeginInit();
        this.SuspendLayout();
        // 
        // dgvTM
        // 
        this.dgvTM.AllowUserToAddRows = false;
        this.dgvTM.AllowUserToDeleteRows = false;
        this.dgvTM.AllowUserToResizeColumns = false;
        this.dgvTM.AllowUserToResizeRows = false;
        this.dgvTM.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
                                                                   | System.Windows.Forms.AnchorStyles.Left) 
                                                                  | System.Windows.Forms.AnchorStyles.Right)));
        this.dgvTM.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dgvTM.Location = new System.Drawing.Point(9, 25);
        this.dgvTM.Name = "dgvTM";
        this.dgvTM.Size = new System.Drawing.Size(240, 299);
        this.dgvTM.TabIndex = 1;
        // 
        // L_TM
        // 
        this.L_TM.AutoSize = true;
        this.L_TM.Location = new System.Drawing.Point(9, 9);
        this.L_TM.Name = "L_TM";
        this.L_TM.Size = new System.Drawing.Size(26, 13);
        this.L_TM.TabIndex = 2;
        this.L_TM.Text = "TM:";
        // 
        // B_RTM
        // 
        this.B_RTM.Location = new System.Drawing.Point(41, 1);
        this.B_RTM.Name = "B_RTM";
        this.B_RTM.Size = new System.Drawing.Size(75, 23);
        this.B_RTM.TabIndex = 5;
        this.B_RTM.Text = "Randomize";
        this.B_RTM.UseVisualStyleBackColor = true;
        this.B_RTM.Click += new System.EventHandler(this.B_RandomTM_Click);
        // 
        // B_ExportTxt
        // 
        this.B_ExportTxt.Location = new System.Drawing.Point(120, 1);
        this.B_ExportTxt.Name = "B_ExportTxt";
        this.B_ExportTxt.Size = new System.Drawing.Size(75, 23);
        this.B_ExportTxt.TabIndex = 6;
        this.B_ExportTxt.Text = "Export .txt";
        this.B_ExportTxt.UseVisualStyleBackColor = true;
        this.B_ExportTxt.Click += new System.EventHandler(this.B_ExportTxt_Click);
        // 
        // B_ImportTxt
        // 
        this.B_ImportTxt.Location = new System.Drawing.Point(199, 1);
        this.B_ImportTxt.Name = "B_ImportTxt";
        this.B_ImportTxt.Size = new System.Drawing.Size(75, 23);
        this.B_ImportTxt.TabIndex = 7;
        this.B_ImportTxt.Text = "Import .txt";
        this.B_ImportTxt.UseVisualStyleBackColor = true;
        this.B_ImportTxt.Click += new System.EventHandler(this.B_ImportTxt_Click);
        // 
        // B_UpdateDesc
        // 
        this.B_UpdateDesc.Location = new System.Drawing.Point(278, 1);
        this.B_UpdateDesc.Name = "B_UpdateDesc";
        this.B_UpdateDesc.Size = new System.Drawing.Size(90, 23);
        this.B_UpdateDesc.TabIndex = 8;
        this.B_UpdateDesc.Text = "Update Desc";
        this.B_UpdateDesc.UseVisualStyleBackColor = true;
        this.B_UpdateDesc.Click += new System.EventHandler(this.B_UpdateDesc_Click);
        // 
        // TB_Offset
        // 
        this.TB_Offset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.TB_Offset.Location = new System.Drawing.Point(100, 332);  // row 1
        this.TB_Offset.Name = "TB_Offset";
        this.TB_Offset.Size = new System.Drawing.Size(100, 20);
        this.TB_Offset.TabIndex = 9;
        this.TB_Offset.TextChanged += new System.EventHandler(this.TB_Offset_TextChanged);
        // 
        // L_Offset
        // 
        this.L_Offset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        // Fixed width and right-aligned: AutoSize made this row depend on the rendered width of
        this.L_Offset.Location = new System.Drawing.Point(9, 335);
        this.L_Offset.Name = "L_Offset";
        this.L_Offset.Size = new System.Drawing.Size(86, 15);
        this.L_Offset.TabIndex = 10;
        this.L_Offset.Text = "Table Offset:";
        // 
        // NUD_TMCount
        // 
        this.NUD_TMCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.NUD_TMCount.Location = new System.Drawing.Point(275, 332);  // row 1
        this.NUD_TMCount.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
        this.NUD_TMCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.NUD_TMCount.Name = "NUD_TMCount";
        this.NUD_TMCount.Size = new System.Drawing.Size(60, 20);
        this.NUD_TMCount.TabIndex = 11;
        this.NUD_TMCount.Value = new decimal(new int[] { 100, 0, 0, 0 });
        this.NUD_TMCount.ValueChanged += new System.EventHandler(this.NUD_TMCount_ValueChanged);
        //
        // L_TMCount
        //
        this.L_TMCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.L_TMCount.AutoSize = true;
        this.L_TMCount.Location = new System.Drawing.Point(215, 335);  // row 1, before its NUD
        this.L_TMCount.Name = "L_TMCount";
        this.L_TMCount.Size = new System.Drawing.Size(55, 13);
        this.L_TMCount.TabIndex = 12;
        this.L_TMCount.Text = "TM Count";
        // 
        // TMEditor7
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(380, 400);
        this.Controls.Add(this.L_Offset);
        this.Controls.Add(this.TB_Offset);
        // The expansion's code and text patches had no entry point in the UI at all, so the extra
        // item IDs could be assigned while nothing that makes them usable ever ran.
        this.B_TMPreflight = new System.Windows.Forms.Button();
        this.B_TMPreflight.Location = new System.Drawing.Point(8, 362);   // row 2
        this.B_TMPreflight.Name = "B_TMPreflight";
        this.B_TMPreflight.Size = new System.Drawing.Size(175, 25);
        this.B_TMPreflight.Text = "Check Expansion Patches";
        this.B_TMPreflight.UseVisualStyleBackColor = true;
        this.B_TMPreflight.Click += new System.EventHandler(this.B_TMPreflight_Click);
        this.Controls.Add(this.B_TMPreflight);

        this.B_TMApply = new System.Windows.Forms.Button();
        this.B_TMApply.Location = new System.Drawing.Point(189, 362);  // row 2
        this.B_TMApply.Name = "B_TMApply";
        this.B_TMApply.Size = new System.Drawing.Size(180, 25);
        this.B_TMApply.Text = "Apply Expansion Patches...";
        this.B_TMApply.UseVisualStyleBackColor = true;
        this.B_TMApply.Click += new System.EventHandler(this.B_TMApply_Click);
        this.Controls.Add(this.B_TMApply);

        this.Controls.Add(this.B_UpdateDesc);
        this.Controls.Add(this.B_ImportTxt);
        this.Controls.Add(this.B_ExportTxt);
        this.Controls.Add(this.B_RTM);
        this.Controls.Add(this.L_TM);
        this.Controls.Add(this.dgvTM);
        this.Controls.Add(this.NUD_TMCount);
        this.Controls.Add(this.L_TMCount);
        this.MaximizeBox = false;
        this.MaximumSize = new System.Drawing.Size(520, 670);
        this.MinimumSize = new System.Drawing.Size(396, 370);
        this.Name = "TMEditor7";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "TM Editor";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
        ((System.ComponentModel.ISupportInitialize)(this.dgvTM)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.NUD_TMCount)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.DataGridView dgvTM;
    private System.Windows.Forms.Label L_TM;
    private System.Windows.Forms.Button B_RTM;
    private System.Windows.Forms.Button B_ExportTxt;
    private System.Windows.Forms.Button B_ImportTxt;
    private System.Windows.Forms.Button B_UpdateDesc;
    private System.Windows.Forms.Button B_TMPreflight;
    private System.Windows.Forms.Button B_TMApply;
    private System.Windows.Forms.TextBox TB_Offset;
    private System.Windows.Forms.Label L_Offset;
    private System.Windows.Forms.NumericUpDown NUD_TMCount;
    private System.Windows.Forms.Label L_TMCount;
}