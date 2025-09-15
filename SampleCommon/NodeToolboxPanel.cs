using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NodeEditor;

namespace SampleCommon
{
    /// <summary>
    /// A toolbox panel that displays available node types with collapsible categories and drag-and-drop support
    /// </summary>
    public partial class NodeToolboxPanel : UserControl
    {
        private Dictionary<string, CategoryPanel> categoryPanels = new Dictionary<string, CategoryPanel>();
        private Panel mainPanel;

        public INodesContext Context { get; set; }

        public NodeToolboxPanel()
        {
            InitializeComponent();
            SetupMainPanel();
        }

        private void SetupMainPanel()
        {
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            Controls.Add(mainPanel);
        }

        /// <summary>
        /// Refreshes the toolbox with available nodes from the context
        /// </summary>
        public void RefreshNodes()
        {
            mainPanel.Controls.Clear();
            categoryPanels.Clear();

            if (Context == null) return;

            // Get all nodes and group by category
            MethodInfo[] methods = Context.GetType().GetMethods();
            var nodesByCategory = methods
                .Where(m => m.GetCustomAttributes(typeof(NodeAttribute), false).Length > 0)
                .Select(m => new
                {
                    Method = m,
                    Attribute = m.GetCustomAttributes(typeof(NodeAttribute), false)
                        .Cast<NodeAttribute>()
                        .FirstOrDefault()
                })
                .Where(x => x.Attribute != null)
                .GroupBy(x => x.Attribute.Category ?? "General")
                .OrderBy(g => g.Key);

            int yPosition = 0;

            foreach (var categoryGroup in nodesByCategory)
            {
                // Create category panel
                CategoryPanel categoryPanel = new CategoryPanel(categoryGroup.Key)
                {
                    Location = new Point(0, yPosition),
                    Width = mainPanel.ClientSize.Width,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                // Add nodes to category
                foreach (var nodeInfo in categoryGroup.OrderBy(x => x.Attribute.Name))
                {
                    NodeToolboxItem item = new NodeToolboxItem
                    {
                        Method = nodeInfo.Method,
                        Attribute = nodeInfo.Attribute,
                        DisplayName = nodeInfo.Attribute.Name ?? nodeInfo.Method.Name
                    };

                    MiniNodeControl miniNode = new MiniNodeControl(item, Context)
                    {
                        Margin = new Padding(5)
                    };

                    miniNode.MouseDown += MiniNode_MouseDown;
                    miniNode.MouseMove += MiniNode_MouseMove;

                    categoryPanel.AddNode(miniNode);
                }

                categoryPanel.CollapsedChanged += (sender, e) => RecalculateLayout();
                categoryPanels[categoryGroup.Key] = categoryPanel;
                mainPanel.Controls.Add(categoryPanel);

                yPosition += categoryPanel.Height + 5;
            }
        }

        private void RecalculateLayout()
        {
            int yPosition = 0;
            foreach (Control control in mainPanel.Controls)
            {
                if (control is CategoryPanel categoryPanel)
                {
                    categoryPanel.Location = new Point(0, yPosition);
                    yPosition += categoryPanel.Height + 5;
                }
            }
        }

        private Point dragStartPoint;
        private MiniNodeControl draggingNode;

        private void MiniNode_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingNode = sender as MiniNodeControl;
                dragStartPoint = e.Location;
            }
        }

