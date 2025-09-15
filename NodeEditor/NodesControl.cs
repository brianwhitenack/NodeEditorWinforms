/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2021 Mariusz Komorowski (komorra)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES 
 * OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
 * OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using Newtonsoft.Json;

using NodeEditor.Serialization;

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    [ToolboxBitmap(typeof(NodesControl), "nodeed")]
    public partial class NodesControl : UserControl
    {
        internal class NodeToken
        {
            public MethodInfo Method;
            public NodeAttribute Attribute;
        }

        private NodesGraph graph = new NodesGraph();
        private bool needRepaint = true;
        private Timer timer = new Timer();
        private bool mdown;
        private Point lastmpos;
        private SocketVisual dragSocket;
        private NodeVisual dragSocketNode;
        private PointF dragConnectionBegin;
        private PointF dragConnectionEnd;
        private NodeConnection dragConnection;
        private bool isDraggingConnection;
        private Stack<NodeVisual> executionStack = new Stack<NodeVisual>();
        private bool rebuildConnectionDictionary = true;
        private Dictionary<string, NodeConnection> connectionDictionary = new Dictionary<string, NodeConnection>();

        // Drag preview support
        private NodeVisual dragPreviewNode = null;
        private Point dragPreviewLocation;

        /// <summary>
        /// Context of the editor. You should set here an instance that implements INodesContext interface.
        /// In context you should define your nodes (methods decorated by Node attribute).
        /// </summary>
        public INodesContext Context
        {
            get { return context; }
            set
            {
                if (context != null)
                {
                    context.FeedbackInfo -= ContextOnFeedbackInfo;
                }
                context = value;
                if (context != null)
                {
                    context.FeedbackInfo += ContextOnFeedbackInfo;
                }
            }
        }

        /// <summary>
        /// Occurs when user selects a node. In the object will be passed node settings for unplugged inputs/outputs.
        /// </summary>
        public event Action<object> OnNodeContextSelected = delegate { };

        /// <summary>
        /// Occurs when node would to share its description.
        /// </summary>
        public event Action<string> OnNodeHint = delegate { };

        /// <summary>
        /// Indicates which part of control should be actually visible. It is useful when dragging nodes out of autoscroll parent control,
        /// to guarantee that moving node/connection is visible to user.
        /// </summary>
        public event Action<RectangleF> OnShowLocation = delegate { };

        private readonly Dictionary<ToolStripMenuItem, int> allContextItems = new Dictionary<ToolStripMenuItem, int>();

        private Point lastMouseLocation;

        private Point autoScroll;

        private PointF selectionStart;

        private PointF selectionEnd;

        private INodesContext context;

        private bool breakExecution = false;

        // Panning support
        private bool isPanning = false;
        private Point panStartPoint;
        private PointF panOffset = new PointF(0, 0);
        private bool rightMouseMoved = false;

        // Zoom support
        private float zoomLevel = 1.0f;
        private const float MIN_ZOOM = 0.1f;
        private const float MAX_ZOOM = 3.0f;
        private const float ZOOM_STEP = 0.1f;

        /// <summary>
        /// Default constructor
        /// </summary>
        public NodesControl()
        {
            InitializeComponent();
            timer.Interval = 16; // ~60 FPS instead of 33 FPS
            timer.Tick += TimerOnTick;
            timer.Start();
            KeyDown += OnKeyDown;
            MouseWheel += OnMouseWheel;
            SetStyle(ControlStyles.Selectable, true);
            // Enable double buffering for smoother rendering
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Enable drag and drop
            AllowDrop = true;
            DragEnter += NodesControl_DragEnter;
            DragOver += NodesControl_DragOver;
            DragDrop += NodesControl_DragDrop;
            DragLeave += NodesControl_DragLeave;
        }

        private void ContextOnFeedbackInfo(string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution)
        {
            this.breakExecution = breakExecution;
            if (breakExecution)
            {
                nodeVisual.Feedback = type;
                OnNodeHint(message);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 7)
            {
                return;
            }
            base.WndProc(ref m);
        }

        private void OnKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.KeyCode == Keys.Delete)
            {
                DeleteSelectedNodes();
            }
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            // Store the old zoom level
            float oldZoom = zoomLevel;

            // Calculate new zoom level
            float zoomDelta = e.Delta > 0 ? ZOOM_STEP : -ZOOM_STEP;
            float newZoom = oldZoom + zoomDelta;
            zoomLevel = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, newZoom));

            // Calculate zoom ratio
            float zoomRatio = zoomLevel / oldZoom;

            // Adjust pan offset to keep mouse position fixed in world space
            // The point under the mouse should remain at the same screen position
            panOffset.X = e.Location.X - (e.Location.X - panOffset.X) * zoomRatio;
            panOffset.Y = e.Location.Y - (e.Location.Y - panOffset.Y) * zoomRatio;

            needRepaint = true;
        }

        /// <summary>
        /// Convert screen coordinates to world coordinates (considering zoom and pan)
        /// </summary>
        private PointF ScreenToWorld(Point screenPoint)
        {
            return new PointF(
                (screenPoint.X - panOffset.X) / zoomLevel,
                (screenPoint.Y - panOffset.Y) / zoomLevel
            );
        }

        /// <summary>
        /// Convert world coordinates to screen coordinates (considering zoom and pan)
        /// </summary>
        private PointF WorldToScreen(PointF worldPoint)
        {
            return new PointF(
                worldPoint.X * zoomLevel + panOffset.X,
                worldPoint.Y * zoomLevel + panOffset.Y
            );
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (DesignMode) return;
            if (needRepaint)
            {
                Invalidate();
            }
        }

        private void DrawGrid(Graphics g)
        {
            // Grid settings
            int baseGridSize = 20;
            float gridSize = baseGridSize * zoomLevel; // Scale grid with zoom

            // Skip grid drawing if too small or too large to be useful
            if (gridSize < 2 || gridSize > 200)
                return;

            Color gridColor = Color.FromArgb(60, 0, 0, 0); // Light gray grid
            Color majorGridColor = Color.FromArgb(100, 0, 0, 0); // Darker for major lines

            using (Pen gridPen = new Pen(gridColor, 1))
            using (Pen majorGridPen = new Pen(majorGridColor, 1))
            {
                // Calculate grid offset based on pan and zoom
                float offsetX = panOffset.X % gridSize;
                float offsetY = panOffset.Y % gridSize;

                // Draw vertical lines
                for (float x = offsetX; x < Width; x += gridSize)
                {
                    // Every 5th line is a major grid line
                    int gridIndex = (int)Math.Round((x - panOffset.X) / gridSize);
                    bool isMajor = gridIndex % 5 == 0;
                    g.DrawLine(isMajor ? majorGridPen : gridPen, x, 0, x, Height);
                }

                // Draw horizontal lines
                for (float y = offsetY; y < Height; y += gridSize)
                {
                    // Every 5th line is a major grid line
                    int gridIndex = (int)Math.Round((y - panOffset.Y) / gridSize);
                    bool isMajor = gridIndex % 5 == 0;
                    g.DrawLine(isMajor ? majorGridPen : gridPen, 0, y, Width, y);
                }
            }
        }

        private void NodesControl_Paint(object sender, PaintEventArgs e)
        {
            // Set graphics quality once
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;

            // Draw the background grid first (not transformed)
            DrawGrid(e.Graphics);

            // Save the original transform
            Matrix originalTransform = e.Graphics.Transform;

            // Apply zoom and pan transformation
            e.Graphics.TranslateTransform(panOffset.X, panOffset.Y);
            e.Graphics.ScaleTransform(zoomLevel, zoomLevel);

            // Transform mouse position for drawing
            PointF transformedMouse = ScreenToWorld(PointToClient(MousePosition));

            graph.Draw(e.Graphics, Point.Round(transformedMouse), MouseButtons);

            if (dragSocket != null)
            {
                using (Pen pen = new Pen(Color.Black, 2 / zoomLevel)) // Adjust pen width for zoom
                {
                    // dragConnectionBegin and dragConnectionEnd are already in world space
                    NodesGraph.DrawConnection(e.Graphics, pen, dragConnectionBegin, dragConnectionEnd);
                }
            }

            // Draw drag preview if active
            if (dragPreviewNode != null)
            {
                // Draw the preview node with a slight visual difference (e.g., dashed border)
                dragPreviewNode.Draw(e.Graphics, Point.Round(transformedMouse), MouseButtons.None);

                // Draw a dashed outline to indicate it's a preview
                SizeF nodeSize = dragPreviewNode.GetNodeBounds();
                using (Pen dashPen = new Pen(Color.DodgerBlue, 2 / zoomLevel))
                {
                    dashPen.DashStyle = DashStyle.Dash;
                    RectangleF previewRect = new RectangleF(
                        dragPreviewNode.X - 2,
                        dragPreviewNode.Y - 2,
                        nodeSize.Width + 4,
                        nodeSize.Height + 4
                    );
                    e.Graphics.DrawRectangle(dashPen, Rectangle.Round(previewRect));
                }
            }

            // Restore transform for UI elements
            e.Graphics.Transform = originalTransform;

            if (selectionStart != PointF.Empty)
            {
                Rectangle rect = Rectangle.Round(MakeRect(selectionStart, selectionEnd));
                using (var fillBrush = new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)))
                using (var borderPen = new Pen(Color.DodgerBlue))
                {
                    e.Graphics.FillRectangle(fillBrush, rect);
                    e.Graphics.DrawRectangle(borderPen, rect);
                }
            }

            needRepaint = false;
        }

        private static RectangleF MakeRect(PointF a, PointF b)
        {
            float x1 = a.X;
            float x2 = b.X;
            float y1 = a.Y;
            float y2 = b.Y;
            return new RectangleF(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        private void NodesControl_MouseMove(object sender, MouseEventArgs e)
        {
            Point em = PointToScreen(e.Location);

            // Handle right-click panning
            if (isPanning && e.Button == MouseButtons.Right)
            {
                rightMouseMoved = true;
                float dx = e.Location.X - panStartPoint.X;
                float dy = e.Location.Y - panStartPoint.Y;

                panOffset.X += dx;
                panOffset.Y += dy;

                panStartPoint = e.Location;
                needRepaint = true;
                return;
            }

            if (selectionStart != PointF.Empty)
            {
                selectionEnd = e.Location;
            }
            if (mdown)
            {
                if (!isDraggingConnection && dragSocket == null)
                {
                    // Regular node dragging - only when not dragging sockets or connections
                    // Scale the movement by zoom level
                    float dx = (em.X - lastmpos.X) / zoomLevel;
                    float dy = (em.Y - lastmpos.Y) / zoomLevel;

                    // Cache selected nodes to avoid multiple LINQ queries
                    List<NodeVisual> selectedNodes = graph.Nodes.Where(x => x.IsSelected).ToList();

                    if (selectedNodes.Count > 0)
                    {
                        // Move all selected nodes
                        foreach (NodeVisual node in selectedNodes)
                        {
                            node.X += dx;
                            node.Y += dy;
                            // Efficiently update socket positions without full recalculation
                            node.UpdateSocketPositions(dx, dy);
                            // Only layout custom editors if present
                            if (node.CustomEditor != null)
                            {
                                node.LayoutEditor();
                            }
                        }

                        // Calculate bounds only if needed for auto-scroll
                        // This is expensive so we can skip it for most drag operations
                        // Only calculate every few pixels of movement
                        if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                        {
                            NodeVisual firstNode = selectedNodes[0];
                            RectangleF bound = new RectangleF(new PointF(firstNode.X, firstNode.Y), firstNode.GetNodeBounds());
                            for (int i = 1; i < selectedNodes.Count; i++)
                            {
                                NodeVisual node = selectedNodes[i];
                                bound = RectangleF.Union(bound, new RectangleF(new PointF(node.X, node.Y), node.GetNodeBounds()));
                            }
                            OnShowLocation(bound);
                        }
                    }
                }

                if (dragSocket != null)
                {
                    if (isDraggingConnection)
                    {
                        // Connection dragging: floating end follows mouse, fixed end stays at socket
                        // Keep positions in world space
                        PointF mousePos = ScreenToWorld(PointToClient(em));

                        // Get the fixed socket center (the one we're NOT dragging from) in world space
                        PointF fixedSocketCenter = new PointF(dragSocket.X + dragSocket.Width / 2f, dragSocket.Y + dragSocket.Height / 2f);

                        if (dragSocket.Input)
                        {
                            // Dragging towards a new input, keep current input socket fixed
                            dragConnectionBegin = mousePos; // Floating end follows mouse (world space)
                            dragConnectionEnd = fixedSocketCenter; // Fixed end stays at input socket (world space)
                        }
                        else
                        {
                            // Dragging towards a new output, keep current output socket fixed
                            dragConnectionBegin = fixedSocketCenter; // Fixed end stays at output socket (world space)
                            dragConnectionEnd = mousePos; // Floating end follows mouse (world space)
                        }
                    }
                    else
                    {
                        // Regular socket dragging: positions in world space
                        PointF center = new PointF(dragSocket.X + dragSocket.Width / 2f, dragSocket.Y + dragSocket.Height / 2f);
                        PointF mouseWorldPos = ScreenToWorld(PointToClient(em));

                        if (dragSocket.Input)
                        {
                            dragConnectionBegin = mouseWorldPos;
                            dragConnectionEnd = center;
                        }
                        else
                        {
                            dragConnectionBegin = center;
                            dragConnectionEnd = mouseWorldPos;
                        }
                    }
                }
                lastmpos = em;

                // For immediate responsiveness during dragging, invalidate directly
                // instead of waiting for timer
                if (mdown || isDraggingConnection || isPanning)
                {
                    Invalidate();
                }
                else
                {
                    needRepaint = true;
                }
            }
            else
            {
                needRepaint = true;
            }
        }

        /// <summary>
        /// Handles mouse down on connections for dragging/reconnecting
        /// </summary>
        /// <param name="location">Mouse location</param>
        /// <returns>True if connection dragging was initiated</returns>
        private bool TryStartConnectionDrag(Point location)
        {
            // Convert screen coordinates to world coordinates
            PointF worldLocation = ScreenToWorld(location);

            NodeConnection hitConnection = graph.GetConnectionAtPoint(worldLocation);
            if (hitConnection == null || mdown) return false;

            // Start dragging the connection
            isDraggingConnection = true;
            dragConnection = hitConnection;

            // Remove the connection temporarily while dragging
            graph.Connections.Remove(hitConnection);
            rebuildConnectionDictionary = true;
            Invalidate(); // Force immediate repaint to hide the original connection

            // Determine which end to drag based on mouse position
            SocketVisual outputSocket = hitConnection.OutputNode.GetSockets().FirstOrDefault(x => x.Name == hitConnection.OutputSocketName);
            SocketVisual inputSocket = hitConnection.InputNode.GetSockets().FirstOrDefault(x => x.Name == hitConnection.InputSocketName);

            if (outputSocket != null && inputSocket != null)
            {
                RectangleF outputBounds = outputSocket.GetBounds();
                RectangleF inputBounds = inputSocket.GetBounds();
                PointF outputCenter = outputBounds.Location + new SizeF(outputBounds.Width / 2f, outputBounds.Height / 2f);
                PointF inputCenter = inputBounds.Location + new SizeF(inputBounds.Width / 2f, inputBounds.Height / 2f);

                // Calculate distance to both ends
                float distToOutput = (float)Math.Sqrt(Math.Pow(location.X - outputCenter.X, 2) + Math.Pow(location.Y - outputCenter.Y, 2));
                float distToInput = (float)Math.Sqrt(Math.Pow(location.X - inputCenter.X, 2) + Math.Pow(location.Y - inputCenter.Y, 2));

                // Drag from the closer end - disconnect that end and keep the farther end fixed
                if (distToOutput < distToInput)
                {
                    // Closer to output - disconnect from output, keep input fixed, drag towards new output
                    dragSocket = inputSocket;
                    dragSocketNode = hitConnection.InputNode;
                    dragConnectionBegin = worldLocation; // Floating end starts at mouse (world space)
                    dragConnectionEnd = inputCenter; // Fixed end stays at input socket
                }
                else
                {
                    // Closer to input - disconnect from input, keep output fixed, drag towards new input
                    dragSocket = outputSocket;
                    dragSocketNode = hitConnection.OutputNode;
                    dragConnectionBegin = outputCenter; // Fixed end stays at output socket
                    dragConnectionEnd = worldLocation; // Floating end starts at mouse (world space)
                }
            }

            mdown = true;
            lastmpos = PointToScreen(location);

            // Handle type propagation after disconnection - use unified propagation
            PropagateTypesForConnection(hitConnection);

            return true;
        }

        /// <summary>
        /// Handles mouse down on node headers for selection and dragging
        /// </summary>
        /// <param name="location">Mouse location</param>
        /// <returns>The selected node if header was clicked, null otherwise</returns>
        private NodeVisual TrySelectNodeHeader(Point location)
        {
            // Convert screen coordinates to world coordinates
            PointF worldLocation = ScreenToWorld(location);

            NodeVisual node = graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                x => new RectangleF(new PointF(x.X, x.Y), x.GetHeaderSize()).Contains(worldLocation));

            if (node != null && !mdown)
            {
                // If the node wasn't already selected, select it
                // If it was already selected, keep it selected (for multi-drag)
                if (!node.IsSelected)
                {
                    node.IsSelected = true;
                }

                node.Order = graph.Nodes.Min(x => x.Order) - 1;
                if (node.CustomEditor != null)
                {
                    node.CustomEditor.BringToFront();
                }
                mdown = true;
                lastmpos = PointToScreen(location);
                Refresh();
            }

            return node;
        }

        /// <summary>
        /// Handles mouse down on sockets for connection creation/modification
        /// </summary>
        /// <param name="location">Mouse location</param>
        /// <param name="targetNode">The node that was already selected (if any)</param>
        /// <returns>The node if socket interaction occurred, null otherwise</returns>
        private NodeVisual TryHandleSocketInteraction(Point location, NodeVisual targetNode)
        {
            if (targetNode != null || mdown) return targetNode;

            // Convert screen coordinates to world coordinates
            PointF worldLocation = ScreenToWorld(location);

            NodeVisual nodeWhole = graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(worldLocation));

            if (nodeWhole == null) return null;

            targetNode = nodeWhole;
            SocketVisual socket = nodeWhole.GetSockets().FirstOrDefault(x => x.GetBounds().Contains(worldLocation));

            if (socket == null) return targetNode;

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                // Ctrl+Click: Disconnect existing connection and start dragging from the other end
                NodeConnection connection = graph.Connections.FirstOrDefault(
                    x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                if (connection != null)
                {
                    dragSocket = connection.OutputNode.GetSockets()
                        .FirstOrDefault(x => x.Name == connection.OutputSocketName);
                    dragSocketNode = connection.OutputNode;
                }
                else
                {
                    connection = graph.Connections.FirstOrDefault(
                        x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                    if (connection != null)
                    {
                        dragSocket = connection.InputNode.GetSockets()
                            .FirstOrDefault(x => x.Name == connection.InputSocketName);
                        dragSocketNode = connection.InputNode;
                    }
                }

                graph.Connections.Remove(connection);
                rebuildConnectionDictionary = true;

                // Handle type propagation after disconnection - use unified propagation
                if (connection != null)
                {
                    PropagateTypesForConnection(connection);
                }
            }
            else
            {
                // Normal click: Start dragging from this socket
                dragSocket = socket;
                dragSocketNode = nodeWhole;
            }

            // Convert to world coordinates for connection dragging
            PointF worldLoc = ScreenToWorld(location);
            dragConnectionBegin = worldLoc;
            dragConnectionEnd = worldLoc;
            mdown = true;
            lastmpos = PointToScreen(location);

            return targetNode;
        }

        private void NodesControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                selectionStart = PointF.Empty;
                Focus();

                // Check if clicking on an already selected node first
                PointF worldLocation = ScreenToWorld(e.Location);
                NodeVisual clickedNode = graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                    x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(worldLocation));

                bool clickedOnSelectedNode = clickedNode != null && clickedNode.IsSelected;

                // Only clear selection if not shift-clicking and not clicking on already selected node
                if ((ModifierKeys & Keys.Shift) != Keys.Shift && !clickedOnSelectedNode)
                {
                    graph.Nodes.ForEach(x => x.IsSelected = false);
                }

                // Try each type of interaction in order of priority - sockets first, then connections
                NodeVisual selectedNode = TrySelectNodeHeader(e.Location);

                if (selectedNode == null)
                {
                    selectedNode = TryHandleSocketInteraction(e.Location, selectedNode);
                }

                // Only try connection dragging if no socket interaction occurred
                if (selectedNode == null && !mdown && TryStartConnectionDrag(e.Location))
                {
                    return; // Connection dragging started
                }

                if (selectedNode == null && !mdown)
                {
                    // Start selection rectangle
                    selectionStart = selectionEnd = e.Location;
                }

                if (selectedNode != null)
                {
                    OnNodeContextSelected(selectedNode.GetNodeContext());
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Start panning mode
                isPanning = true;
                panStartPoint = e.Location;
                rightMouseMoved = false;
                Cursor = Cursors.SizeAll;
            }

            needRepaint = true;
        }

        private bool IsConnectable(SocketVisual a, SocketVisual b)
        {
            SocketVisual input = a.Input ? a : b;
            SocketVisual output = a.Input ? b : a;

            // Use runtime types if available, otherwise fall back to static types
            Type outputType = output.RuntimeType ?? output.Type;
            Type inputType = input.RuntimeType ?? input.Type;

            outputType = Type.GetType(outputType.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);
            inputType = Type.GetType(inputType.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);

            if (outputType == null || inputType == null) return false;

            // Use TypePropagation's more sophisticated type compatibility check
            // which handles generic collections properly
            return TypePropagation.AreTypesCompatible(outputType, inputType);
        }

        private Type TypeResolver(Assembly assembly, string name, bool inh)
        {
            if (assembly == null) assembly = ResolveAssembly(name);
            if (assembly == null) return null;
            return assembly.GetType(name);
        }

        private Assembly ResolveAssembly(string fullTypeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetTypes().Any(o => o.FullName == fullTypeName));
        }

        private Assembly AssemblyResolver(AssemblyName assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == assemblyName.FullName);
        }

        private void NodesControl_MouseUp(object sender, MouseEventArgs e)
        {
            // Handle right-click panning end
            if (e.Button == MouseButtons.Right)
            {
                isPanning = false;
                Cursor = Cursors.Default;
            }

            if (selectionStart != PointF.Empty)
            {
                // Convert selection rectangle to world coordinates
                PointF worldStart = ScreenToWorld(Point.Round(selectionStart));
                PointF worldEnd = ScreenToWorld(Point.Round(selectionEnd));
                RectangleF rect = MakeRect(worldStart, worldEnd);

                graph.Nodes.ForEach(
                    x => x.IsSelected = rect.IntersectsWith(new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds())));
                selectionStart = PointF.Empty;
            }

            // Handle connection dragging
            if (isDraggingConnection && dragSocket != null && dragConnection != null)
            {
                // Convert mouse location to world coordinates
                PointF worldLocation = ScreenToWorld(e.Location);

                NodeVisual nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(worldLocation));

                bool connectionRecreated = false;

                if (nodeWhole != null)
                {
                    SocketVisual socket = nodeWhole.GetSockets().FirstOrDefault(x => x.GetBounds().Contains(worldLocation));
                    if (socket != null && IsConnectable(dragSocket, socket) && dragSocket.Input != socket.Input)
                    {
                        // Recreate the connection with new endpoint
                        NodeConnection nc = new NodeConnection();
                        if (!dragSocket.Input)
                        {
                            nc.OutputNode = dragSocketNode;
                            nc.OutputSocketName = dragSocket.Name;
                            nc.InputNode = nodeWhole;
                            nc.InputSocketName = socket.Name;
                        }
                        else
                        {
                            nc.InputNode = dragSocketNode;
                            nc.InputSocketName = dragSocket.Name;
                            nc.OutputNode = nodeWhole;
                            nc.OutputSocketName = socket.Name;
                        }

                        graph.Connections.RemoveAll(
                            x => x.InputNode == nc.InputNode && x.InputSocketName == nc.InputSocketName);

                        graph.Connections.Add(nc);
                        rebuildConnectionDictionary = true;
                        connectionRecreated = true;

                        // Propagate types for dynamic nodes
                        PropagateTypesForConnection(nc);
                    }
                }

                // If connection wasn't recreated, it's deleted (user dropped it in empty space)
                if (!connectionRecreated)
                {
                    // Connection is already removed from graph, just need to handle type propagation
                    // Handle type propagation for nodes that were connected to the deleted connection - use unified propagation
                    PropagateTypesForConnection(dragConnection);
                }

                // Reset connection dragging state
                isDraggingConnection = false;
                dragConnection = null;
            }
            else if (dragSocket != null)
            {
                // Convert mouse location to world coordinates
                PointF worldLocation = ScreenToWorld(e.Location);

                NodeVisual nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(worldLocation));
                if (nodeWhole != null)
                {
                    SocketVisual socket = nodeWhole.GetSockets().FirstOrDefault(x => x.GetBounds().Contains(worldLocation));
                    if (socket != null)
                    {
                        if (IsConnectable(dragSocket, socket) && dragSocket.Input != socket.Input)
                        {
                            NodeConnection nc = new NodeConnection();
                            if (!dragSocket.Input)
                            {
                                nc.OutputNode = dragSocketNode;
                                nc.OutputSocketName = dragSocket.Name;
                                nc.InputNode = nodeWhole;
                                nc.InputSocketName = socket.Name;
                            }
                            else
                            {
                                nc.InputNode = dragSocketNode;
                                nc.InputSocketName = dragSocket.Name;
                                nc.OutputNode = nodeWhole;
                                nc.OutputSocketName = socket.Name;
                            }

                            graph.Connections.RemoveAll(
                                x => x.InputNode == nc.InputNode && x.InputSocketName == nc.InputSocketName);

                            graph.Connections.Add(nc);
                            rebuildConnectionDictionary = true;

                            // Propagate types for dynamic nodes
                            PropagateTypesForConnection(nc);
                        }
                    }
                }
            }

            dragSocket = null;
            mdown = false;
            needRepaint = true;
        }

        /// <summary>
        /// Propagates types through the graph when a connection is made or removed
        /// </summary>
        /// <summary>
        /// Unified type propagation for a connection - handles both initial and cascading propagation
        /// This is the single entry point for all type propagation to ensure consistency
        /// </summary>
        private void PropagateTypesForConnection(NodeConnection connection)
        {
            if (connection == null) return;

            // Propagate types for the input node if it supports dynamic typing
            if (connection.InputNode.HasDynamicTypeSupport())
            {
                connection.InputNode.PropagateTypes(graph);

                // Check for incompatible downstream connections
                DynamicNodeAttribute nodeAttr = connection.InputNode.Type?.GetCustomAttribute<DynamicNodeAttribute>();
                if (nodeAttr != null && nodeAttr.AutoDisconnectIncompatible)
                {
                    DisconnectIncompatibleConnections(connection.InputNode);
                }
            }

            // Always propagate downstream to ensure complete type flow
            PropagateTypesDownstream(connection.InputNode);
        }

        /// <summary>
        /// Propagates types for all connections in the graph using topological ordering
        /// This ensures types flow from source nodes to sink nodes, preventing premature incompatibility detection
        /// </summary>
        private void PropagateAllConnectionTypes()
        {
            // Process in topological order to ensure types flow from sources to sinks
            HashSet<NodeVisual> processedNodes = new HashSet<NodeVisual>();
            List<NodeConnection> connectionsToProcess = graph.Connections.ToList();

            // First pass: Process connections from nodes with no inputs (source nodes)
            // This ensures concrete types are established before dynamic nodes
            List<NodeVisual> sourceNodes = graph.Nodes.Where(n =>
                !graph.Connections.Any(c => c.InputNode == n)).ToList();

            foreach (NodeVisual sourceNode in sourceNodes)
            {
                List<NodeConnection> sourceConnections = connectionsToProcess
                    .Where(c => c.OutputNode == sourceNode).ToList();
                foreach (NodeConnection connection in sourceConnections)
                {
                    PropagateTypesForConnection(connection);
                    processedNodes.Add(connection.InputNode);
                }
            }

            // Second pass: Process remaining connections
            // These are connections in the middle of the graph
            foreach (NodeConnection connection in connectionsToProcess)
            {
                if (!processedNodes.Contains(connection.InputNode))
                {
                    PropagateTypesForConnection(connection);
                }
            }
        }

        /// <summary>
        /// Propagates types to all nodes downstream from the given node
        /// </summary>
        private void PropagateTypesDownstream(NodeVisual startNode)
        {
            HashSet<NodeVisual> visited = new HashSet<NodeVisual>();
            Queue<NodeVisual> toProcess = new Queue<NodeVisual>();
            toProcess.Enqueue(startNode);
            bool needsInvalidate = false;

            while (toProcess.Count > 0)
            {
                NodeVisual current = toProcess.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);

                // Find all nodes connected to outputs of current node
                List<NodeConnection> outputConnections = graph.Connections
                    .Where(c => c.OutputNode == current)
                    .ToList();

                foreach (NodeConnection conn in outputConnections)
                {
                    if (conn.InputNode.HasDynamicTypeSupport())
                    {
                        conn.InputNode.PropagateTypes(graph);
                        needsInvalidate = true;

                        // Check for incompatible connections
                        DynamicNodeAttribute nodeAttr = conn.InputNode.Type?.GetCustomAttribute<DynamicNodeAttribute>();
                        if (nodeAttr != null && nodeAttr.AutoDisconnectIncompatible)
                        {
                            DisconnectIncompatibleConnections(conn.InputNode);
                        }
                    }

                    if (!visited.Contains(conn.InputNode))
                    {
                        toProcess.Enqueue(conn.InputNode);
                    }
                }
            }

            // Trigger a visual refresh if any types were updated
            if (needsInvalidate)
            {
                Invalidate();
            }
        }

        /// <summary>
        /// Disconnects connections that are no longer type-compatible
        /// </summary>
        private void DisconnectIncompatibleConnections(NodeVisual node)
        {
            List<NodeConnection> connectionsToRemove = new List<NodeConnection>();

            // Check input connections
            List<NodeConnection> inputConnections = graph.Connections
                .Where(c => c.InputNode == node)
                .ToList();

            foreach (NodeConnection conn in inputConnections)
            {
                Type expectedType = node.GetSocketRuntimeType(conn.InputSocketName);
                Type actualType = conn.OutputNode.GetSocketRuntimeType(conn.OutputSocketName);

                if (expectedType != null && actualType != null)
                {
                    if (!TypePropagation.AreTypesCompatible(actualType, expectedType))
                    {
                        connectionsToRemove.Add(conn);
                    }
                }
            }

            // Check output connections
            List<NodeConnection> outputConnections = graph.Connections
                .Where(c => c.OutputNode == node)
                .ToList();

            foreach (NodeConnection conn in outputConnections)
            {
                Type providedType = node.GetSocketRuntimeType(conn.OutputSocketName);
                Type requiredType = conn.InputNode.GetSocketRuntimeType(conn.InputSocketName);

                if (providedType != null && requiredType != null)
                {
                    // Disconnect if types are incompatible
                    if (!TypePropagation.AreTypesCompatible(providedType, requiredType))
                    {
                        // Special case: if the provided type became generic (object) and the required type is specific,
                        // we should disconnect - this handles the case where dynamic node reverts to object
                        if (providedType == typeof(object) && requiredType != typeof(object))
                        {
                            connectionsToRemove.Add(conn);
                        }
                        // For other incompatibilities, only disconnect if the input socket is dynamic
                        else if (TypePropagation.IsDynamicSocket(conn.InputNode, conn.InputSocketName))
                        {
                            connectionsToRemove.Add(conn);
                        }
                        // Also disconnect if both sockets are from dynamic nodes
                        else if (TypePropagation.IsDynamicSocket(node, conn.OutputSocketName))
                        {
                            connectionsToRemove.Add(conn);
                        }
                    }
                }
            }

            // Remove incompatible connections
            foreach (NodeConnection conn in connectionsToRemove)
            {
                graph.Connections.Remove(conn);
                rebuildConnectionDictionary = true;

                // Reset types for disconnected input node if it's dynamic - use unified propagation
                PropagateTypesForConnection(conn);
            }
        }

        /// <summary>
        /// Disconnects connections that are no longer type-compatible (overload that returns removed connections)
        /// </summary>
        private void DisconnectIncompatibleConnections(NodeVisual node, List<NodeConnection> connectionsRemoved)
        {
            List<NodeConnection> connectionsToRemove = new List<NodeConnection>();

            // Check input connections
            List<NodeConnection> inputConnections = graph.Connections
                .Where(c => c.InputNode == node)
                .ToList();

            foreach (NodeConnection conn in inputConnections)
            {
                Type expectedType = node.GetSocketRuntimeType(conn.InputSocketName);
                Type actualType = conn.OutputNode.GetSocketRuntimeType(conn.OutputSocketName);

                if (expectedType != null && actualType != null)
                {
                    if (!TypePropagation.AreTypesCompatible(actualType, expectedType))
                    {
                        connectionsToRemove.Add(conn);
                    }
                }
            }

            // Check output connections
            List<NodeConnection> outputConnections = graph.Connections
                .Where(c => c.OutputNode == node)
                .ToList();

            foreach (NodeConnection conn in outputConnections)
            {
                Type providedType = node.GetSocketRuntimeType(conn.OutputSocketName);
                Type requiredType = conn.InputNode.GetSocketRuntimeType(conn.InputSocketName);

                if (providedType != null && requiredType != null)
                {
                    // Disconnect if types are incompatible
                    if (!TypePropagation.AreTypesCompatible(providedType, requiredType))
                    {
                        // Special case: if the provided type became generic (object) and the required type is specific,
                        // we should disconnect - this handles the case where dynamic node reverts to object
                        if (providedType == typeof(object) && requiredType != typeof(object))
                        {
                            connectionsToRemove.Add(conn);
                        }
                        // For other incompatibilities, only disconnect if the input socket is dynamic
                        else if (TypePropagation.IsDynamicSocket(conn.InputNode, conn.InputSocketName))
                        {
                            connectionsToRemove.Add(conn);
                        }
                        // Also disconnect if both sockets are from dynamic nodes
                        else if (TypePropagation.IsDynamicSocket(node, conn.OutputSocketName))
                        {
                            connectionsToRemove.Add(conn);
                        }
                    }
                }
            }

            // Remove incompatible connections
            foreach (NodeConnection conn in connectionsToRemove)
            {
                graph.Connections.Remove(conn);
                rebuildConnectionDictionary = true;
                connectionsRemoved.Add(conn);

                // Reset types for disconnected input node if it's dynamic - use unified propagation
                PropagateTypesForConnection(conn);
            }
        }

        private void AddToMenu(ToolStripItemCollection items, NodeToken token, string path, EventHandler click)
        {
            string[] pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string first = pathParts.FirstOrDefault();
            ToolStripMenuItem item = null;
            if (!items.ContainsKey(first))
            {
                item = new ToolStripMenuItem(first);
                item.Name = first;
                item.Tag = token;
                items.Add(item);
            }
            else
            {
                item = items[first] as ToolStripMenuItem;
            }
            string next = string.Join("/", pathParts.Skip(1));
            if (!string.IsNullOrEmpty(next))
            {
                item.MouseEnter += (sender, args) => OnNodeHint("");
                AddToMenu(item.DropDownItems, token, next, click);
            }
            else
            {
                item.Click += click;
                item.Click += (sender, args) =>
                {
                    ToolStripMenuItem i = allContextItems.Keys.FirstOrDefault(x => x.Name == item.Name);
                    allContextItems[i]++;
                };
                item.MouseEnter += (sender, args) => OnNodeHint(token.Attribute.Description ?? "");
                if (!allContextItems.Keys.Any(x => x.Name == item.Name))
                {
                    allContextItems.Add(item, 0);
                }
            }
        }

        private void NodesControl_MouseClick(object sender, MouseEventArgs e)
        {
            lastMouseLocation = e.Location;

            if (Context == null) return;

            // Only show context menu if we didn't drag
            if (e.Button == MouseButtons.Right && !rightMouseMoved)
            {
                MethodInfo[] methods = Context.GetType().GetMethods();
                IEnumerable<NodeToken> nodes =
                    methods.Select(
                        x =>
                            new
                                NodeToken()
                            {
                                Method = x,
                                Attribute =
                                    x.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault()
                            }).Where(x => x.Attribute != null);

                ContextMenuStrip context = new ContextMenuStrip();
                if (graph.Nodes.Exists(x => x.IsSelected))
                {
                    context.Items.Add("Delete Node(s)", null, ((o, args) =>
                    {
                        DeleteSelectedNodes();
                    }));
                    context.Items.Add("Duplicate Node(s)", null, ((o, args) =>
                    {
                        DuplicateSelectedNodes();
                    }));
                    context.Items.Add("Change Color ...", null, ((o, args) =>
                    {
                        ChangeSelectedNodesColor();
                    }));
                    if (graph.Nodes.Count(x => x.IsSelected) == 2)
                    {
                        NodeVisual[] sel = graph.Nodes.Where(x => x.IsSelected).ToArray();
                        context.Items.Add("Check Impact", null, ((o, args) =>
                        {
                            if (HasImpact(sel[0], sel[1]) || HasImpact(sel[1], sel[0]))
                            {
                                MessageBox.Show("One node has impact on other.", "Impact detected.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("These nodes not impacts themselves.", "No impact.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }));
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                if (allContextItems.Values.Any(x => x > 0))
                {
                    IEnumerable<KeyValuePair<ToolStripMenuItem, int>> handy = allContextItems.Where(x => x.Value > 0 && !string.IsNullOrEmpty(((x.Key.Tag) as NodeToken).Attribute.Menu)).OrderByDescending(x => x.Value).Take(8);
                    foreach (var kv in handy)
                    {
                        context.Items.Add(kv.Key);
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                foreach (var node in nodes.OrderBy(x => x.Attribute.Path))
                {
                    AddToMenu(context.Items, node, node.Attribute.Path, (s, ev) =>
                    {
                        AddNodeToGraph(node);
                    });
                }
                context.Show(MousePosition);
            }
        }

        private void AddNodeToGraph(NodeToken node)
        {
            NodeVisual nv = new NodeVisual();
            nv.X = lastMouseLocation.X;
            nv.Y = lastMouseLocation.Y;
            nv.Type = node.Method;
            nv.Callable = node.Attribute.IsCallable;
            nv.Name = node.Attribute.Name;
            nv.Order = graph.Nodes.Count;
            nv.ExecInit = node.Attribute.IsExecutionInitiator;
            nv.XmlExportName = node.Attribute.XmlExportName;
            nv.CustomWidth = node.Attribute.Width;
            nv.CustomHeight = node.Attribute.Height;

            // Create flow control handler if specified
            if (node.Attribute.FlowControlHandler != null)
            {
                nv.FlowControlHandler = Activator.CreateInstance(node.Attribute.FlowControlHandler) as IFlowControlNode;
            }

            if (node.Attribute.CustomEditor != null)
            {
                Control ctrl = null;
                nv.CustomEditor = ctrl = Activator.CreateInstance(node.Attribute.CustomEditor) as Control;
                if (ctrl != null)
                {
                    ctrl.Tag = nv;
                    Controls.Add(ctrl);
                }
                nv.LayoutEditor();
            }

            graph.Nodes.Add(nv);
            Refresh();
            needRepaint = true;
        }

        private void ChangeSelectedNodesColor()
        {
            ColorDialog cd = new ColorDialog();
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                foreach (var n in graph.Nodes.Where(x => x.IsSelected))
                {
                    n.NodeColor = cd.Color;
                }
            }
            Refresh();
            needRepaint = true;
        }

        private void DuplicateSelectedNodes()
        {
            List<NodeVisual> cloned = new List<NodeVisual>();
            foreach (var n in graph.Nodes.Where(x => x.IsSelected))
            {
                int count = graph.Nodes.Count(x => x.IsSelected);
                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms);
                SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                BinaryReader br = new BinaryReader(ms);
                NodeVisual clone = DeserializeNode(br);
                clone.X += 40;
                clone.Y += 40;
                clone.GUID = Guid.NewGuid().ToString();
                cloned.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }
            graph.Nodes.ForEach(x => x.IsSelected = false);
            cloned.ForEach(x => x.IsSelected = true);
            cloned.Where(x => x.CustomEditor != null).ToList().ForEach(x => x.CustomEditor.BringToFront());
            graph.Nodes.AddRange(cloned);
            Invalidate();
        }

        private void DeleteSelectedNodes()
        {
            if (graph.Nodes.Exists(x => x.IsSelected))
            {
                // Collect all nodes that will be affected by the deletion BEFORE removing connections
                HashSet<NodeVisual> affectedNodes = new HashSet<NodeVisual>();

                foreach (NodeVisual selectedNode in graph.Nodes.Where(x => x.IsSelected))
                {
                    // Find all downstream nodes from this node before we delete connections
                    CollectDownstreamNodes(selectedNode, affectedNodes);

                    // Also collect nodes that have this node as input
                    List<NodeConnection> incomingConnections = graph.Connections
                        .Where(x => x.OutputNode == selectedNode)
                        .ToList();

                    foreach (NodeConnection conn in incomingConnections)
                    {
                        if (conn.InputNode.HasDynamicTypeSupport() && !graph.Nodes.Any(n => n.IsSelected && n == conn.InputNode))
                        {
                            affectedNodes.Add(conn.InputNode);
                        }
                    }
                }

                // Now remove the selected nodes and their connections
                foreach (NodeVisual selectedNode in graph.Nodes.Where(x => x.IsSelected))
                {
                    Controls.Remove(selectedNode.CustomEditor);
                    graph.Connections.RemoveAll(x => x.OutputNode == selectedNode || x.InputNode == selectedNode);
                }

                graph.Nodes.RemoveAll(x => graph.Nodes.Where(n => n.IsSelected).Contains(x));
                rebuildConnectionDictionary = true;

                // After deletion, update types for all affected nodes
                // We need to do this in multiple passes because disconnecting connections might affect more nodes
                HashSet<NodeVisual> processedNodes = new HashSet<NodeVisual>();
                Queue<NodeVisual> nodesToProcess = new Queue<NodeVisual>();

                // Initial set of affected nodes
                foreach (NodeVisual affectedNode in affectedNodes)
                {
                    if (graph.Nodes.Contains(affectedNode))
                    {
                        nodesToProcess.Enqueue(affectedNode);
                    }
                }

                while (nodesToProcess.Count > 0)
                {
                    NodeVisual currentNode = nodesToProcess.Dequeue();
                    if (processedNodes.Contains(currentNode) || !graph.Nodes.Contains(currentNode))
                        continue;

                    processedNodes.Add(currentNode);

                    // Store connections before type propagation to see what changes
                    List<NodeConnection> connectionsBefore = graph.Connections
                        .Where(c => c.OutputNode == currentNode)
                        .ToList();

                    // Propagate types
                    currentNode.PropagateTypes(graph);

                    // Check for incompatible connections after type reset
                    DynamicNodeAttribute nodeAttr = currentNode.Type?.GetCustomAttribute<DynamicNodeAttribute>();
                    if (nodeAttr != null && nodeAttr.AutoDisconnectIncompatible)
                    {
                        List<NodeConnection> connectionsRemoved = new List<NodeConnection>();
                        DisconnectIncompatibleConnections(currentNode, connectionsRemoved);

                        // If connections were removed, add affected downstream nodes to processing queue
                        foreach (NodeConnection removedConn in connectionsRemoved)
                        {
                            if (!processedNodes.Contains(removedConn.InputNode))
                            {
                                nodesToProcess.Enqueue(removedConn.InputNode);
                            }
                        }
                    }

                    // Add directly connected downstream nodes
                    List<NodeConnection> outputConnections = graph.Connections
                        .Where(c => c.OutputNode == currentNode)
                        .ToList();

                    foreach (NodeConnection conn in outputConnections)
                    {
                        if (!processedNodes.Contains(conn.InputNode))
                        {
                            nodesToProcess.Enqueue(conn.InputNode);
                        }
                    }
                }
            }
            Invalidate();
        }

        /// <summary>
        /// Collects all nodes downstream from the given node
        /// </summary>
        private void CollectDownstreamNodes(NodeVisual startNode, HashSet<NodeVisual> collected)
        {
            List<NodeConnection> outgoingConnections = graph.Connections
                .Where(c => c.OutputNode == startNode)
                .ToList();

            foreach (NodeConnection conn in outgoingConnections)
            {
                if (!collected.Contains(conn.InputNode) && !graph.Nodes.Any(n => n.IsSelected && n == conn.InputNode))
                {
                    collected.Add(conn.InputNode);
                    CollectDownstreamNodes(conn.InputNode, collected); // Recursive call to get all downstream
                }
            }
        }

        /// <summary>
        /// Executes whole node graph (when called parameterless) or given node when specified.
        /// </summary>
        /// <param name="node"></param>
        private void ExecuteFlowControlNode(NodeVisual flowControlNode, Queue<NodeVisual> nodeQueue)
        {
            if (flowControlNode.FlowControlHandler == null)
                return;

            DynamicNodeContext dc = flowControlNode.GetNodeContext();

            // First execute the node to initialize outputs
            flowControlNode.Execute(Context);

            // Create the executeOutputPath callback
            Action<string> executeOutputPath = (outputName) =>
            {
                NodeConnection connection = graph.Connections.FirstOrDefault(
                    x => x.OutputNode == flowControlNode && x.OutputSocketName == outputName);

                if (connection != null)
                {
                    if (outputName == "Exit")
                    {
                        // Queue for later execution
                        nodeQueue.Enqueue(connection.InputNode);
                    }
                    else
                    {
                        // Execute immediately (for loop body, etc.)
                        Execute(connection.InputNode);

                        // After execution, transfer values back to the flow control node
                        // Look for connections that feed back into the flow control node
                        foreach (NodeConnection feedbackConnection in graph.Connections)
                        {
                            if (feedbackConnection.InputNode == flowControlNode)
                            {
                                // Check if this is a feedback input (marked with LoopFeedback attribute)
                                ParameterInfo inputParam = flowControlNode.GetInputs()
                                    .FirstOrDefault(x => x.Name == feedbackConnection.InputSocketName);

                                if (inputParam != null &&
                                    inputParam.GetCustomAttributes(typeof(LoopFeedbackAttribute), false).Any())
                                {
                                    // Transfer the value from the output node to the flow control node
                                    DynamicNodeContext outputContext = feedbackConnection.OutputNode.GetNodeContext();
                                    dc[feedbackConnection.InputSocketName] = outputContext[feedbackConnection.OutputSocketName];
                                }
                            }
                        }
                    }
                }
            };

            // Create the shouldBreak callback
            Func<bool> shouldBreak = () => breakExecution;

            // Execute the flow control logic
            flowControlNode.FlowControlHandler.ExecuteFlowControl(
                Context,
                dc,
                executeOutputPath,
                shouldBreak);

            // Clear break flag if it was set
            if (breakExecution)
            {
                breakExecution = false;
                executionStack.Clear();
            }
        }

        public void Execute(NodeVisual node = null)
        {
            Queue<NodeVisual> nodeQueue = new Queue<NodeVisual>();
            nodeQueue.Enqueue(node);

            while (nodeQueue.Count > 0)
            {
                //Refresh();
                if (breakExecution)
                {
                    breakExecution = false;
                    executionStack.Clear();
                    return;
                }

                NodeVisual init = nodeQueue.Dequeue() ?? graph.Nodes.FirstOrDefault(x => x.ExecInit);
                if (init != null)
                {
                    init.Feedback = FeedbackType.Debug;

                    Resolve(init);

                    // Check if this node has a flow control handler
                    if (init.FlowControlHandler != null)
                    {
                        // Handle flow control node
                        ExecuteFlowControlNode(init, nodeQueue);
                    }
                    else
                    {
                        // Normal node execution
                        init.Execute(Context);

                        NodeConnection connection =
                            graph.Connections.FirstOrDefault(
                                x => x.OutputNode == init && x.IsExecution && x.OutputSocket.Value != null && (x.OutputSocket.Value as ExecutionPath).IsSignaled);
                        if (connection == null)
                        {
                            connection = graph.Connections.FirstOrDefault(x => x.OutputNode == init && x.IsExecution && x.OutputSocket.IsMainExecution);
                        }
                        else
                        {
                            executionStack.Push(init);
                        }
                        if (connection != null)
                        {
                            connection.InputNode.IsBackExecuted = false;
                            //Execute(connection.InputNode);
                            nodeQueue.Enqueue(connection.InputNode);
                        }
                        else
                        {
                            if (executionStack.Count > 0)
                            {
                                NodeVisual back = executionStack.Pop();
                                back.IsBackExecuted = true;
                                Execute(back);
                            }
                        }
                    }
                }
            }
        }

        public List<NodeVisual> GetNodes(params string[] nodeNames)
        {
            IEnumerable<NodeVisual> nodes = graph.Nodes.Where(x => nodeNames.Contains(x.Name));
            return nodes.ToList();
        }

        /// <summary>
        /// Adds a node to the graph by method name at the specified coordinates
        /// </summary>
        /// <param name="methodName">Name of the method decorated with NodeAttribute</param>
        /// <param name="x">X coordinate for the node</param>
        /// <param name="y">Y coordinate for the node</param>
        /// <returns>True if the node was successfully added, false otherwise</returns>
        public bool AddNodeByMethodName(string methodName, float x, float y)
        {
            if (Context == null) return false;

            MethodInfo[] methods = Context.GetType().GetMethods();
            NodeToken nodeToken = methods.Select(m => new NodeToken()
            {
                Method = m,
                Attribute = m.GetCustomAttributes(typeof(NodeAttribute), false)
                    .Cast<NodeAttribute>()
                    .FirstOrDefault()
            }).FirstOrDefault(n => n.Attribute != null && n.Method.Name == methodName);

            if (nodeToken != null)
            {
                Point originalMouseLocation = lastMouseLocation;
                lastMouseLocation = new Point((int)x, (int)y);
                AddNodeToGraph(nodeToken);
                lastMouseLocation = originalMouseLocation;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a node to the graph by node name at the specified coordinates
        /// </summary>
        /// <param name="nodeName">Display name of the node as defined in NodeAttribute</param>
        /// <param name="x">X coordinate for the node</param>
        /// <param name="y">Y coordinate for the node</param>
        /// <returns>True if the node was successfully added, false otherwise</returns>
        public bool AddNodeByName(string nodeName, float x, float y)
        {
            if (Context == null) return false;

            MethodInfo[] methods = Context.GetType().GetMethods();
            NodeToken nodeToken = methods.Select(m => new NodeToken()
            {
                Method = m,
                Attribute = m.GetCustomAttributes(typeof(NodeAttribute), false)
                    .Cast<NodeAttribute>()
                    .FirstOrDefault()
            }).FirstOrDefault(n => n.Attribute != null && n.Attribute.Name == nodeName);

            if (nodeToken != null)
            {
                Point originalMouseLocation = lastMouseLocation;
                lastMouseLocation = new Point((int)x, (int)y);
                AddNodeToGraph(nodeToken);
                lastMouseLocation = originalMouseLocation;
                return true;
            }

            return false;
        }

        public bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            IEnumerable<NodeConnection> connections = graph.Connections.Where(x => x.OutputNode == startNode && !x.IsExecution);
            foreach (var connection in connections)
            {
                if (connection.InputNode == endNode)
                {
                    return true;
                }
                bool nextImpact = HasImpact(connection.InputNode, endNode);
                if (nextImpact)
                {
                    return true;
                }
            }

            return false;
        }

        public void ExecuteResolving(params string[] nodeNames)
        {
            IEnumerable<NodeVisual> nodes = graph.Nodes.Where(x => nodeNames.Contains(x.Name));

            foreach (var node in nodes)
            {
                ExecuteResolvingInternal(node);
            }
        }

        private void ExecuteResolvingInternal(NodeVisual node)
        {
            DynamicNodeContext icontext = node.GetNodeContext();
            foreach (var input in node.GetInputs())
            {
                // Skip inputs marked with LoopFeedback attribute
                if (input.GetCustomAttributes(typeof(LoopFeedbackAttribute), false).Any())
                {
                    continue;
                }

                NodeConnection connection =
                    graph.Connections.FirstOrDefault(x => x.InputNode == node && x.InputSocketName == input.Name);
                if (connection != null)
                {
                    Resolve(connection.OutputNode);

                    connection.OutputNode.Execute(Context);

                    ExecuteResolvingInternal(connection.OutputNode);

                    DynamicNodeContext ocontext = connection.OutputNode.GetNodeContext();
                    icontext[connection.InputSocketName] = ocontext[connection.OutputSocketName];
                }
            }
        }

        /// <summary>
        /// Resolves given node, resolving it all dependencies recursively.
        /// </summary>
        /// <param name="node"></param>
        private void Resolve(NodeVisual node)
        {
            DynamicNodeContext icontext = node.GetNodeContext();
            foreach (var input in node.GetInputs())
            {
                // Skip inputs marked with LoopFeedback attribute
                if (input.GetCustomAttributes(typeof(LoopFeedbackAttribute), false).Any())
                {
                    continue;
                }

                NodeConnection connection = GetConnection(node.GUID + input.Name);
                //graph.Connections.FirstOrDefault(x => x.InputNode == node && x.InputSocketName == input.Name);
                if (connection != null)
                {
                    Resolve(connection.OutputNode);
                    if (!connection.OutputNode.Callable)
                    {
                        connection.OutputNode.Execute(Context);
                    }
                    DynamicNodeContext ocontext = connection.OutputNode.GetNodeContext();
                    icontext[connection.InputSocketName] = ocontext[connection.OutputSocketName];
                }
            }
        }

        private NodeConnection GetConnection(string v)
        {
            if (rebuildConnectionDictionary)
            {
                rebuildConnectionDictionary = false;
                connectionDictionary.Clear();
                foreach (var conn in graph.Connections)
                {
                    connectionDictionary.Add(conn.InputNode.GUID + conn.InputSocketName, conn);
                }
            }
            NodeConnection nc = null;
            if (connectionDictionary.TryGetValue(v, out nc))
            {
                return nc;
            }
            return null;
        }

        public string ExportToXml()
        {
            XmlDocument xml = new XmlDocument();

            XmlElement el = (XmlElement)xml.AppendChild(xml.CreateElement("NodeGrap"));
            el.SetAttribute("Created", DateTime.Now.ToString());
            XmlNode nodes = el.AppendChild(xml.CreateElement("Nodes"));
            foreach (var node in graph.Nodes)
            {
                XmlElement xmlNode = (XmlElement)nodes.AppendChild(xml.CreateElement("Node"));
                xmlNode.SetAttribute("Name", node.XmlExportName);
                xmlNode.SetAttribute("Id", node.GetGuid());
                XmlElement xmlContext = (XmlElement)xmlNode.AppendChild(xml.CreateElement("Context"));
                DynamicNodeContext context = node.GetNodeContext();
                foreach (var kv in context)
                {
                    XmlElement ce = (XmlElement)xmlContext.AppendChild(xml.CreateElement("ContextMember"));
                    ce.SetAttribute("Name", kv);
                    ce.SetAttribute("Value", Convert.ToString(context[kv] ?? ""));
                    ce.SetAttribute("Type", context[kv] == null ? "" : context[kv].GetType().FullName);
                }
            }
            XmlNode connections = el.AppendChild(xml.CreateElement("Connections"));
            foreach (var conn in graph.Connections)
            {
                XmlElement xmlConn = (XmlElement)nodes.AppendChild(xml.CreateElement("Connection"));
                xmlConn.SetAttribute("OutputNodeId", conn.OutputNode.GetGuid());
                xmlConn.SetAttribute("OutputNodeSocket", conn.OutputSocketName);
                xmlConn.SetAttribute("InputNodeId", conn.InputNode.GetGuid());
                xmlConn.SetAttribute("InputNodeSocket", conn.InputSocketName);
            }
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                xml.Save(writer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes current node graph to binary data.
        /// </summary>        
        public byte[] Serialize()
        {
            using (var bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write("NodeSystemP"); //recognization string
                bw.Write(1000); //version
                bw.Write(graph.Nodes.Count);
                foreach (var node in graph.Nodes)
                {
                    SerializeNode(bw, node);
                }
                bw.Write(graph.Connections.Count);
                foreach (var connection in graph.Connections)
                {
                    bw.Write(connection.OutputNode.GUID);
                    bw.Write(connection.OutputSocketName);

                    bw.Write(connection.InputNode.GUID);
                    bw.Write(connection.InputSocketName);
                    bw.Write(0); //additional data size per connection
                }
                bw.Write(0); //additional data size per graph
                return (bw.BaseStream as MemoryStream).ToArray();
            }
        }

        private static void SerializeNode(BinaryWriter bw, NodeVisual node)
        {
            bw.Write(node.GUID);
            bw.Write(node.X);
            bw.Write(node.Y);
            bw.Write(node.Callable);
            bw.Write(node.ExecInit);
            bw.Write(node.Name);
            bw.Write(node.Order);
            if (node.CustomEditor == null)
            {
                bw.Write("");
                bw.Write("");
            }
            else
            {
                bw.Write(node.CustomEditor.GetType().Assembly.GetName().Name);
                bw.Write(node.CustomEditor.GetType().FullName);
            }
            bw.Write(node.Type.Name);
            byte[] context = node.GetNodeContext().Serialize();
            bw.Write(context.Length);
            bw.Write(context);
            bw.Write(8); //additional data size per node
            bw.Write(node.Int32Tag);
            bw.Write(node.NodeColor.ToArgb());
        }

        /// <summary>
        /// Restores node graph state from previously serialized binary data.
        /// </summary>
        /// <param name="data"></param>
        public void Deserialize(byte[] data)
        {
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                string ident = br.ReadString();
                if (ident != "NodeSystemP") return;
                rebuildConnectionDictionary = true;
                graph.Connections.Clear();
                graph.Nodes.Clear();
                Controls.Clear();

                int version = br.ReadInt32();
                int nodeCount = br.ReadInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    NodeVisual nv = DeserializeNode(br);

                    graph.Nodes.Add(nv);
                }
                int connectionsCount = br.ReadInt32();
                for (int i = 0; i < connectionsCount; i++)
                {
                    NodeConnection con = new NodeConnection();
                    string og = br.ReadString();
                    con.OutputNode = graph.Nodes.FirstOrDefault(x => x.GUID == og);
                    con.OutputSocketName = br.ReadString();
                    string ig = br.ReadString();
                    con.InputNode = graph.Nodes.FirstOrDefault(x => x.GUID == ig);
                    con.InputSocketName = br.ReadString();
                    br.ReadBytes(br.ReadInt32()); //read additional data

                    graph.Connections.Add(con);
                    rebuildConnectionDictionary = true;
                }
                br.ReadBytes(br.ReadInt32()); //read additional data
            }

            // Propagate types for all connections after loading
            PropagateAllConnectionTypes();

            Refresh();
        }

        private NodeVisual DeserializeNode(BinaryReader br)
        {
            NodeVisual nv = new NodeVisual();
            nv.GUID = br.ReadString();
            nv.X = br.ReadSingle();
            nv.Y = br.ReadSingle();
            nv.Callable = br.ReadBoolean();
            nv.ExecInit = br.ReadBoolean();
            nv.Name = br.ReadString();
            nv.Order = br.ReadInt32();
            string customEditorAssembly = br.ReadString();
            string customEditor = br.ReadString();
            nv.Type = Context.GetType().GetMethod(br.ReadString());
            NodeAttribute attribute = (NodeAttribute)nv.Type.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault();
            if (attribute != null)
            {
                nv.CustomWidth = attribute.Width;
                nv.CustomHeight = attribute.Height;
            }
            nv.GetNodeContext().Deserialize(br.ReadBytes(br.ReadInt32()));
            int additional = br.ReadInt32(); //read additional data
            if (additional >= 4)
            {
                nv.Int32Tag = br.ReadInt32();
                if (additional >= 8)
                {
                    nv.NodeColor = Color.FromArgb(br.ReadInt32());
                }
            }
            if (additional > 8)
            {
                br.ReadBytes(additional - 8);
            }

            if (customEditor != "")
            {
                nv.CustomEditor =
                    Activator.CreateInstance(AppDomain.CurrentDomain, customEditorAssembly, customEditor).Unwrap() as Control;

                Control ctrl = nv.CustomEditor;
                if (ctrl != null)
                {
                    ctrl.Tag = nv;
                    Controls.Add(ctrl);
                }
                nv.LayoutEditor();
            }
            return nv;
        }

        /// <summary>
        /// Clears node graph state.
        /// </summary>
        public void Clear()
        {
            graph.Nodes.Clear();
            graph.Connections.Clear();
            Controls.Clear();
            Refresh();
            rebuildConnectionDictionary = true;
        }

        /// <summary>
        /// Serializes current node graph to JSON string.
        /// </summary>
        public string SerializeToJson()
        {
            NodeGraphModel model = new NodeGraphModel();
            model.Version = 1001;

            // Serialize nodes
            foreach (NodeVisual node in graph.Nodes)
            {
                NodeModel nodeModel = new NodeModel
                {
                    Guid = node.GUID,
                    X = node.X,
                    Y = node.Y,
                    Callable = node.Callable,
                    ExecInit = node.ExecInit,
                    Name = node.Name,
                    Order = node.Order,
                    XmlExportName = node.XmlExportName,
                    CustomWidth = node.CustomWidth,
                    CustomHeight = node.CustomHeight,
                    Int32Tag = node.Int32Tag,
                    NodeColor = node.NodeColor.ToArgb(),
                    MethodName = node.Type?.Name,
                    Context = node.GetNodeContext().GetPropertiesForSerialization()
                };

                // Add custom editor info if present
                if (node.CustomEditor != null)
                {
                    nodeModel.CustomEditor = new CustomEditorInfo
                    {
                        AssemblyName = node.CustomEditor.GetType().Assembly.GetName().Name,
                        TypeName = node.CustomEditor.GetType().FullName
                    };
                }

                // Add flow control handler if present
                if (node.FlowControlHandler != null)
                {
                    nodeModel.FlowControlHandler = node.FlowControlHandler.GetType().FullName;
                }

                model.Nodes.Add(nodeModel);
            }

            // Serialize connections
            foreach (var connection in graph.Connections)
            {
                ConnectionModel connModel = new ConnectionModel
                {
                    OutputNodeId = connection.OutputNode.GUID,
                    OutputSocketName = connection.OutputSocketName,
                    InputNodeId = connection.InputNode.GUID,
                    InputSocketName = connection.InputSocketName
                };
                model.Connections.Add(connModel);
            }

            return JsonConvert.SerializeObject(model, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Deserializes node graph from JSON string.
        /// </summary>
        public void DeserializeFromJson(string json)
        {
            NodeGraphModel model = JsonConvert.DeserializeObject<NodeGraphModel>(json);
            if (model == null)
                return;

            rebuildConnectionDictionary = true;
            graph.Connections.Clear();
            graph.Nodes.Clear();
            Controls.Clear();

            // Deserialize nodes
            foreach (NodeModel nodeModel in model.Nodes)
            {
                NodeVisual node = new NodeVisual();
                node.GUID = nodeModel.Guid ?? Guid.NewGuid().ToString();
                node.X = nodeModel.X;
                node.Y = nodeModel.Y;
                node.Callable = nodeModel.Callable;
                node.ExecInit = nodeModel.ExecInit;
                node.Name = nodeModel.Name;
                node.Order = nodeModel.Order;
                node.XmlExportName = nodeModel.XmlExportName;
                node.CustomWidth = nodeModel.CustomWidth;
                node.CustomHeight = nodeModel.CustomHeight;
                node.Int32Tag = nodeModel.Int32Tag;
                node.NodeColor = Color.FromArgb(nodeModel.NodeColor);

                // Set method
                if (!string.IsNullOrEmpty(nodeModel.MethodName) && Context != null)
                {
                    node.Type = Context.GetType().GetMethod(nodeModel.MethodName);

                    if (node.Type != null)
                    {
                        NodeAttribute attribute = node.Type.GetCustomAttributes(typeof(NodeAttribute), false)
                            .Cast<NodeAttribute>()
                            .FirstOrDefault();
                        if (attribute != null)
                        {
                            if (node.CustomWidth == 0)
                                node.CustomWidth = attribute.Width;
                            if (node.CustomHeight == 0)
                                node.CustomHeight = attribute.Height;

                            // Create flow control handler if specified
                            if (attribute.FlowControlHandler != null)
                            {
                                node.FlowControlHandler = Activator.CreateInstance(attribute.FlowControlHandler) as IFlowControlNode;
                            }
                        }
                    }
                }

                // Set context
                if (nodeModel.Context != null)
                {
                    node.GetNodeContext().SetPropertiesFromSerialization(nodeModel.Context, node);
                }

                // Create custom editor
                if (nodeModel.CustomEditor != null &&
                    !string.IsNullOrEmpty(nodeModel.CustomEditor.AssemblyName) &&
                    !string.IsNullOrEmpty(nodeModel.CustomEditor.TypeName))
                {
                    try
                    {
                        node.CustomEditor = Activator.CreateInstance(
                            AppDomain.CurrentDomain,
                            nodeModel.CustomEditor.AssemblyName,
                            nodeModel.CustomEditor.TypeName).Unwrap() as Control;

                        if (node.CustomEditor != null)
                        {
                            node.CustomEditor.Tag = node;
                            Controls.Add(node.CustomEditor);
                            node.LayoutEditor();
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }

                graph.Nodes.Add(node);
            }

            // Deserialize connections
            foreach (var connModel in model.Connections)
            {
                NodeConnection connection = new NodeConnection();
                connection.OutputNode = graph.Nodes.FirstOrDefault(x => x.GUID == connModel.OutputNodeId);
                connection.OutputSocketName = connModel.OutputSocketName;
                connection.InputNode = graph.Nodes.FirstOrDefault(x => x.GUID == connModel.InputNodeId);
                connection.InputSocketName = connModel.InputSocketName;

                if (connection.OutputNode != null && connection.InputNode != null)
                {
                    graph.Connections.Add(connection);
                }
            }

            // Propagate types for all connections after loading
            PropagateAllConnectionTypes();

            rebuildConnectionDictionary = true;
            Refresh();
        }

        private void NodesControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NodeDragData)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void NodesControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NodeDragData)))
            {
                e.Effect = DragDropEffects.Copy;

                // Update drag preview position if active
                if (dragPreviewNode != null)
                {
                    // Convert screen coordinates to client coordinates, then to world coordinates
                    Point clientPoint = PointToClient(new Point(e.X, e.Y));
                    PointF worldPoint = ScreenToWorld(clientPoint);

                    // Store old position for socket update
                    float oldX = dragPreviewNode.X;
                    float oldY = dragPreviewNode.Y;

                    // Update preview position
                    dragPreviewNode.X = worldPoint.X;
                    dragPreviewNode.Y = worldPoint.Y;

                    // Update socket positions with the delta movement
                    float dx = worldPoint.X - oldX;
                    float dy = worldPoint.Y - oldY;
                    dragPreviewNode.UpdateSocketPositions(dx, dy);

                    // Trigger repaint
                    Invalidate();
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void NodesControl_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NodeDragData)))
            {
                NodeDragData dragData = e.Data.GetData(typeof(NodeDragData)) as NodeDragData;
                if (dragData?.MethodName != null)
                {
                    // Convert screen coordinates to world coordinates
                    Point clientPoint = PointToClient(new Point(e.X, e.Y));
                    PointF worldPoint = ScreenToWorld(clientPoint);

                    // Create a new node using the method from the dragged item
                    AddNodeByMethodName(dragData.MethodName, worldPoint.X, worldPoint.Y);
                }
            }
        }

        private void NodesControl_DragLeave(object sender, EventArgs e)
        {
            // Hide preview when drag leaves the control
            if (dragPreviewNode != null)
            {
                dragPreviewNode.X = -1000;
                dragPreviewNode.Y = -1000;
                Invalidate();
            }
        }

        /// <summary>
        /// Starts showing a drag preview for a node being dragged from toolbox
        /// </summary>
        public void StartDragPreview(NodeToolboxItem item)
        {
            if (item?.Method == null) return;

            // Create a preview node
            dragPreviewNode = new NodeVisual();
            dragPreviewNode.Type = item.Method;
            dragPreviewNode.Name = item.DisplayName;
            dragPreviewNode.Callable = item.Attribute.IsCallable;
            dragPreviewNode.ExecInit = item.Attribute.IsExecutionInitiator;
            dragPreviewNode.CustomWidth = item.Attribute.Width > 0 ? item.Attribute.Width : -1;
            dragPreviewNode.CustomHeight = item.Attribute.Height > 0 ? item.Attribute.Height : -1;

            // Position it initially off-screen
            dragPreviewNode.X = -1000;
            dragPreviewNode.Y = -1000;

            // Force socket generation by calling GetSockets
            dragPreviewNode.GetSockets();
        }

        /// <summary>
        /// Stops showing the drag preview
        /// </summary>
        public void StopDragPreview()
        {
            dragPreviewNode = null;
            Invalidate(); // Redraw to remove preview
        }
    }

    /// <summary>
    /// Represents a draggable node item from the toolbox (moved here for shared access)
    /// </summary>
    public class NodeToolboxItem
    {
        public MethodInfo Method { get; set; }
        public NodeAttribute Attribute { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// Data object for node drag-and-drop operations
    /// </summary>
    [Serializable]
    public class NodeDragData
    {
        public string MethodName { get; set; }
        public string DisplayName { get; set; }
    }
}
