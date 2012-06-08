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
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.LayerName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.LayerAvail = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.LayerParam = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CatField = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ValField = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.MeasField = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioReadMap = new System.Windows.Forms.RadioButton();
            this.radioUseGrid = new System.Windows.Forms.RadioButton();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label6 = new System.Windows.Forms.Label();
            this.ComboExtentFeatures = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ComboFlowAcc = new System.Windows.Forms.ComboBox();
            this.ComboFlowDir = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.LayerName,
            this.LayerAvail,
            this.LayerParam,
            this.CatField,
            this.ValField,
            this.MeasField});
            this.dataGridView1.Location = new System.Drawing.Point(6, 42);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(542, 168);
            this.dataGridView1.TabIndex = 10;
            // 
            // LayerName
            // 
            this.LayerName.HeaderText = "Layer";
            this.LayerName.Name = "LayerName";
            this.LayerName.ReadOnly = true;
            this.LayerName.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.LayerName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.LayerName.ToolTipText = "Name of the map layer";
            this.LayerName.Width = 125;
            // 
            // LayerAvail
            // 
            this.LayerAvail.HeaderText = "Use?";
            this.LayerAvail.Name = "LayerAvail";
            this.LayerAvail.ToolTipText = "Check to expose this layer as an extractable parameter";
            this.LayerAvail.Width = 40;
            // 
            // LayerParam
            // 
            this.LayerParam.HeaderText = "Parameter";
            this.LayerParam.MaxInputLength = 6;
            this.LayerParam.Name = "LayerParam";
            this.LayerParam.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.LayerParam.ToolTipText = "Enter a short ID (max 6 chars) that will determine the REST parameter name to be " +
                "exposed for this layer, and the prefix for the associated columns in the output";
            this.LayerParam.Width = 70;
            // 
            // CatField
            // 
            this.CatField.HeaderText = "Category field";
            this.CatField.Name = "CatField";
            this.CatField.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.CatField.ToolTipText = "Optionally, choose a field to group the summary by";
            this.CatField.Width = 90;
            // 
            // ValField
            // 
            this.ValField.HeaderText = "Value Field";
            this.ValField.Name = "ValField";
            this.ValField.ToolTipText = "Optionally, choose a field to summarise (numerically), in addition to length / ar" +
                "ea";
            this.ValField.Width = 90;
            // 
            // MeasField
            // 
            this.MeasField.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.MeasField.HeaderText = "Length/Area";
            this.MeasField.Name = "MeasField";
            this.MeasField.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.MeasField.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.MeasField.ToolTipText = "If a field exists with pre-calculated values for length or area you can select it" +
                " here to speed calculations.";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Location = new System.Drawing.Point(12, 10);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(562, 242);
            this.tabControl1.TabIndex = 11;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.dataGridView1);
            this.tabPage2.Controls.Add(this.groupBox1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(554, 216);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Extraction layers configuration";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioReadMap);
            this.groupBox1.Controls.Add(this.radioUseGrid);
            this.groupBox1.Location = new System.Drawing.Point(6, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(467, 33);
            this.groupBox1.TabIndex = 14;
            this.groupBox1.TabStop = false;
            // 
            // radioReadMap
            // 
            this.radioReadMap.AutoSize = true;
            this.radioReadMap.Location = new System.Drawing.Point(6, 10);
            this.radioReadMap.Name = "radioReadMap";
            this.radioReadMap.Size = new System.Drawing.Size(150, 17);
            this.radioReadMap.TabIndex = 12;
            this.radioReadMap.TabStop = true;
            this.radioReadMap.Text = "Read from Map Document";
            this.radioReadMap.UseVisualStyleBackColor = true;
            this.radioReadMap.CheckedChanged += new System.EventHandler(this.radioReadMap_CheckedChanged);
            // 
            // radioUseGrid
            // 
            this.radioUseGrid.AutoSize = true;
            this.radioUseGrid.Location = new System.Drawing.Point(163, 10);
            this.radioUseGrid.Name = "radioUseGrid";
            this.radioUseGrid.Size = new System.Drawing.Size(102, 17);
            this.radioUseGrid.TabIndex = 13;
            this.radioUseGrid.TabStop = true;
            this.radioUseGrid.Text = "Configure Below";
            this.radioUseGrid.UseVisualStyleBackColor = true;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.ComboExtentFeatures);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.ComboFlowAcc);
            this.tabPage1.Controls.Add(this.ComboFlowDir);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(554, 216);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Watershed Configuration";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 83);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(315, 26);
            this.label6.TabIndex = 16;
            this.label6.Text = "Optionally, set layer containing non-overlapping polygons that will \r\nbe used to " +
                "limit the analysis extent";
            // 
            // ComboExtentFeatures
            // 
            this.ComboExtentFeatures.FormattingEnabled = true;
            this.ComboExtentFeatures.Location = new System.Drawing.Point(108, 119);
            this.ComboExtentFeatures.Name = "ComboExtentFeatures";
            this.ComboExtentFeatures.Size = new System.Drawing.Size(121, 21);
            this.ComboExtentFeatures.TabIndex = 15;
            this.ComboExtentFeatures.SelectedIndexChanged += new System.EventHandler(this.ComboExtentFeatures_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(7, 119);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(78, 13);
            this.label5.TabIndex = 14;
            this.label5.Text = "Extent features";
            // 
            // ComboFlowAcc
            // 
            this.ComboFlowAcc.FormattingEnabled = true;
            this.ComboFlowAcc.Location = new System.Drawing.Point(108, 59);
            this.ComboFlowAcc.Name = "ComboFlowAcc";
            this.ComboFlowAcc.Size = new System.Drawing.Size(173, 21);
            this.ComboFlowAcc.TabIndex = 13;
            this.ComboFlowAcc.SelectedIndexChanged += new System.EventHandler(this.ComboFlowAcc_SelectedIndexChanged);
            // 
            // ComboFlowDir
            // 
            this.ComboFlowDir.FormattingEnabled = true;
            this.ComboFlowDir.Location = new System.Drawing.Point(108, 32);
            this.ComboFlowDir.Name = "ComboFlowDir";
            this.ComboFlowDir.Size = new System.Drawing.Size(173, 21);
            this.ComboFlowDir.TabIndex = 12;
            this.ComboFlowDir.SelectedIndexChanged += new System.EventHandler(this.ComboFlowDir_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 56);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Flow Accumulation";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Flow Direction";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(338, 26);
            this.label1.TabIndex = 9;
            this.label1.Text = "Select layers required for catchment definition. \r\nOr leave at \"NONE\" if you only" +
                " want the Extract by Polygon operation.";
            // 
            // PropertyForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(593, 264);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "PropertyForm";
            this.Text = "PropertyForm";
            this.Load += new System.EventHandler(this.PropertyForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox ComboExtentFeatures;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox ComboFlowAcc;
        private System.Windows.Forms.ComboBox ComboFlowDir;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioUseGrid;
        private System.Windows.Forms.RadioButton radioReadMap;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.DataGridViewTextBoxColumn LayerName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn LayerAvail;
        private System.Windows.Forms.DataGridViewTextBoxColumn LayerParam;
        private System.Windows.Forms.DataGridViewComboBoxColumn CatField;
        private System.Windows.Forms.DataGridViewComboBoxColumn ValField;
        private System.Windows.Forms.DataGridViewComboBoxColumn MeasField;
    }
}