        private void MiniNode_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingNode != null && e.Button == MouseButtons.Left)
            {
                // Check if we've moved enough to start a drag operation
                int deltaX = Math.Abs(e.Location.X - dragStartPoint.X);
                int deltaY = Math.Abs(e.Location.Y - dragStartPoint.Y);

                if (deltaX > 5 || deltaY > 5)
                {
                    // Start drag operation
                    NodeDragData dragData = new NodeDragData
                    {
                        MethodName = draggingNode.Item.Method.Name,
                        DisplayName = draggingNode.Item.DisplayName
                    };

                    draggingNode.DoDragDrop(dragData, DragDropEffects.Copy);
                    draggingNode = null;
                }
            }
        }
    }

    /// <summary>
    /// A collapsible category panel that contains a flow layout of nodes
    /// </summary>
    public class CategoryPanel : Panel
    {
        private Button headerButton;
        private FlowLayoutPanel flowPanel;
        private bool isCollapsed = false;
        private const int HeaderHeight = 25;

        public event EventHandler CollapsedChanged;

        public CategoryPanel(string categoryName)
        {
            BackColor = Color.White;
            BorderStyle = BorderStyle.FixedSingle;

            // Create header button
            headerButton = new Button
            {
                Text = (isCollapsed ? "▶ " : "▼ ") + categoryName,
                Dock = DockStyle.Top,
                Height = HeaderHeight,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.Black,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };
            headerButton.FlatAppearance.BorderSize = 0;
            headerButton.Click += HeaderButton_Click;

            // Create flow panel for nodes
            flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5),
                BackColor = Color.White
            };

            Controls.Add(flowPanel);
            Controls.Add(headerButton);

            UpdateHeight();
        }

        private void HeaderButton_Click(object sender, EventArgs e)
        {
            isCollapsed = !isCollapsed;
            flowPanel.Visible = !isCollapsed;
            headerButton.Text = (isCollapsed ? "▶ " : "▼ ") + headerButton.Text.Substring(2);
            UpdateHeight();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddNode(MiniNodeControl node)
        {
            flowPanel.Controls.Add(node);
            UpdateHeight();
        }

        private void UpdateHeight()
        {
            if (isCollapsed)
            {
                Height = HeaderHeight;
            }
            else
            {
                // Calculate required height based on flow panel contents
                int maxY = HeaderHeight + 10;
                foreach (Control control in flowPanel.Controls)
                {
                    maxY = Math.Max(maxY, control.Bottom + flowPanel.Padding.Bottom);
                }
                Height = maxY + 5;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateHeight();
        }
    }

    /// <summary>
    /// A mini node control that renders a small version of a node
    /// </summary>
    public class MiniNodeControl : Control
    {
        public NodeToolboxItem Item { get; private set; }
        private INodesContext context;
        private float scale = 0.5f; // Scale factor for mini nodes

        public MiniNodeControl(NodeToolboxItem item, INodesContext context)
        {
            Item = item;
            this.context = context;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            // Calculate size based on actual node dimensions
            SizeF nodeSize = CalculateNodeSize();
            Size = new Size((int)(nodeSize.Width * scale), (int)(nodeSize.Height * scale));

            Cursor = Cursors.Hand;
        }

        private SizeF CalculateNodeSize()
        {
            // Get parameters to determine socket count
            ParameterInfo[] parameters = Item.Method.GetParameters();
            ParameterInfo[] inputs = parameters.Where(p => !p.IsOut).ToArray();
            ParameterInfo[] outputs = parameters.Where(p => p.IsOut).ToArray();

            // Calculate text widths
            float maxInputWidth = 0;
            float maxOutputWidth = 0;

            foreach (var input in inputs)
            {
                string displayName = ToTitleCase(input.Name);
                SizeF textSize = TextRenderer.MeasureText(displayName, SystemFonts.SmallCaptionFont);
                maxInputWidth = Math.Max(maxInputWidth, textSize.Width);
            }

            foreach (var output in outputs)
            {
                string displayName = ToTitleCase(output.Name);
                SizeF textSize = TextRenderer.MeasureText(displayName, SystemFonts.SmallCaptionFont);
                maxOutputWidth = Math.Max(maxOutputWidth, textSize.Width);
            }

            // Add execution sockets if callable
            if (Item.Attribute.IsCallable)
            {
                if (!Item.Attribute.IsExecutionInitiator)
                {
                    SizeF textSize = TextRenderer.MeasureText("Enter", SystemFonts.SmallCaptionFont);
                    maxInputWidth = Math.Max(maxInputWidth, textSize.Width);
                }
                SizeF exitSize = TextRenderer.MeasureText("Exit", SystemFonts.SmallCaptionFont);
                maxOutputWidth = Math.Max(maxOutputWidth, exitSize.Width);
            }

            // Calculate node name width
            SizeF nameSize = TextRenderer.MeasureText(Item.DisplayName, SystemFonts.DefaultFont);
            float nameWidth = nameSize.Width + 10;

            // Constants from NodeVisual
            const float minWidth = 150;
            const float socketHeight = 16;
            const float socketPadding = 2;
            const float padding = 20;
            const float edgePadding = 10;
            const float headerHeight = 18;
            const float componentPadding = 1;

            // Calculate width
            float socketBasedWidth = socketHeight + socketPadding + maxInputWidth + padding +
                                     maxOutputWidth + socketPadding + socketHeight + edgePadding * 2;
            float width = Math.Max(Math.Max(minWidth, nameWidth), socketBasedWidth);

            // Calculate height
            int inputCount = inputs.Length;
            int outputCount = outputs.Length;
            if (Item.Attribute.IsCallable)
            {
                inputCount++;
                outputCount++;
            }
            float height = headerHeight + Math.Max(inputCount * (socketHeight + componentPadding),
                                                   outputCount * (socketHeight + componentPadding)) + componentPadding * 2f;

            // Use custom dimensions if specified
            if (Item.Attribute.Width > 0)
                width = Item.Attribute.Width;
            if (Item.Attribute.Height > 0)
                height = Item.Attribute.Height;

            return new SizeF(width, height);
        }

        private string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Split on capital letters and underscores
            string result = System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1").Trim();
            result = result.Replace("_", " ");

            // Convert to title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            // Apply scaling
            g.ScaleTransform(scale, scale);

            // Draw the node using similar logic to NodeVisual
            RectangleF nodeRect = new RectangleF(0, 0, Width / scale, Height / scale);
            RectangleF headerRect = new RectangleF(0, 0, nodeRect.Width, 18);

            // Node background
            using (Brush nodeBrush = new SolidBrush(Color.LightCyan))
            {
                g.FillRectangle(nodeBrush, nodeRect);
            }

            // Header
            using (Brush headerBrush = new SolidBrush(Color.Aquamarine))
            {
                g.FillRectangle(headerBrush, headerRect);
            }

            // Borders
            using (Pen borderPen = new Pen(Color.Black, 1))
            {
                g.DrawRectangle(borderPen, Rectangle.Round(nodeRect));
                g.DrawRectangle(borderPen, Rectangle.Round(headerRect));
            }

            // Node name
            using (Font smallFont = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size * 0.9f))
            {
                g.DrawString(Item.DisplayName, smallFont, Brushes.Black, new PointF(3, 2));
            }

            // Draw sockets
            DrawSockets(g);

            // Reset transform
            g.ResetTransform();

            // Draw hover effect
            if (ClientRectangle.Contains(PointToClient(MousePosition)))
            {
                using (Pen hoverPen = new Pen(Color.FromArgb(100, Color.Blue), 2))
                {
                    g.DrawRectangle(hoverPen, new Rectangle(0, 0, Width - 1, Height - 1));
                }
            }
        }

        private void DrawSockets(Graphics g)
        {
            ParameterInfo[] parameters = Item.Method.GetParameters();
            ParameterInfo[] inputs = parameters.Where(p => !p.IsOut).ToArray();
            ParameterInfo[] outputs = parameters.Where(p => p.IsOut).ToArray();

            const float socketSize = 8;
            const float headerHeight = 18;
            const float socketSpacing = 17;
            float startY = headerHeight + 5;

            Font socketFont = new Font(SystemFonts.SmallCaptionFont.FontFamily,
                                      SystemFonts.SmallCaptionFont.Size * 0.85f);

            // Draw execution sockets if callable
            int inputOffset = 0;
            int outputOffset = 0;

            if (Item.Attribute.IsCallable)
            {
                // Input execution socket
                if (!Item.Attribute.IsExecutionInitiator)
                {
                    RectangleF execInRect = new RectangleF(1, startY, socketSize, socketSize);
                    g.FillRectangle(Brushes.White, execInRect);
                    g.DrawRectangle(Pens.Black, Rectangle.Round(execInRect));
                    g.DrawString("Enter", socketFont, Brushes.Black, new PointF(socketSize + 3, startY - 2));
                    inputOffset = 1;
                }

                // Output execution socket
                RectangleF execOutRect = new RectangleF(Width / scale - socketSize - 1, startY, socketSize, socketSize);
                g.FillRectangle(Brushes.White, execOutRect);
                g.DrawRectangle(Pens.Black, Rectangle.Round(execOutRect));

                SizeF exitSize = g.MeasureString("Exit", socketFont);
                g.DrawString("Exit", socketFont, Brushes.Black,
                           new PointF(Width / scale - socketSize - 3 - exitSize.Width, startY - 2));
                outputOffset = 1;
            }

            // Draw input sockets
            for (int i = 0; i < inputs.Length; i++)
            {
                float y = startY + (i + inputOffset) * socketSpacing;
                Color socketColor = GetSocketColor(inputs[i].ParameterType);

                using (Brush socketBrush = new SolidBrush(socketColor))
                {
                    g.FillEllipse(socketBrush, 1, y, socketSize, socketSize);
                }
                g.DrawEllipse(Pens.Black, 1, y, socketSize, socketSize);

                string displayName = ToTitleCase(inputs[i].Name);
                g.DrawString(displayName, socketFont, Brushes.Black, new PointF(socketSize + 3, y - 2));
            }

            // Draw output sockets
            for (int i = 0; i < outputs.Length; i++)
            {
                float y = startY + (i + outputOffset) * socketSpacing;
                Type outputType = outputs[i].ParameterType.GetElementType() ?? outputs[i].ParameterType;
                Color socketColor = GetSocketColor(outputType);

                using (Brush socketBrush = new SolidBrush(socketColor))
                {
                    g.FillEllipse(socketBrush, Width / scale - socketSize - 1, y, socketSize, socketSize);
                }
                g.DrawEllipse(Pens.Black, Width / scale - socketSize - 1, y, socketSize, socketSize);

                string displayName = ToTitleCase(outputs[i].Name);
                SizeF textSize = g.MeasureString(displayName, socketFont);
                g.DrawString(displayName, socketFont, Brushes.Black,
                           new PointF(Width / scale - socketSize - 3 - textSize.Width, y - 2));
            }

            socketFont.Dispose();
        }

        private Color GetSocketColor(Type type)
        {
            // Match the color scheme used in SocketVisual
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return Color.LightGreen;
            else if (type == typeof(string))
                return Color.Yellow;
            else if (type == typeof(bool))
                return Color.Red;
            else
                return Color.LightBlue;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }
    }

    /// <summary>
    /// Represents a draggable node item in the toolbox
    /// </summary>
    public class NodeToolboxItem
    {
        public MethodInfo Method { get; set; }
        public NodeAttribute Attribute { get; set; }
        public string DisplayName { get; set; }
    }
}