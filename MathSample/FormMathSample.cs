using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MathSample
{
    public partial class FormMathSample : Form
    {
        //Context that will be used for our nodes
        PartCalculation context = new PartCalculation();

        public FormMathSample()
        {
            InitializeComponent();
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            //Context assignment
            controlNodeEditor.nodesControl.Context = context;
            controlNodeEditor.nodesControl.OnNodeContextSelected += NodesControlOnOnNodeContextSelected; 
        }

        private void NodesControlOnOnNodeContextSelected(object o)
        {
            controlNodeEditor.propertyGrid.SelectedObject = o;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Node Graph Files (*.json)|*.json|Binary Node Graph Files (*.nod)|*.nod|All Files (*.*)|*.*";
            saveFileDialog.DefaultExt = "json";
            saveFileDialog.Title = "Save Node Graph";
            
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (saveFileDialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Save as JSON
                        string jsonData = controlNodeEditor.nodesControl.SerializeToJson();
                        File.WriteAllText(saveFileDialog.FileName, jsonData);
                    }
                    else
                    {
                        // Save as binary (legacy)
                        byte[] graphData = controlNodeEditor.nodesControl.Serialize();
                        File.WriteAllBytes(saveFileDialog.FileName, graphData);
                    }
                    MessageBox.Show("Node graph saved successfully!", "Save Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving node graph: {ex.Message}", "Save Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Node Graph Files (*.json)|*.json|Binary Node Graph Files (*.nod)|*.nod|All Files (*.*)|*.*";
            openFileDialog.DefaultExt = "json";
            openFileDialog.Title = "Load Node Graph";
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (openFileDialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Load from JSON
                        string jsonData = File.ReadAllText(openFileDialog.FileName);
                        controlNodeEditor.nodesControl.DeserializeFromJson(jsonData);
                    }
                    else
                    {
                        // Load from binary (legacy)
                        byte[] graphData = File.ReadAllBytes(openFileDialog.FileName);
                        controlNodeEditor.nodesControl.Deserialize(graphData);
                    }
                    MessageBox.Show("Node graph loaded successfully!", "Load Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading node graph: {ex.Message}", "Load Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
