namespace pk3DS.WinForms;

partial class ItemEditor7
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
        this.B_Table = new System.Windows.Forms.Button();
        this.PB_ItemSprite = new System.Windows.Forms.PictureBox();
        this.B_CopyTable = new System.Windows.Forms.Button();
        this.B_PasteTable = new System.Windows.Forms.Button();
        this.CB_Item = new System.Windows.Forms.ComboBox();
        this.L_Item = new System.Windows.Forms.Label();
        this.RTB = new System.Windows.Forms.RichTextBox();
        this.L_Index = new System.Windows.Forms.Label();
        this.Grid = new System.Windows.Forms.PropertyGrid();
        ((System.ComponentModel.ISupportInitialize)(this.PB_ItemSprite)).BeginInit();
        this.SuspendLayout();
        // 
        // CB_Item
        // 
        this.CB_Item.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
        this.CB_Item.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
        this.CB_Item.DropDownWidth = 120;
        this.CB_Item.FormattingEnabled = true;
        this.CB_Item.Location = new System.Drawing.Point(71, 10);
        this.CB_Item.Name = "CB_Item";
        this.CB_Item.Size = new System.Drawing.Size(144, 21);
        this.CB_Item.TabIndex = 1;
        this.CB_Item.SelectedIndexChanged += new System.EventHandler(this.ChangeEntry);
        // 
        // L_Item
        // 
        this.L_Item.Location = new System.Drawing.Point(12, 12);
        this.L_Item.Name = "L_Item";
        this.L_Item.Size = new System.Drawing.Size(51, 13);
        this.L_Item.TabIndex = 2;
        this.L_Item.Text = "Item:";
        this.L_Item.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // RTB
        // 
        this.RTB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.RTB.Location = new System.Drawing.Point(11, 68);
        this.RTB.Name = "RTB";
        this.RTB.ReadOnly = true;
        this.RTB.Size = new System.Drawing.Size(460, 51);
        this.RTB.TabIndex = 38;
        this.RTB.Text = "";
        // 
        // L_Index
        // 
        this.L_Index.AutoSize = true;
        this.L_Index.Location = new System.Drawing.Point(12, 38);
        this.L_Index.Name = "L_Index";
        this.L_Index.Size = new System.Drawing.Size(39, 13);
        this.L_Index.TabIndex = 46;
        this.L_Index.Text = "Index: ";
        // 
        // Grid
        // 
        this.Grid.Location = new System.Drawing.Point(11, 125);
        this.Grid.Size = new System.Drawing.Size(460, 300);
        this.Grid.TabIndex = 47;
        this.PB_ItemSprite.Location = new System.Drawing.Point(400, 5);
        this.PB_ItemSprite.Name = "PB_ItemSprite";
        this.PB_ItemSprite.Size = new System.Drawing.Size(50, 50);
        this.PB_ItemSprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_ItemSprite.BorderStyle = System.Windows.Forms.BorderStyle.None;
        // 
        // B_Table
        // 
        // Two button rows sit between the combo box (ends at x=215) and the sprite (starts at
        // x=400), so every button here has to live inside 235..390 on row one and 115..390 on row
        // two. Widths below are chosen to tile those spans exactly, with a 6px gutter.
        this.B_Table.Location = new System.Drawing.Point(235, 10);
        this.B_Table.Name = "B_Table";
        this.B_Table.Size = new System.Drawing.Size(78, 23);
        this.B_Table.TabIndex = 48;
        this.B_Table.Text = "Export Table";
        this.B_Table.UseVisualStyleBackColor = true;
        this.B_Table.Click += new System.EventHandler(this.B_Table_Click);

        // B_CopyTable
        this.B_CopyTable.Location = new System.Drawing.Point(235, 36);
        this.B_CopyTable.Name = "B_CopyTable";
        this.B_CopyTable.Size = new System.Drawing.Size(75, 23);
        this.B_CopyTable.TabIndex = 50;
        this.B_CopyTable.Text = "Copy";
        this.B_CopyTable.UseVisualStyleBackColor = true;
        this.B_CopyTable.Click += new System.EventHandler(this.B_CopyTable_Click);

        this.B_PasteTable.Location = new System.Drawing.Point(315, 36);
        this.B_PasteTable.Name = "B_PasteTable";
        this.B_PasteTable.Size = new System.Drawing.Size(75, 23);
        this.B_PasteTable.TabIndex = 51;
        this.B_PasteTable.Text = "Paste";
        this.B_PasteTable.UseVisualStyleBackColor = true;
        this.B_PasteTable.Click += new System.EventHandler(this.B_PasteTable_Click);

        this.B_MakeMega = new System.Windows.Forms.Button();
        this.B_MakeMega.Location = new System.Drawing.Point(115, 36);
        this.B_MakeMega.Name = "B_MakeMega";
        this.B_MakeMega.Size = new System.Drawing.Size(115, 23);
        this.B_MakeMega.TabIndex = 52;
        this.B_MakeMega.Text = "Make Mega Stone";
        this.B_MakeMega.UseVisualStyleBackColor = true;
        this.B_MakeMega.Click += new System.EventHandler(this.B_MakeMega_Click);

        this.B_AddItem = new System.Windows.Forms.Button();
        this.B_AddItem.Location = new System.Drawing.Point(319, 10);
        this.B_AddItem.Name = "B_AddItem";
        this.B_AddItem.Size = new System.Drawing.Size(71, 23);
        this.B_AddItem.TabIndex = 49;
        this.B_AddItem.Text = "Add Items";
        this.B_AddItem.UseVisualStyleBackColor = true;
        this.B_AddItem.Click += new System.EventHandler(this.B_AddItem_Click);

        //
        // ItemEditor7
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(484, 440);
        this.Controls.Add(this.PB_ItemSprite);
        this.Controls.Add(this.B_AddItem);
        this.Controls.Add(this.B_MakeMega);
        this.Controls.Add(this.B_CopyTable);
        this.Controls.Add(this.B_PasteTable);
        this.Controls.Add(this.B_Table);
        this.Controls.Add(this.Grid);
        this.Controls.Add(this.L_Index);
        this.Controls.Add(this.RTB);
        this.Controls.Add(this.L_Item);
        this.Controls.Add(this.CB_Item);
        this.MaximizeBox = false;
        this.MinimumSize = new System.Drawing.Size(450, 420);
        this.Name = "ItemEditor7";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Item Editor";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.ComboBox CB_Item;
    private System.Windows.Forms.Label L_Item;
    private System.Windows.Forms.RichTextBox RTB;
    private System.Windows.Forms.Label L_Index;
    private System.Windows.Forms.PropertyGrid Grid;
    private System.Windows.Forms.Button B_Table;
    private System.Windows.Forms.PictureBox PB_ItemSprite;
    private System.Windows.Forms.Button B_CopyTable;
    private System.Windows.Forms.Button B_PasteTable;
    private System.Windows.Forms.Button B_MakeMega;
    private System.Windows.Forms.Button B_AddItem;
}
