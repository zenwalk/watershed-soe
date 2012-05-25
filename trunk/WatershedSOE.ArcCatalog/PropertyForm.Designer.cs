namespace WatershedSOE.ArcCatalog
{
    partial class PropertyForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PropertyForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.ComboFlowDir = new System.Windows.Forms.ComboBox();
            this.ComboFlowAcc = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.ComboExtentFeatures = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(221, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Select layers required for catchment definition";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Flow Direction";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 72);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Flow Accumulation";
            // 
            // ComboFlowDir
            // 
            this.ComboFlowDir.FormattingEnabled = true;
            this.ComboFlowDir.Location = new System.Drawing.Point(115, 41);
            this.ComboFlowDir.Name = "ComboFlowDir";
            this.ComboFlowDir.Size = new System.Drawing.Size(121, 21);
            this.ComboFlowDir.TabIndex = 3;
            this.ComboFlowDir.SelectedIndexChanged += new System.EventHandler(this.ComboFlowDir_SelectedIndexChanged);
            // 
            // ComboFlowAcc
            // 
            this.ComboFlowAcc.FormattingEnabled = true;
            this.ComboFlowAcc.Location = new System.Drawing.Point(115, 69);
            this.ComboFlowAcc.Name = "ComboFlowAcc";
            this.ComboFlowAcc.Size = new System.Drawing.Size(121, 21);
            this.ComboFlowAcc.TabIndex = 4;
            this.ComboFlowAcc.SelectedIndexChanged += new System.EventHandler(this.ComboFlowAcc_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 163);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(230, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "All other map layers will be used to extract data.";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 133);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(78, 13);
            this.label5.TabIndex = 6;
            this.label5.Text = "Extent features";
            // 
            // ComboExtentFeatures
            // 
            this.ComboExtentFeatures.FormattingEnabled = true;
            this.ComboExtentFeatures.Location = new System.Drawing.Point(115, 130);
            this.ComboExtentFeatures.Name = "ComboExtentFeatures";
            this.ComboExtentFeatures.Size = new System.Drawing.Size(121, 21);
            this.ComboExtentFeatures.TabIndex = 7;
            this.ComboExtentFeatures.SelectedIndexChanged += new System.EventHandler(this.ComboExtentFeatures_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(14, 99);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(274, 26);
            this.label6.TabIndex = 8;
            this.label6.Text = "Optionally, set layer containing non-overlapping polygons\r\nthat will be used to l" +
                "imit the analysis extent";
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.Info;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Enabled = false;
            this.textBox1.Location = new System.Drawing.Point(15, 179);
            this.textBox1.Margin = new System.Windows.Forms.Padding(5, 5, 3, 3);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(351, 202);
            this.textBox1.TabIndex = 9;
            this.textBox1.Text = resources.GetString("textBox1.Text");
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // PropertyForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(378, 393);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.ComboExtentFeatures);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.ComboFlowAcc);
            this.Controls.Add(this.ComboFlowDir);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "PropertyForm";
            this.Text = "PropertyForm";
            this.Load += new System.EventHandler(this.PropertyForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox ComboFlowDir;
        private System.Windows.Forms.ComboBox ComboFlowAcc;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox ComboExtentFeatures;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBox1;
    }
}