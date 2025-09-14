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
            this.controlNodeEditor = new SampleCommon.ControlNodeEditor();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabMeasurements = new System.Windows.Forms.TabPage();
            this.tabParts = new System.Windows.Forms.TabPage();
            this.txtMeasurements = new System.Windows.Forms.TextBox();
            this.txtParts = new System.Windows.Forms.TextBox();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabMeasurements.SuspendLayout();
            this.tabParts.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSave,
            this.btnLoad});
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
            this.tabControl1.Controls.Add(this.tabMeasurements);
            this.tabControl1.Controls.Add(this.tabParts);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(148, 485);
            this.tabControl1.TabIndex = 0;
            // 
            // tabMeasurements
            // 
            this.tabMeasurements.Controls.Add(this.txtMeasurements);
            this.tabMeasurements.Location = new System.Drawing.Point(4, 22);
            this.tabMeasurements.Name = "tabMeasurements";
            this.tabMeasurements.Padding = new System.Windows.Forms.Padding(3);
            this.tabMeasurements.Size = new System.Drawing.Size(140, 459);
            this.tabMeasurements.TabIndex = 0;
            this.tabMeasurements.Text = "Measurements";
            this.tabMeasurements.UseVisualStyleBackColor = true;
            // 
            // tabParts
            // 
            this.tabParts.Controls.Add(this.txtParts);
            this.tabParts.Location = new System.Drawing.Point(4, 22);
            this.tabParts.Name = "tabParts";
            this.tabParts.Padding = new System.Windows.Forms.Padding(3);
            this.tabParts.Size = new System.Drawing.Size(311, 459);
            this.tabParts.TabIndex = 1;
            this.tabParts.Text = "Parts";
            this.tabParts.UseVisualStyleBackColor = true;
            // 
            // txtMeasurements
            // 
            this.txtMeasurements.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtMeasurements.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtMeasurements.Location = new System.Drawing.Point(3, 3);
            this.txtMeasurements.Multiline = true;
            this.txtMeasurements.Name = "txtMeasurements";
            this.txtMeasurements.Size = new System.Drawing.Size(134, 453);
            this.txtMeasurements.TabIndex = 0;
            // 
            // txtParts
            // 
            this.txtParts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtParts.Location = new System.Drawing.Point(3, 3);
            this.txtParts.Multiline = true;
            this.txtParts.Name = "txtParts";
            this.txtParts.Size = new System.Drawing.Size(305, 453);
            this.txtParts.TabIndex = 0;
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
    }
}

