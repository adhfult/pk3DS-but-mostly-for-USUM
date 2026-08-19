namespace pk3DS.WinForms
{
    partial class ShowdownSetStorage
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
            this.LB_Sets = new System.Windows.Forms.ListBox();
            this.TB_Search = new System.Windows.Forms.TextBox();
            this.RTB_Preview = new System.Windows.Forms.RichTextBox();
            this.B_Add = new System.Windows.Forms.Button();
            this.B_ImportFile = new System.Windows.Forms.Button();
            this.B_ExportFile = new System.Windows.Forms.Button();
            this.B_Delete = new System.Windows.Forms.Button();
            this.B_ClearAll = new System.Windows.Forms.Button();
            this.B_Copy = new System.Windows.Forms.Button();
            this.B_Use = new System.Windows.Forms.Button();
            this.B_Close = new System.Windows.Forms.Button();
            this.L_Storage = new System.Windows.Forms.Label();
            this.L_Count = new System.Windows.Forms.Label();
            this.L_PreviewHeader = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // L_Storage
            // 
            this.L_Storage.AutoSize = true;
            this.L_Storage.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.L_Storage.ForeColor = System.Drawing.Color.Cyan;
            this.L_Storage.Location = new System.Drawing.Point(12, 10);
            this.L_Storage.Name = "L_Storage";
            this.L_Storage.Size = new System.Drawing.Size(262, 25);
            this.L_Storage.TabIndex = 0;
            this.L_Storage.Text = "SHOWDOWN SET STORAGE";
            this.L_Count.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.L_Count.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.L_Count.ForeColor = System.Drawing.Color.Gainsboro;
            this.L_Count.Location = new System.Drawing.Point(440, 14);
            this.L_Count.Name = "L_Count";
            this.L_Count.Size = new System.Drawing.Size(288, 20);
            this.L_Count.TabIndex = 1;
            this.L_Count.Text = "Capacity: 0 / 1500 Sets";
            this.L_Count.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.TB_Search.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(25)))), ((int)(((byte)(32)))));
            this.TB_Search.ForeColor = System.Drawing.Color.LightGray;
            this.TB_Search.Location = new System.Drawing.Point(12, 42);
            this.TB_Search.Name = "TB_Search";
            this.TB_Search.Size = new System.Drawing.Size(260, 22);
            this.TB_Search.TabIndex = 2;
            this.TB_Search.TextChanged += new System.EventHandler(this.TB_Search_TextChanged);
            // 
            // LB_Sets
            // 
            this.LB_Sets.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.LB_Sets.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(26)))));
            this.LB_Sets.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.LB_Sets.ForeColor = System.Drawing.Color.White;
            this.LB_Sets.FormattingEnabled = true;
            this.LB_Sets.IntegralHeight = false;
            this.LB_Sets.ItemHeight = 15;
            this.LB_Sets.Location = new System.Drawing.Point(12, 70);
            this.LB_Sets.Name = "LB_Sets";
            this.LB_Sets.Size = new System.Drawing.Size(260, 390);
            this.LB_Sets.TabIndex = 3;
            this.LB_Sets.SelectedIndexChanged += new System.EventHandler(this.LB_Sets_SelectedIndexChanged);
            // 
            // L_PreviewHeader
            // 
            this.L_PreviewHeader.AutoSize = true;
            this.L_PreviewHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.L_PreviewHeader.ForeColor = System.Drawing.Color.LightSkyBlue;
            this.L_PreviewHeader.Location = new System.Drawing.Point(282, 45);
            this.L_PreviewHeader.Name = "L_PreviewHeader";
            this.L_PreviewHeader.Size = new System.Drawing.Size(76, 15);
            this.L_PreviewHeader.TabIndex = 4;
            this.L_PreviewHeader.Text = "Set Preview:";
            // 
            // RTB_Preview
            // 
            this.RTB_Preview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RTB_Preview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(14)))), ((int)(((byte)(14)))), ((int)(((byte)(18)))));
            this.RTB_Preview.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.RTB_Preview.ForeColor = System.Drawing.Color.LightCyan;
            this.RTB_Preview.Location = new System.Drawing.Point(282, 70);
            this.RTB_Preview.Name = "RTB_Preview";
            this.RTB_Preview.ReadOnly = true;
            this.RTB_Preview.Size = new System.Drawing.Size(446, 390);
            this.RTB_Preview.TabIndex = 5;
            this.RTB_Preview.Text = "";
            // 
            // B_Add
            // 
            this.B_Add.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_Add.Location = new System.Drawing.Point(12, 468);
            this.B_Add.Name = "B_Add";
            this.B_Add.Size = new System.Drawing.Size(126, 28);
            this.B_Add.TabIndex = 6;
            this.B_Add.Text = "+ Add Clipboard";
            this.B_Add.UseVisualStyleBackColor = true;
            this.B_Add.Click += new System.EventHandler(this.B_Add_Click);
            // 
            // B_ImportFile
            // 
            this.B_ImportFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_ImportFile.Location = new System.Drawing.Point(146, 468);
            this.B_ImportFile.Name = "B_ImportFile";
            this.B_ImportFile.Size = new System.Drawing.Size(126, 28);
            this.B_ImportFile.TabIndex = 7;
            this.B_ImportFile.Text = "📁 Import File";
            this.B_ImportFile.UseVisualStyleBackColor = true;
            this.B_ImportFile.Click += new System.EventHandler(this.B_ImportFile_Click);
            // 
            // B_Delete
            // 
            this.B_Delete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_Delete.Location = new System.Drawing.Point(12, 502);
            this.B_Delete.Name = "B_Delete";
            this.B_Delete.Size = new System.Drawing.Size(126, 28);
            this.B_Delete.TabIndex = 8;
            this.B_Delete.Text = "Delete Selected";
            this.B_Delete.UseVisualStyleBackColor = true;
            this.B_Delete.Click += new System.EventHandler(this.B_Delete_Click);
            // 
            // B_ClearAll
            // 
            this.B_ClearAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_ClearAll.Location = new System.Drawing.Point(146, 502);
            this.B_ClearAll.Name = "B_ClearAll";
            this.B_ClearAll.Size = new System.Drawing.Size(126, 28);
            this.B_ClearAll.TabIndex = 9;
            this.B_ClearAll.Text = "Clear All";
            this.B_ClearAll.UseVisualStyleBackColor = true;
            this.B_ClearAll.Click += new System.EventHandler(this.B_ClearAll_Click);
            this.B_ExportFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_ExportFile.Location = new System.Drawing.Point(282, 468);
            this.B_ExportFile.Name = "B_ExportFile";
            this.B_ExportFile.Size = new System.Drawing.Size(130, 28);
            this.B_ExportFile.TabIndex = 10;
            this.B_ExportFile.Text = "💾 Export All";
            this.B_ExportFile.UseVisualStyleBackColor = true;
            this.B_ExportFile.Click += new System.EventHandler(this.B_ExportFile_Click);
            // 
            // B_Copy
            // 
            this.B_Copy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.B_Copy.Location = new System.Drawing.Point(282, 502);
            this.B_Copy.Name = "B_Copy";
            this.B_Copy.Size = new System.Drawing.Size(130, 28);
            this.B_Copy.TabIndex = 11;
            this.B_Copy.Text = "📋 Copy Set";
            this.B_Copy.UseVisualStyleBackColor = true;
            this.B_Copy.Click += new System.EventHandler(this.B_Copy_Click);
            // 
            // B_Use
            // 
            this.B_Use.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.B_Use.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.B_Use.Location = new System.Drawing.Point(478, 485);
            this.B_Use.Name = "B_Use";
            this.B_Use.Size = new System.Drawing.Size(120, 35);
            this.B_Use.TabIndex = 12;
            this.B_Use.Text = "✓ Use Selected";
            this.B_Use.UseVisualStyleBackColor = true;
            this.B_Use.Click += new System.EventHandler(this.B_Use_Click);
            // 
            // B_Close
            // 
            this.B_Close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.B_Close.Location = new System.Drawing.Point(608, 485);
            this.B_Close.Name = "B_Close";
            this.B_Close.Size = new System.Drawing.Size(120, 35);
            this.B_Close.TabIndex = 13;
            this.B_Close.Text = "Close";
            this.B_Close.UseVisualStyleBackColor = true;
            this.B_Close.Click += new System.EventHandler(this.B_Close_Click);
            // 
            // ShowdownSetStorage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(740, 542);
            this.Controls.Add(this.B_Close);
            this.Controls.Add(this.B_Use);
            this.Controls.Add(this.B_Copy);
            this.Controls.Add(this.B_ExportFile);
            this.Controls.Add(this.B_ClearAll);
            this.Controls.Add(this.B_Delete);
            this.Controls.Add(this.B_ImportFile);
            this.Controls.Add(this.B_Add);
            this.Controls.Add(this.RTB_Preview);
            this.Controls.Add(this.L_PreviewHeader);
            this.Controls.Add(this.LB_Sets);
            this.Controls.Add(this.TB_Search);
            this.Controls.Add(this.L_Count);
            this.Controls.Add(this.L_Storage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "ShowdownSetStorage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Showdown Set Storage (Max: 1500 Sets)";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListBox LB_Sets;
        private System.Windows.Forms.TextBox TB_Search;
        private System.Windows.Forms.RichTextBox RTB_Preview;
        private System.Windows.Forms.Button B_Add;
        private System.Windows.Forms.Button B_ImportFile;
        private System.Windows.Forms.Button B_Delete;
        private System.Windows.Forms.Button B_ClearAll;
        private System.Windows.Forms.Button B_ExportFile;
        private System.Windows.Forms.Button B_Copy;
        private System.Windows.Forms.Button B_Close;
        private System.Windows.Forms.Button B_Use;
        private System.Windows.Forms.Label L_Storage;
        private System.Windows.Forms.Label L_Count;
        private System.Windows.Forms.Label L_PreviewHeader;
    }
}
