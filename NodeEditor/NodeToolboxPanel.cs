using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NodeEditor
{
    /// <summary>
    /// A toolbox panel that displays available node types with collapsible categories and drag-and-drop support
    /// </summary>
    public class NodeToolboxPanel : UserControl
    {
        private Dictionary<string, CategoryPanel> categoryPanels = new Dictionary<string, CategoryPanel>();
        private Panel mainPanel;
        private Timer resizeTimer;

        public INodesContext Context { get; set; }

        // Events for drag preview communication
        public event Action<NodeToolboxItem> OnDragPreviewStart;
        public event Action OnDragPreviewEnd;

        public NodeToolboxPanel()
        {
            SetupMainPanel();
            SetupResizeTimer();
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

        private void SetupResizeTimer()
        {
            resizeTimer = new Timer();
            resizeTimer.Interval = 100;
            resizeTimer.Tick += (s, e) =>
            {
                resizeTimer.Stop();

                // Update all category panel widths first
                foreach (var panel in categoryPanels.Values)
                {
                    panel.Width = mainPanel.ClientSize.Width;
                    panel.UpdateLayout();
                }

                // Then recalculate the overall layout
                RecalculateLayout();
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (resizeTimer != null)
            {
                resizeTimer.Stop();
                resizeTimer.Start();
            }
        }

        /// <summary>
        /// Refreshes the toolbox with available nodes from the context
        /// </summary>
        public void RefreshNodes()
        {
            mainPanel.Controls.Clear();
            categoryPanels.Clear();

            if (Context == null) return;

            // Get all nodes and group by menu path
            MethodInfo[] methods = Context.GetType().GetMethods();
            var nodesByMenu = methods
                .Where(m => m.GetCustomAttributes(typeof(NodeAttribute), false).Length > 0)
                .Select(m => new
                {
                    Method = m,
                    Attribute = m.GetCustomAttributes(typeof(NodeAttribute), false)
                        .Cast<NodeAttribute>()
                        .FirstOrDefault()
                })
                .Where(x => x.Attribute != null)
                .GroupBy(x => string.IsNullOrEmpty(x.Attribute.Menu) ? "General" : x.Attribute.Menu)
                .OrderBy(g => g.Key);

            int yPosition = 0;

            foreach (var categoryGroup in nodesByMenu)
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
            }

            // Defer layout calculation to ensure controls are properly sized
            Timer layoutTimer = new Timer();
            layoutTimer.Interval = 10;
            layoutTimer.Tick += (s, e) =>
            {
                layoutTimer.Stop();
                layoutTimer.Dispose();
                RecalculateLayout();

                // Force update of all category panels
                foreach (var panel in categoryPanels.Values)
                {
                    panel.UpdateLayout();
                }
            };
            layoutTimer.Start();
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
                    // Notify any connected NodesControl about drag start
                    OnDragPreviewStart?.Invoke(draggingNode.Item);

                    // Start drag operation
                    NodeDragData dragData = new NodeDragData
                    {
                        MethodName = draggingNode.Item.Method.Name,
                        DisplayName = draggingNode.Item.DisplayName
                    };

                    try
                    {
                        draggingNode.DoDragDrop(dragData, DragDropEffects.Copy);
                    }
                    finally
                    {
                        // Notify drag end
                        OnDragPreviewEnd?.Invoke();
                        draggingNode = null;
                    }
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
        private bool isCollapsed = true; // Start collapsed by default
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
                Location = new Point(0, HeaderHeight),
                Width = Width,
                AutoScroll = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5),
                BackColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Visible = !isCollapsed // Start hidden if collapsed
            };

            Controls.Add(flowPanel);
            Controls.Add(headerButton);

            // Set initial height based on collapsed state
            Height = isCollapsed ? HeaderHeight : HeaderHeight + 10;
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

        public void UpdateLayout()
        {
            if (flowPanel == null) return;

            // Update flowPanel width to match container
            int newWidth = Math.Max(Width - 2, 50); // Minimum width
            flowPanel.Width = newWidth;
            flowPanel.MaximumSize = new Size(newWidth, 0);

            // Force a layout pass before calculating height
            flowPanel.SuspendLayout();
            flowPanel.PerformLayout();
            flowPanel.ResumeLayout(true);

            // Small delay to ensure all controls are properly positioned
            Application.DoEvents();

            UpdateHeight();

            // Fire CollapsedChanged to trigger parent layout update
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateHeight()
        {
            if (isCollapsed)
            {
                Height = HeaderHeight;
            }
            else
            {
                // Force layout update first
                flowPanel.PerformLayout();

                if (flowPanel.Controls.Count > 0)
                {
                    // Calculate height based on actual control positions
                    int maxBottom = HeaderHeight + 10; // Start with header + padding

                    foreach (Control control in flowPanel.Controls)
                    {
                        // Account for control's position relative to flowPanel + flowPanel's position
                        int controlBottom = flowPanel.Top + control.Bottom + flowPanel.Padding.Bottom;
                        maxBottom = Math.Max(maxBottom, controlBottom);
                    }

                    Height = maxBottom + 5; // Add extra padding at bottom
                }
                else
                {
                    Height = HeaderHeight + 10; // Minimal height when empty
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Update flow panel width to match new container width
            if (flowPanel != null)
            {
                flowPanel.Width = Width - 2;
                flowPanel.MaximumSize = new Size(Width - 2, 0);

                // Use a small delay to ensure resize is complete
                Timer updateTimer = new Timer();
                updateTimer.Interval = 10;
                updateTimer.Tick += (s, args) =>
                {
                    updateTimer.Stop();
                    updateTimer.Dispose();
                    UpdateLayout();
                };
                updateTimer.Start();
            }
        }
    }

    /// <summary>
    /// A mini node control that renders a small version of a node
    /// </summary>
    public class MiniNodeControl : Control
    {
        public NodeToolboxItem Item { get; private set; }
        private INodesContext context;
        private float scale = 0.7f; // Increased from 0.5f for better visibility

        public MiniNodeControl(NodeToolboxItem item, INodesContext context)
        {
            Item = item;
            this.context = context;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            // Calculate size based on actual node dimensions
            SizeF nodeSize = CalculateNodeSize();
            // Add 2 pixels padding to ensure borders aren't cut off
            Size = new Size((int)(nodeSize.Width * scale) + 2, (int)(nodeSize.Height * scale) + 2);

            Cursor = Cursors.Hand;
        }

        public SizeF CalculateNodeSize()
        {
            // Create a temporary NodeVisual to get accurate sizing
            NodeVisual tempNode = new NodeVisual();
            tempNode.Type = Item.Method;
            tempNode.Name = Item.DisplayName;
            tempNode.Callable = Item.Attribute.IsCallable;
            tempNode.ExecInit = Item.Attribute.IsExecutionInitiator;
            tempNode.CustomWidth = Item.Attribute.Width > 0 ? Item.Attribute.Width : -1;
            tempNode.CustomHeight = Item.Attribute.Height > 0 ? Item.Attribute.Height : -1;

            return tempNode.GetNodeBounds();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            // Save the original transform
            var originalTransform = g.Transform;

            // Offset by 1 pixel to account for border padding
            g.TranslateTransform(1, 1);

            // Apply scaling
            g.ScaleTransform(scale, scale);

            // Create a temporary NodeVisual for rendering
            NodeVisual tempNode = new NodeVisual();
            tempNode.Type = Item.Method;
            tempNode.Name = Item.DisplayName;
            tempNode.Callable = Item.Attribute.IsCallable;
            tempNode.ExecInit = Item.Attribute.IsExecutionInitiator;
            tempNode.CustomWidth = Item.Attribute.Width > 0 ? Item.Attribute.Width : -1;
            tempNode.CustomHeight = Item.Attribute.Height > 0 ? Item.Attribute.Height : -1;
            tempNode.X = 0;
            tempNode.Y = 0;

            // Draw the node using NodeVisual's Draw method
            Point mousePos = new Point(-100, -100); // Off-screen so nothing is highlighted
            tempNode.Draw(g, mousePos, MouseButtons.None);

            // Reset transform
            g.Transform = originalTransform;

            // Draw hover effect border on top
            if (ClientRectangle.Contains(PointToClient(MousePosition)))
            {
                using (Pen hoverPen = new Pen(Color.FromArgb(100, Color.Blue), 2))
                {
                    g.DrawRectangle(hoverPen, new Rectangle(0, 0, Width - 1, Height - 1));
                }
            }
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

}