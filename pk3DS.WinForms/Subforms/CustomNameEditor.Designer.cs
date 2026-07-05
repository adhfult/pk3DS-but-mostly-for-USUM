namespace pk3DS.WinForms
{
    partial class CustomNameEditor
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.LB_Entries = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.TB_CustomName = new System.Windows.Forms.TextBox();
            this.B_Save = new System.Windows.Forms.Button();
            this.B_Remove = new System.Windows.Forms.Button();
            this.L_Hint = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // LB_Entries
            this.LB_Entries.FormattingEnabled = true;
            this.LB_Entries.Location = new System.Drawing.Point(12, 40);
            this.LB_Entries.Name = "LB_Entries";
            this.LB_Entries.Size = new System.Drawing.Size(340, 382);
            this.LB_Entries.TabIndex = 0;
            this.LB_Entries.SelectedIndexChanged += new System.EventHandler(this.LB_Entries_SelectedIndexChanged);

            // label1 – header above list
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(120, 13);
            this.label1.Text = "Pokémon / Form Entry:";

            // label2 – custom name field label
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(365, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.Text = "Custom Name:";

            // TB_CustomName
            this.TB_CustomName.Location = new System.Drawing.Point(365, 58);
            this.TB_CustomName.Name = "TB_CustomName";
            this.TB_CustomName.Size = new System.Drawing.Size(220, 20);
            this.TB_CustomName.TabIndex = 1;
            this.TB_CustomName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TB_CustomName_KeyDown);

            // B_Save
            this.B_Save.Location = new System.Drawing.Point(365, 88);
            this.B_Save.Name = "B_Save";
            this.B_Save.Size = new System.Drawing.Size(105, 30);
            this.B_Save.TabIndex = 2;
            this.B_Save.Text = "Save Name";
            this.B_Save.UseVisualStyleBackColor = true;
            this.B_Save.Click += new System.EventHandler(this.B_Save_Click);

            // B_Remove
            this.B_Remove.Enabled = false;
            this.B_Remove.Location = new System.Drawing.Point(480, 88);
            this.B_Remove.Name = "B_Remove";
            this.B_Remove.Size = new System.Drawing.Size(105, 30);
            this.B_Remove.TabIndex = 3;
            this.B_Remove.Text = "Remove Name";
            this.B_Remove.UseVisualStyleBackColor = true;
            this.B_Remove.Click += new System.EventHandler(this.B_Remove_Click);

            // L_Hint
            this.L_Hint.AutoSize = false;
            this.L_Hint.Location = new System.Drawing.Point(365, 135);
            this.L_Hint.Name = "L_Hint";
            this.L_Hint.Size = new System.Drawing.Size(220, 65);
            this.L_Hint.Text = "Custom names are displayed in the editor only and are never written to the ROM.\r\n\r\nPress Enter or click Save to apply.";

            // CustomNameEditor
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 445);
            this.Controls.Add(this.L_Hint);
            this.Controls.Add(this.B_Remove);
            this.Controls.Add(this.B_Save);
            this.Controls.Add(this.TB_CustomName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.LB_Entries);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "CustomNameEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Custom Pokémon Names";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.ListBox LB_Entries;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox TB_CustomName;
        private System.Windows.Forms.Button B_Save;
        private System.Windows.Forms.Button B_Remove;
        private System.Windows.Forms.Label L_Hint;
    }
}
