namespace MathSample
{
    partial class FormMathSample
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMathSample));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnLoad = new System.Windows.Forms.ToolStripButton();
            this.btnNew = new System.Windows.Forms.ToolStripButton();
            this.controlNodeEditor = new SampleCommon.ControlNodeEditor();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabMeasurements = new System.Windows.Forms.TabPage();
            this.btnUpdateMeasurements = new System.Windows.Forms.Button();
            this.txtMeasurements = new System.Windows.Forms.TextBox();
            this.tabParts = new System.Windows.Forms.TabPage();
            this.txtParts = new System.Windows.Forms.TextBox();
            this.tabVariables = new System.Windows.Forms.TabPage();
            this.btnUpdateVariables = new System.Windows.Forms.Button();
            this.txtVariables = new System.Windows.Forms.TextBox();
            this.tabFeatureFlags = new System.Windows.Forms.TabPage();
            this.btnUpdateFeatureFlags = new System.Windows.Forms.Button();
            this.txtFeatureFlags = new System.Windows.Forms.TextBox();
            this.tabNodes = new System.Windows.Forms.TabPage();
            this.ntbNodes = new NodeEditor.NodeToolboxPanel();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabMeasurements.SuspendLayout();
            this.tabParts.SuspendLayout();
            this.tabVariables.SuspendLayout();
            this.tabFeatureFlags.SuspendLayout();
            this.tabNodes.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSave,
            this.btnLoad,
            this.btnNew});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(957, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnSave
            // 
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(35, 22);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnLoad
            // 
            this.btnLoad.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLoad.Image = ((System.Drawing.Image)(resources.GetObject("btnLoad.Image")));
            this.btnLoad.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(37, 22);
            this.btnLoad.Text = "Load";
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);
            // 
            // btnNew
            // 
            this.btnNew.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnNew.Image = ((System.Drawing.Image)(resources.GetObject("btnNew.Image")));
            this.btnNew.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(35, 22);
            this.btnNew.Text = "New";
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // controlNodeEditor
            // 
            this.controlNodeEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.controlNodeEditor.Location = new System.Drawing.Point(0, 0);
            this.controlNodeEditor.Name = "controlNodeEditor";
            this.controlNodeEditor.Size = new System.Drawing.Size(805, 485);
            this.controlNodeEditor.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.controlNodeEditor);
            this.splitContainer1.Size = new System.Drawing.Size(957, 485);
            this.splitContainer1.SplitterDistance = 148;
            this.splitContainer1.TabIndex = 2;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabNodes);
            this.tabControl1.Controls.Add(this.tabMeasurements);
            this.tabControl1.Controls.Add(this.tabParts);
            this.tabControl1.Controls.Add(this.tabVariables);
            this.tabControl1.Controls.Add(this.tabFeatureFlags);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(148, 485);
            this.tabControl1.TabIndex = 0;
            // 
            // tabMeasurements
            // 
            this.tabMeasurements.Controls.Add(this.btnUpdateMeasurements);
            this.tabMeasurements.Controls.Add(this.txtMeasurements);
            this.tabMeasurements.Location = new System.Drawing.Point(4, 22);
            this.tabMeasurements.Name = "tabMeasurements";
            this.tabMeasurements.Padding = new System.Windows.Forms.Padding(3);
            this.tabMeasurements.Size = new System.Drawing.Size(140, 459);
            this.tabMeasurements.TabIndex = 0;
            this.tabMeasurements.Text = "Measurements";
            this.tabMeasurements.UseVisualStyleBackColor = true;
            // 
            // btnUpdateMeasurements
            // 
            this.btnUpdateMeasurements.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUpdateMeasurements.Location = new System.Drawing.Point(3, 6);
            this.btnUpdateMeasurements.Name = "btnUpdateMeasurements";
            this.btnUpdateMeasurements.Size = new System.Drawing.Size(134, 23);
            this.btnUpdateMeasurements.TabIndex = 2;
            this.btnUpdateMeasurements.Text = "Update";
            this.btnUpdateMeasurements.UseVisualStyleBackColor = true;
            this.btnUpdateMeasurements.Click += new System.EventHandler(this.btnUpdateMeasurements_Click);
            // 
            // txtMeasurements
            // 
            this.txtMeasurements.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMeasurements.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtMeasurements.Location = new System.Drawing.Point(3, 32);
            this.txtMeasurements.Multiline = true;
            this.txtMeasurements.Name = "txtMeasurements";
            this.txtMeasurements.Size = new System.Drawing.Size(134, 424);
            this.txtMeasurements.TabIndex = 0;
            // 
            // tabParts
            // 
            this.tabParts.Controls.Add(this.txtParts);
            this.tabParts.Location = new System.Drawing.Point(4, 22);
            this.tabParts.Name = "tabParts";
            this.tabParts.Padding = new System.Windows.Forms.Padding(3);
            this.tabParts.Size = new System.Drawing.Size(140, 459);
            this.tabParts.TabIndex = 1;
            this.tabParts.Text = "Parts";
            this.tabParts.UseVisualStyleBackColor = true;
            // 
            // txtParts
            // 
            this.txtParts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtParts.Location = new System.Drawing.Point(3, 3);
            this.txtParts.Multiline = true;
            this.txtParts.Name = "txtParts";
            this.txtParts.Size = new System.Drawing.Size(134, 453);
            this.txtParts.TabIndex = 0;
            // 
            // tabVariables
            // 
            this.tabVariables.Controls.Add(this.btnUpdateVariables);
            this.tabVariables.Controls.Add(this.txtVariables);
            this.tabVariables.Location = new System.Drawing.Point(4, 22);
            this.tabVariables.Name = "tabVariables";
            this.tabVariables.Padding = new System.Windows.Forms.Padding(3);
            this.tabVariables.Size = new System.Drawing.Size(140, 459);
            this.tabVariables.TabIndex = 2;
            this.tabVariables.Text = "Variables";
            this.tabVariables.UseVisualStyleBackColor = true;
            // 
            // btnUpdateVariables
            // 
            this.btnUpdateVariables.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUpdateVariables.Location = new System.Drawing.Point(3, 4);
            this.btnUpdateVariables.Name = "btnUpdateVariables";
            this.btnUpdateVariables.Size = new System.Drawing.Size(134, 23);
            this.btnUpdateVariables.TabIndex = 4;
            this.btnUpdateVariables.Text = "Update";
            this.btnUpdateVariables.UseVisualStyleBackColor = true;
            this.btnUpdateVariables.Click += new System.EventHandler(this.btnUpdateVariables_Click);
            // 
            // txtVariables
            // 
            this.txtVariables.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtVariables.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtVariables.Location = new System.Drawing.Point(3, 30);
            this.txtVariables.Multiline = true;
            this.txtVariables.Name = "txtVariables";
            this.txtVariables.Size = new System.Drawing.Size(134, 424);
            this.txtVariables.TabIndex = 3;
            // 
            // tabFeatureFlags
            // 
            this.tabFeatureFlags.Controls.Add(this.btnUpdateFeatureFlags);
            this.tabFeatureFlags.Controls.Add(this.txtFeatureFlags);
            this.tabFeatureFlags.Location = new System.Drawing.Point(4, 22);
            this.tabFeatureFlags.Name = "tabFeatureFlags";
            this.tabFeatureFlags.Padding = new System.Windows.Forms.Padding(3);
            this.tabFeatureFlags.Size = new System.Drawing.Size(140, 459);
            this.tabFeatureFlags.TabIndex = 3;
            this.tabFeatureFlags.Text = "Feature Flags";
            this.tabFeatureFlags.UseVisualStyleBackColor = true;
            // 
            // btnUpdateFeatureFlags
            // 
            this.btnUpdateFeatureFlags.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUpdateFeatureFlags.Location = new System.Drawing.Point(3, 4);
            this.btnUpdateFeatureFlags.Name = "btnUpdateFeatureFlags";
            this.btnUpdateFeatureFlags.Size = new System.Drawing.Size(134, 23);
            this.btnUpdateFeatureFlags.TabIndex = 4;
            this.btnUpdateFeatureFlags.Text = "Update";
            this.btnUpdateFeatureFlags.UseVisualStyleBackColor = true;
            this.btnUpdateFeatureFlags.Click += new System.EventHandler(this.btnUpdateFeatureFlags_Click);
            // 
            // txtFeatureFlags
            // 
            this.txtFeatureFlags.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFeatureFlags.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtFeatureFlags.Location = new System.Drawing.Point(3, 30);
            this.txtFeatureFlags.Multiline = true;
            this.txtFeatureFlags.Name = "txtFeatureFlags";
            this.txtFeatureFlags.Size = new System.Drawing.Size(134, 424);
            this.txtFeatureFlags.TabIndex = 3;
            // 
            // tabNodes
            // 
            this.tabNodes.Controls.Add(this.ntbNodes);
            this.tabNodes.Location = new System.Drawing.Point(4, 22);
            this.tabNodes.Name = "tabNodes";
            this.tabNodes.Padding = new System.Windows.Forms.Padding(3);
            this.tabNodes.Size = new System.Drawing.Size(140, 459);
            this.tabNodes.TabIndex = 4;
            this.tabNodes.Text = "Toolbox";
            this.tabNodes.UseVisualStyleBackColor = true;
            // 
            // ntbNodes
            // 
            this.ntbNodes.Context = null;
            this.ntbNodes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ntbNodes.Location = new System.Drawing.Point(3, 3);
            this.ntbNodes.Name = "ntbNodes";
            this.ntbNodes.Size = new System.Drawing.Size(134, 453);
            this.ntbNodes.TabIndex = 0;
            // 
            // FormMathSample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(957, 510);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "FormMathSample";
            this.Text = "NodeEditor WinForms - Math Sample";
            this.Load += new System.EventHandler(this.FormMathSample_Load);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabMeasurements.ResumeLayout(false);
            this.tabMeasurements.PerformLayout();
            this.tabParts.ResumeLayout(false);
            this.tabParts.PerformLayout();
            this.tabVariables.ResumeLayout(false);
            this.tabVariables.PerformLayout();
            this.tabFeatureFlags.ResumeLayout(false);
            this.tabFeatureFlags.PerformLayout();
            this.tabNodes.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private SampleCommon.ControlNodeEditor controlNodeEditor;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripButton btnLoad;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabMeasurements;
        private System.Windows.Forms.TabPage tabParts;
        private System.Windows.Forms.TextBox txtMeasurements;
        private System.Windows.Forms.TextBox txtParts;
        private System.Windows.Forms.ToolStripButton btnNew;
        private System.Windows.Forms.Button btnUpdateMeasurements;
        private System.Windows.Forms.TabPage tabVariables;
        private System.Windows.Forms.Button btnUpdateVariables;
        private System.Windows.Forms.TextBox txtVariables;
        private System.Windows.Forms.TabPage tabFeatureFlags;
        private System.Windows.Forms.Button btnUpdateFeatureFlags;
        private System.Windows.Forms.TextBox txtFeatureFlags;
        private System.Windows.Forms.TabPage tabNodes;
        private NodeEditor.NodeToolboxPanel ntbNodes;
    }
}

