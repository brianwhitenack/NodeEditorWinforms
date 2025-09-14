using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Newtonsoft.Json;

namespace MathSample
{
    public partial class FormMathSample : Form
    {
        //Context that will be used for our nodes
        PartCalculation context = new PartCalculation();

        public FormMathSample()
        {
            InitializeComponent();

            context.Measurements = new List<Measurement>()
            {
                new Measurement()
                {
                    Type = "Beam",
                    Length = 50,
                    Count = 2,
                    Selections = new Dictionary<string, object>()
                    {
                        { "BeamType", "Header" },
                        { "Material", "LVL" },
                        { "Grade", "#1" },
                        { "Plies", "3" },
                        { "Thickness", 2 },
                        { "Width", 4 }
                    }
                },
                new Measurement()
                {
                    Type = "Beam",
                    Length = 14.5,
                    Count = 2,
                    Selections = new Dictionary<string, object>()
                    {
                        { "BeamType", "Header" },
                        { "Material", "LVL" },
                        { "Grade", "#1" },
                        { "Plies", "1" },
                        { "Thickness", 2 },
                        { "Width", 4 }
                    }
                },
                new Measurement()
                {
                    Type = "Siding",
                    Area = 1200,
                    Count = 2,
                    Selections = new Dictionary<string, object>()
                    {
                        { "Selection", "Brick" },
                    }
                },
                new Measurement()
                {
                    Type = "Siding",
                    Area = 100,
                    Count = 2,
                    Selections = new Dictionary<string, object>()
                    {
                        { "Selection", "LP" },
                    }
                }
            };

            txtMeasurements.Text = JsonConvert.SerializeObject(context.Measurements, Formatting.Indented);

            context.OnExecutionFinished += Context_OnExecutionFinished;
        }

        private void Context_OnExecutionFinished()
        {
            txtParts.Text = JsonConvert.SerializeObject(context.Parts, Formatting.Indented);
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            //Context assignment
            controlNodeEditor.nodesControl.Context = context;
            controlNodeEditor.nodesControl.OnNodeContextSelected += NodesControlOnOnNodeContextSelected;
            
            // Add default nodes
            AddDefaultNodes();
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading node graph: {ex.Message}", "Load Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AddDefaultNodes()
        {
            // Only add default nodes if the graph is empty
            var existingNodes = controlNodeEditor.nodesControl.GetNodes();
            if (existingNodes.Count > 0) return;

            // Add Starter node by method name
            controlNodeEditor.nodesControl.AddNodeByMethodName("Starter", 50, 50);
            
            // Add Parts List node by method name
            controlNodeEditor.nodesControl.AddNodeByMethodName("PartsList", 300, 50);
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            // Clear the current node graph
            controlNodeEditor.nodesControl.Clear();

            // Add default nodes for a new graph
            AddDefaultNodes();
        }

        private void btnUpdateMeasurements_Click(object sender, EventArgs e)
        {
            context.Measurements = JsonConvert.DeserializeObject<List<Measurement>>(txtMeasurements.Text);
        }
    }
}
