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
        private Stack<NodeVisual> executionStack = new Stack<NodeVisual>();
        private bool rebuildConnectionDictionary = true;
        private Dictionary<string, NodeConnection> connectionDictionary = new Dictionary<string, NodeConnection>();

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

        /// <summary>
        /// Default constructor
        /// </summary>
        public NodesControl()
        {
            InitializeComponent();
            timer.Interval = 30;
            timer.Tick += TimerOnTick;
            timer.Start();
            KeyDown += OnKeyDown;
            SetStyle(ControlStyles.Selectable, true);
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

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (DesignMode) return;
            if (needRepaint)
            {
                Invalidate();
            }
        }

        private void NodesControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;

            graph.Draw(e.Graphics, PointToClient(MousePosition), MouseButtons);

            if (dragSocket != null)
            {
                var pen = new Pen(Color.Black, 2);
                NodesGraph.DrawConnection(e.Graphics, pen, dragConnectionBegin, dragConnectionEnd);
            }

            if (selectionStart != PointF.Empty)
            {
                var rect = Rectangle.Round(MakeRect(selectionStart, selectionEnd));
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), rect);
                e.Graphics.DrawRectangle(new Pen(Color.DodgerBlue), rect);
            }

            needRepaint = false;
        }

        private static RectangleF MakeRect(PointF a, PointF b)
        {
            var x1 = a.X;
            var x2 = b.X;
            var y1 = a.Y;
            var y2 = b.Y;
            return new RectangleF(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        private void NodesControl_MouseMove(object sender, MouseEventArgs e)
        {
            var em = PointToScreen(e.Location);
            if (selectionStart != PointF.Empty)
            {
                selectionEnd = e.Location;
            }
            if (mdown)
            {
                foreach (var node in graph.Nodes.Where(x => x.IsSelected))
                {
                    node.X += em.X - lastmpos.X;
                    node.Y += em.Y - lastmpos.Y;
                    node.DiscardCache();
                    node.LayoutEditor();
                }
                if (graph.Nodes.Exists(x => x.IsSelected))
                {
                    var n = graph.Nodes.FirstOrDefault(x => x.IsSelected);
                    var bound = new RectangleF(new PointF(n.X, n.Y), n.GetNodeBounds());
                    foreach (var node in graph.Nodes.Where(x => x.IsSelected))
                    {
                        bound = RectangleF.Union(bound, new RectangleF(new PointF(node.X, node.Y), node.GetNodeBounds()));
                    }
                    OnShowLocation(bound);
                }
                Invalidate();

                if (dragSocket != null)
                {
                    var center = new PointF(dragSocket.X + dragSocket.Width / 2f, dragSocket.Y + dragSocket.Height / 2f);
                    if (dragSocket.Input)
                    {
                        dragConnectionBegin.X += em.X - lastmpos.X;
                        dragConnectionBegin.Y += em.Y - lastmpos.Y;
                        dragConnectionEnd = center;
                        OnShowLocation(new RectangleF(dragConnectionBegin, new SizeF(10, 10)));
                    }
                    else
                    {
                        dragConnectionBegin = center;
                        dragConnectionEnd.X += em.X - lastmpos.X;
                        dragConnectionEnd.Y += em.Y - lastmpos.Y;
                        OnShowLocation(new RectangleF(dragConnectionEnd, new SizeF(10, 10)));
                    }

                }
                lastmpos = em;
            }

            needRepaint = true;
        }

        private void NodesControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                selectionStart = PointF.Empty;

                Focus();

                if ((ModifierKeys & Keys.Shift) != Keys.Shift)
                {
                    graph.Nodes.ForEach(x => x.IsSelected = false);
                }

                var node =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetHeaderSize()).Contains(e.Location));

                if (node != null && !mdown)
                {

                    node.IsSelected = true;

                    node.Order = graph.Nodes.Min(x => x.Order) - 1;
                    if (node.CustomEditor != null)
                    {
                        node.CustomEditor.BringToFront();
                    }
                    mdown = true;
                    lastmpos = PointToScreen(e.Location);

                    Refresh();
                }
                if (node == null && !mdown)
                {
                    var nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));
                    if (nodeWhole != null)
                    {
                        node = nodeWhole;
                        var socket = nodeWhole.GetSockets().FirstOrDefault(x => x.GetBounds().Contains(e.Location));
                        if (socket != null)
                        {
                            if ((ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                NodeConnection connection =
                                    graph.Connections.FirstOrDefault(
                                        x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                                if (connection != null)
                                {
                                    dragSocket =
                                        connection.OutputNode.GetSockets()
                                            .FirstOrDefault(x => x.Name == connection.OutputSocketName);
                                    dragSocketNode = connection.OutputNode;
                                }
                                else
                                {
                                    connection =
                                        graph.Connections.FirstOrDefault(
                                            x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                                    if (connection != null)
                                    {
                                        dragSocket =
                                            connection.InputNode.GetSockets()
                                                .FirstOrDefault(x => x.Name == connection.InputSocketName);
                                        dragSocketNode = connection.InputNode;
                                    }
                                }

                                graph.Connections.Remove(connection);
                                rebuildConnectionDictionary = true;

                                // Handle type propagation after disconnection
                                if (connection != null && connection.InputNode.HasDynamicTypeSupport())
                                {
                                    connection.InputNode.PropagateTypes(graph);
                                    PropagateTypesDownstream(connection.InputNode);
                                }
                            }
                            else
                            {
                                dragSocket = socket;
                                dragSocketNode = nodeWhole;
                            }
                            dragConnectionBegin = e.Location;
                            dragConnectionEnd = e.Location;
                            mdown = true;
                            lastmpos = PointToScreen(e.Location);
                        }
                    }
                    else
                    {
                        selectionStart = selectionEnd = e.Location;
                    }
                }
                if (node != null)
                {
                    OnNodeContextSelected(node.GetNodeContext());
                }
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

            // Check for exact match
            if (outputType == inputType) return true;

            // Check for inheritance
            if (outputType.IsSubclassOf(inputType)) return true;

            // Check for interface implementation
            if (inputType.IsInterface && inputType.IsAssignableFrom(outputType)) return true;

            // Special case: Check if output type can be assigned to input type
            // This handles cases like string[] to IEnumerable<string>
            if (inputType.IsAssignableFrom(outputType)) return true;

            return false;
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
            if (selectionStart != PointF.Empty)
            {
                var rect = MakeRect(selectionStart, selectionEnd);
                graph.Nodes.ForEach(
                    x => x.IsSelected = rect.Contains(new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds())));
                selectionStart = PointF.Empty;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().FirstOrDefault(x => x.GetBounds().Contains(e.Location));
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

            // Propagate types through all downstream nodes
            PropagateTypesDownstream(connection.InputNode);
        }

        /// <summary>
        /// Propagates types to all nodes downstream from the given node
        /// </summary>
        private void PropagateTypesDownstream(NodeVisual startNode)
        {
            HashSet<NodeVisual> visited = new HashSet<NodeVisual>();
            Queue<NodeVisual> toProcess = new Queue<NodeVisual>();
            toProcess.Enqueue(startNode);

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

                // Reset types for disconnected input node if it's dynamic
                if (conn.InputNode.HasDynamicTypeSupport())
                {
                    conn.InputNode.PropagateTypes(graph);
                    PropagateTypesDownstream(conn.InputNode);
                }
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

                // Reset types for disconnected input node if it's dynamic
                if (conn.InputNode.HasDynamicTypeSupport())
                {
                    conn.InputNode.PropagateTypes(graph);
                }
            }
        }

        private void AddToMenu(ToolStripItemCollection items, NodeToken token, string path, EventHandler click)
        {
            var pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var first = pathParts.FirstOrDefault();
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
            var next = string.Join("/", pathParts.Skip(1));
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
                    var i = allContextItems.Keys.FirstOrDefault(x => x.Name == item.Name);
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

            if (e.Button == MouseButtons.Right)
            {
                var methods = Context.GetType().GetMethods();
                var nodes =
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

                var context = new ContextMenuStrip();
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
                        var sel = graph.Nodes.Where(x => x.IsSelected).ToArray();
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
                    var handy = allContextItems.Where(x => x.Value > 0 && !string.IsNullOrEmpty(((x.Key.Tag) as NodeToken).Attribute.Menu)).OrderByDescending(x => x.Value).Take(8);
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
            var cloned = new List<NodeVisual>();
            foreach (var n in graph.Nodes.Where(x => x.IsSelected))
            {
                int count = graph.Nodes.Count(x => x.IsSelected);
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = DeserializeNode(br);
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
            var nodeQueue = new Queue<NodeVisual>();
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

                var init = nodeQueue.Dequeue() ?? graph.Nodes.FirstOrDefault(x => x.ExecInit);
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

                        var connection =
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
                                var back = executionStack.Pop();
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
            var nodes = graph.Nodes.Where(x => nodeNames.Contains(x.Name));
            return nodes.ToList();
        }

        public bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            var connections = graph.Connections.Where(x => x.OutputNode == startNode && !x.IsExecution);
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
            var nodes = graph.Nodes.Where(x => nodeNames.Contains(x.Name));

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

                var connection =
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

                var connection = GetConnection(node.GUID + input.Name);
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
            var xml = new XmlDocument();

            XmlElement el = (XmlElement)xml.AppendChild(xml.CreateElement("NodeGrap"));
            el.SetAttribute("Created", DateTime.Now.ToString());
            var nodes = el.AppendChild(xml.CreateElement("Nodes"));
            foreach (var node in graph.Nodes)
            {
                var xmlNode = (XmlElement)nodes.AppendChild(xml.CreateElement("Node"));
                xmlNode.SetAttribute("Name", node.XmlExportName);
                xmlNode.SetAttribute("Id", node.GetGuid());
                var xmlContext = (XmlElement)xmlNode.AppendChild(xml.CreateElement("Context"));
                DynamicNodeContext context = node.GetNodeContext();
                foreach (var kv in context)
                {
                    var ce = (XmlElement)xmlContext.AppendChild(xml.CreateElement("ContextMember"));
                    ce.SetAttribute("Name", kv);
                    ce.SetAttribute("Value", Convert.ToString(context[kv] ?? ""));
                    ce.SetAttribute("Type", context[kv] == null ? "" : context[kv].GetType().FullName);
                }
            }
            var connections = el.AppendChild(xml.CreateElement("Connections"));
            foreach (var conn in graph.Connections)
            {
                var xmlConn = (XmlElement)nodes.AppendChild(xml.CreateElement("Connection"));
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
                var ident = br.ReadString();
                if (ident != "NodeSystemP") return;
                rebuildConnectionDictionary = true;
                graph.Connections.Clear();
                graph.Nodes.Clear();
                Controls.Clear();

                var version = br.ReadInt32();
                int nodeCount = br.ReadInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    var nv = DeserializeNode(br);

                    graph.Nodes.Add(nv);
                }
                var connectionsCount = br.ReadInt32();
                for (int i = 0; i < connectionsCount; i++)
                {
                    var con = new NodeConnection();
                    var og = br.ReadString();
                    con.OutputNode = graph.Nodes.FirstOrDefault(x => x.GUID == og);
                    con.OutputSocketName = br.ReadString();
                    var ig = br.ReadString();
                    con.InputNode = graph.Nodes.FirstOrDefault(x => x.GUID == ig);
                    con.InputSocketName = br.ReadString();
                    br.ReadBytes(br.ReadInt32()); //read additional data

                    graph.Connections.Add(con);
                    rebuildConnectionDictionary = true;
                }
                br.ReadBytes(br.ReadInt32()); //read additional data
            }
            
            // Propagate types for all dynamic nodes after loading
            foreach (NodeVisual node in graph.Nodes)
            {
                if (node.HasDynamicTypeSupport())
                {
                    node.PropagateTypes(graph);
                }
            }
            
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
            var customEditorAssembly = br.ReadString();
            var customEditor = br.ReadString();
            nv.Type = Context.GetType().GetMethod(br.ReadString());
            var attribute = nv.Type.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault();
            if (attribute != null)
            {
                nv.CustomWidth = attribute.Width;
                nv.CustomHeight = attribute.Height;
            }
            nv.GetNodeContext().Deserialize(br.ReadBytes(br.ReadInt32()));
            var additional = br.ReadInt32(); //read additional data
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
                var connModel = new ConnectionModel
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
            var model = JsonConvert.DeserializeObject<NodeGraphModel>(json);
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
                var connection = new NodeConnection();
                connection.OutputNode = graph.Nodes.FirstOrDefault(x => x.GUID == connModel.OutputNodeId);
                connection.OutputSocketName = connModel.OutputSocketName;
                connection.InputNode = graph.Nodes.FirstOrDefault(x => x.GUID == connModel.InputNodeId);
                connection.InputSocketName = connModel.InputSocketName;

                if (connection.OutputNode != null && connection.InputNode != null)
                {
                    graph.Connections.Add(connection);
                }
            }
            
            // Propagate types for all dynamic nodes after loading
            foreach (NodeVisual node in graph.Nodes)
            {
                if (node.HasDynamicTypeSupport())
                {
                    node.PropagateTypes(graph);
                }
            }

            rebuildConnectionDictionary = true;
            Refresh();
        }
    }
}
