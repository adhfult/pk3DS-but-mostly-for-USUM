namespace pk3DS.WinForms
{
    partial class CustomSpriteEditor
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
            this.CB_Species = new System.Windows.Forms.ComboBox();
            this.CB_Form = new System.Windows.Forms.ComboBox();
            this.PB_Sprite = new System.Windows.Forms.PictureBox();
            this.B_Upload = new System.Windows.Forms.Button();
            this.B_Remove = new System.Windows.Forms.Button();
            this.L_Status = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.PB_Sprite)).BeginInit();
            this.SuspendLayout();
            // 
            // CB_Species
            // 
            this.CB_Species.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.CB_Species.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.CB_Species.FormattingEnabled = true;
            this.CB_Species.Location = new System.Drawing.Point(82, 12);
            this.CB_Species.Name = "CB_Species";
            this.CB_Species.Size = new System.Drawing.Size(190, 21);
            this.CB_Species.TabIndex = 0;
            // 
            // CB_Form
            // 
            this.CB_Form.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CB_Form.FormattingEnabled = true;
            this.CB_Form.Location = new System.Drawing.Point(82, 39);
            this.CB_Form.Name = "CB_Form";
            this.CB_Form.Size = new System.Drawing.Size(190, 21);
            this.CB_Form.TabIndex = 1;
            // 

            // 
            // PB_Sprite
            // 
            this.PB_Sprite.BackColor = System.Drawing.Color.Transparent;
            this.PB_Sprite.Location = new System.Drawing.Point(12, 114);
            this.PB_Sprite.Name = "PB_Sprite";
            this.PB_Sprite.Size = new System.Drawing.Size(160, 160);
            this.PB_Sprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.PB_Sprite.TabIndex = 3;
            this.PB_Sprite.TabStop = false;
            // 
            // B_Upload
            // 
            this.B_Upload.Location = new System.Drawing.Point(178, 114);
            this.B_Upload.Name = "B_Upload";
            this.B_Upload.Size = new System.Drawing.Size(117, 34);
            this.B_Upload.TabIndex = 4;
            this.B_Upload.Text = "Upload Custom Sprite";
            this.B_Upload.UseVisualStyleBackColor = true;
            // 
            // B_Remove
            // 
            this.B_Remove.Location = new System.Drawing.Point(178, 154);
            this.B_Remove.Name = "B_Remove";
            this.B_Remove.Size = new System.Drawing.Size(117, 34);
            this.B_Remove.TabIndex = 5;
            this.B_Remove.Text = "Remove Custom";
            this.B_Remove.UseVisualStyleBackColor = true;
            // 
            // L_Status
            // 
            this.L_Status.AutoSize = true;
            this.L_Status.Location = new System.Drawing.Point(178, 204);
            this.L_Status.Name = "L_Status";
            this.L_Status.Size = new System.Drawing.Size(71, 13);
            this.L_Status.TabIndex = 6;
            this.L_Status.Text = "Default Sprite";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(28, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "Species:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(43, 42);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(33, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Form:";
            // 

            // 
            // CustomSpriteEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(309, 290);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.L_Status);
            this.Controls.Add(this.B_Remove);
            this.Controls.Add(this.B_Upload);
            this.Controls.Add(this.PB_Sprite);
            this.Controls.Add(this.CB_Form);
            this.Controls.Add(this.CB_Species);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "CustomSpriteEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Custom Sprites";
            ((System.ComponentModel.ISupportInitialize)(this.PB_Sprite)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox CB_Species;
        private System.Windows.Forms.ComboBox CB_Form;
        private System.Windows.Forms.PictureBox PB_Sprite;
        private System.Windows.Forms.Button B_Upload;
        private System.Windows.Forms.Button B_Remove;
        private System.Windows.Forms.Label L_Status;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}
