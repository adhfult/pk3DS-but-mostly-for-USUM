namespace pk3DS.WinForms;

partial class PickupEditor7
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
        this.B_Save = new System.Windows.Forms.Button();
        this.B_Cancel = new System.Windows.Forms.Button();
        this.B_Randomize = new System.Windows.Forms.Button();
        this.dgvCommon = new System.Windows.Forms.DataGridView();
        this.B_AddRow = new System.Windows.Forms.Button();
        this.B_DeleteRow = new System.Windows.Forms.Button();
        this.PB_Item = new System.Windows.Forms.PictureBox();
        this.FLP_Pokemon = new System.Windows.Forms.FlowLayoutPanel();
        this.L_Pokemon = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.dgvCommon)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_Item)).BeginInit();
        this.SuspendLayout();
        // 
        // B_Save
        // 
        this.B_Save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.B_Save.Location = new System.Drawing.Point(717, 331);
        this.B_Save.Name = "B_Save";
        this.B_Save.Size = new System.Drawing.Size(75, 23);
        this.B_Save.TabIndex = 7;
        this.B_Save.Text = "Save";
        this.B_Save.UseVisualStyleBackColor = true;
        this.B_Save.Click += new System.EventHandler(this.B_Save_Click);
        // 
        // B_Cancel
        // 
        this.B_Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.B_Cancel.Location = new System.Drawing.Point(636, 331);
        this.B_Cancel.Name = "B_Cancel";
        this.B_Cancel.Size = new System.Drawing.Size(75, 23);
        this.B_Cancel.TabIndex = 8;
        this.B_Cancel.Text = "Cancel";
        this.B_Cancel.UseVisualStyleBackColor = true;
        this.B_Cancel.Click += new System.EventHandler(this.B_Cancel_Click);
        // 
        // B_Randomize
        // 
        this.B_Randomize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_Randomize.Location = new System.Drawing.Point(9, 331);
        this.B_Randomize.Name = "B_Randomize";
        this.B_Randomize.Size = new System.Drawing.Size(75, 23);
        this.B_Randomize.TabIndex = 9;
        this.B_Randomize.Text = "Randomize";
        this.B_Randomize.UseVisualStyleBackColor = true;
        this.B_Randomize.Click += new System.EventHandler(this.B_Randomize_Click);
        // 
        // dgvCommon
        // 
        this.dgvCommon.AllowUserToResizeColumns = false;
        this.dgvCommon.AllowUserToResizeRows = false;
        this.dgvCommon.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
                                                                       | System.Windows.Forms.AnchorStyles.Left) 
                                                                      | System.Windows.Forms.AnchorStyles.Right)));
        this.dgvCommon.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dgvCommon.Location = new System.Drawing.Point(12, 12);
        this.dgvCommon.Name = "dgvCommon";
        this.dgvCommon.Size = new System.Drawing.Size(656, 300);
        this.dgvCommon.TabIndex = 10;
        // 
        // B_AddRow
        // 
        this.B_AddRow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_AddRow.Location = new System.Drawing.Point(90, 331);
        this.B_AddRow.Name = "B_AddRow";
        this.B_AddRow.Size = new System.Drawing.Size(75, 23);
        this.B_AddRow.TabIndex = 11;
        this.B_AddRow.Text = "Add Row";
        this.B_AddRow.UseVisualStyleBackColor = true;
        this.B_AddRow.Click += new System.EventHandler(this.B_AddRow_Click);
        // 
        // B_DeleteRow
        // 
        this.B_DeleteRow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.B_DeleteRow.Location = new System.Drawing.Point(171, 331);
        this.B_DeleteRow.Name = "B_DeleteRow";
        this.B_DeleteRow.Size = new System.Drawing.Size(75, 23);
        this.B_DeleteRow.TabIndex = 12;
        this.B_DeleteRow.Text = "Delete Row";
        this.B_DeleteRow.UseVisualStyleBackColor = true;
        this.B_DeleteRow.Click += new System.EventHandler(this.B_DeleteRow_Click);
        // 
        // PB_Item
        // 
        this.PB_Item.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.PB_Item.Location = new System.Drawing.Point(252, 319);
        this.PB_Item.Name = "PB_Item";
        this.PB_Item.Size = new System.Drawing.Size(48, 48);
        this.PB_Item.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_Item.TabIndex = 13;
        this.PB_Item.TabStop = false;
        // 
        // FLP_Pokemon
        // 
        this.FLP_Pokemon.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.FLP_Pokemon.AutoScroll = true;
        this.FLP_Pokemon.BackColor = System.Drawing.Color.White;
        this.FLP_Pokemon.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.FLP_Pokemon.Location = new System.Drawing.Point(674, 30);
        this.FLP_Pokemon.Name = "FLP_Pokemon";
        this.FLP_Pokemon.Padding = new System.Windows.Forms.Padding(5);
        this.FLP_Pokemon.Size = new System.Drawing.Size(114, 282);
        this.FLP_Pokemon.TabIndex = 14;
        // 
        // L_Pokemon
        // 
        this.L_Pokemon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.L_Pokemon.AutoSize = true;
        this.L_Pokemon.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.L_Pokemon.Location = new System.Drawing.Point(671, 14);
        this.L_Pokemon.Name = "L_Pokemon";
        this.L_Pokemon.Size = new System.Drawing.Size(125, 13);
        this.L_Pokemon.TabIndex = 15;
        this.L_Pokemon.Text = "Pokemon with Pickup";
        // 
        // PickupEditor7
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(800, 375);
        this.Controls.Add(this.L_Pokemon);
        this.Controls.Add(this.FLP_Pokemon);
        this.Controls.Add(this.PB_Item);
        this.Controls.Add(this.B_DeleteRow);
        this.Controls.Add(this.B_AddRow);
        this.Controls.Add(this.dgvCommon);
        this.Controls.Add(this.B_Randomize);
        this.Controls.Add(this.B_Cancel);
        this.Controls.Add(this.B_Save);
        this.MaximizeBox = false;
        this.MinimumSize = new System.Drawing.Size(600, 400);
        this.Name = "PickupEditor7";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Pickup Editor";
        ((System.ComponentModel.ISupportInitialize)(this.dgvCommon)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.PB_Item)).EndInit();
        this.ResumeLayout(false);

    }

    #endregion
    private System.Windows.Forms.Button B_Save;
    private System.Windows.Forms.Button B_Cancel;
    private System.Windows.Forms.Button B_Randomize;
    private System.Windows.Forms.DataGridView dgvCommon;
    private System.Windows.Forms.Button B_AddRow;
    private System.Windows.Forms.Button B_DeleteRow;
    private System.Windows.Forms.PictureBox PB_Item;
    private System.Windows.Forms.FlowLayoutPanel FLP_Pokemon;
    private System.Windows.Forms.Label L_Pokemon;
}
