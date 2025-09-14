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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NodeEditor
{
    /// <summary>
    /// Class that represents one instance of node.
    /// </summary>
    public class NodeVisual
    {
        public const float NodeWidth = 140;
        public const float HeaderHeight = 20;
        public const float ComponentPadding = 2;

        /// <summary>
        /// Current node name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current node position X coordinate.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Current node position Y coordinate.
        /// </summary>
        public float Y { get; set; }
        internal MethodInfo Type { get; set; }
        internal int Order { get; set; }
        internal bool Callable { get; set; }
        internal bool ExecInit { get; set; }
        internal bool IsSelected { get; set; }
        internal FeedbackType Feedback { get; set; }
        internal IFlowControlNode FlowControlHandler { get; set; }
        private DynamicNodeContext nodeContext { get; set; } 
        public Control CustomEditor { get; internal set; }
        internal string GUID = Guid.NewGuid().ToString();
        internal Color NodeColor = Color.LightCyan;
        public bool IsBackExecuted { get; internal set; }
        private SocketVisual[] socketCache;
        
        // Dynamic typing support
        private Dictionary<string, SocketTypeInfo> socketTypeInfo = new Dictionary<string, SocketTypeInfo>();

        /// <summary>
        /// Tag for various puposes - may be used freely.
        /// </summary>
        public int Int32Tag = 0;
        public string XmlExportName { get; internal set; }

        internal int CustomWidth = -1;
        internal int CustomHeight = -1;

        internal NodeVisual()
        {
            Feedback = FeedbackType.Debug;
        }

        public string GetGuid()
        {
            return GUID;
        }

        internal SocketVisual[] GetSockets()
        {
            if(socketCache!=null)
            {
                return socketCache;
            }

            List<SocketVisual> socketList = new List<SocketVisual>();
            float curInputH = HeaderHeight + ComponentPadding;
            float curOutputH = HeaderHeight + ComponentPadding;

            float NodeWidth = GetNodeBounds().Width;

            if (Callable)
            {
                if (!ExecInit)
                {
                    socketList.Add(new SocketVisual()
                    {
                        Height = SocketVisual.SocketHeight,
                        Name = "Enter",
                        Type = typeof (ExecutionPath),
                        IsMainExecution = true,
                        Width = SocketVisual.SocketHeight,
                        X = X,
                        Y = Y + curInputH,
                        Input = true
                    });
                }
                socketList.Add(new SocketVisual()
                {
                    Height = SocketVisual.SocketHeight,
                    Name = "Exit",
                    IsMainExecution = true,
                    Type = typeof (ExecutionPath),
                    Width = SocketVisual.SocketHeight,
                    X = X + NodeWidth - SocketVisual.SocketHeight,
                    Y = Y + curOutputH
                });
                curOutputH += SocketVisual.SocketHeight + ComponentPadding;
                curInputH += SocketVisual.SocketHeight + ComponentPadding;
            }

            foreach (ParameterInfo input in GetInputs())
            {
                SocketVisual socket = new SocketVisual();
                socket.Type = input.ParameterType;
                socket.Height = SocketVisual.SocketHeight;
                socket.Name = input.Name;
                socket.Width = SocketVisual.SocketHeight;
                socket.X = X;
                socket.Y = Y + curInputH;
                socket.Input = true;
                
                // Use runtime type if available for dynamic sockets
                if (socketTypeInfo.ContainsKey(input.Name))
                {
                    socket.RuntimeType = socketTypeInfo[input.Name].RuntimeType;
                }
                else
                {
                    socket.RuntimeType = socket.Type;
                }

                socketList.Add(socket);

                curInputH += SocketVisual.SocketHeight + ComponentPadding;
            }
            DynamicNodeContext ctx = GetNodeContext();
            foreach (ParameterInfo output in GetOutputs())
            {
                SocketVisual socket = new SocketVisual();
                socket.Type = output.ParameterType;
                socket.Height = SocketVisual.SocketHeight;
                socket.Name = output.Name;
                socket.Width = SocketVisual.SocketHeight;
                socket.X = X + NodeWidth - SocketVisual.SocketHeight;
                socket.Y = Y + curOutputH;
                socket.Value = ctx[socket.Name];
                
                // Use runtime type if available for dynamic sockets
                if (socketTypeInfo.ContainsKey(output.Name))
                {
                    socket.RuntimeType = socketTypeInfo[output.Name].RuntimeType;
                }
                else
                {
                    socket.RuntimeType = socket.Type;
                }
                
                socketList.Add(socket);

                curOutputH += SocketVisual.SocketHeight + ComponentPadding;
            }

            socketCache = socketList.ToArray();
            return socketCache;
        }

        internal void DiscardCache()
        {
            socketCache = null;
        }

        /// <summary>
        /// Updates socket positions when the node moves without recalculating everything
        /// </summary>
        internal void UpdateSocketPositions(float dx, float dy)
        {
            if (socketCache != null)
            {
                foreach (var socket in socketCache)
                {
                    socket.X += dx;
                    socket.Y += dy;
                }
            }
        }

        /// <summary>
        /// Returns node context which is dynamic type. It will contain all node default input/output properties.
        /// </summary>
        public DynamicNodeContext GetNodeContext()
        {
            if (nodeContext == null)
            {
                DynamicNodeContext context = new DynamicNodeContext();

                foreach (ParameterInfo input in GetInputs())
                {
                    string contextName = input.Name.Replace(" ", "");
                    Type paramType = input.ParameterType;
                    
                    // Handle ref/out parameters by getting the underlying type
                    if (paramType.IsByRef)
                    {
                        paramType = paramType.GetElementType();
                    }
                    
                    context[contextName] = CreateDefaultInstance(paramType);
                }
                foreach (ParameterInfo output in GetOutputs())
                {
                    var contextName = output.Name.Replace(" ", "");
                    Type paramType = output.ParameterType;
                    
                    // Handle ref/out parameters by getting the underlying type
                    if (paramType.IsByRef)
                    {
                        paramType = paramType.GetElementType();
                    }
                    
                    context[contextName] = CreateDefaultInstance(paramType);
                }

                nodeContext = context;
            }
            return nodeContext;
        }
        
        /// <summary>
        /// Creates a default instance of the specified type, handling special cases like arrays, strings, and value types.
        /// </summary>
        private object CreateDefaultInstance(Type type)
        {
            // Handle string type
            if (type == typeof(string))
            {
                return string.Empty;
            }

            // Handle array types
            if (type.IsArray)
            {
                // Create an empty array of the appropriate type
                return Array.CreateInstance(type.GetElementType(), 0);
            }
            
            // Handle generic collection types (List<T>, IList<T>, IEnumerable<T>, etc.)
            if (type.IsGenericType)
            {
                Type genericTypeDef = type.GetGenericTypeDefinition();
                Type[] genericArgs = type.GetGenericArguments();
                
                // Handle IEnumerable<T>, IList<T>, ICollection<T> interfaces
                if (genericTypeDef == typeof(IEnumerable<>) || 
                    genericTypeDef == typeof(IList<>) || 
                    genericTypeDef == typeof(ICollection<>))
                {
                    // Create a List<T> for interface types
                    Type listType = typeof(List<>).MakeGenericType(genericArgs);
                    return Activator.CreateInstance(listType);
                }
                
                // Handle Dictionary<TKey, TValue> and IDictionary<TKey, TValue>
                if (genericTypeDef == typeof(IDictionary<,>) || 
                    genericTypeDef == typeof(Dictionary<,>))
                {
                    Type dictType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
                    return Activator.CreateInstance(dictType);
                }
                
                // Handle HashSet<T> and ISet<T>
                if (genericTypeDef == typeof(ISet<>) || 
                    genericTypeDef == typeof(HashSet<>))
                {
                    Type hashSetType = typeof(HashSet<>).MakeGenericType(genericArgs);
                    return Activator.CreateInstance(hashSetType);
                }
                
                // Try to create instance for other generic types
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch
                {
                    // If it's a generic interface or abstract class, try to create a List<T> as fallback
                    if (type.IsInterface || type.IsAbstract)
                    {
                        if (genericArgs.Length == 1)
                        {
                            Type listType = typeof(List<>).MakeGenericType(genericArgs);
                            return Activator.CreateInstance(listType);
                        }
                    }
                    return null;
                }
            }
            
            // Handle non-generic collection interfaces
            if (type == typeof(System.Collections.IEnumerable) || 
                type == typeof(System.Collections.IList) || 
                type == typeof(System.Collections.ICollection))
            {
                return new System.Collections.ArrayList();
            }
            
            // Handle value types (int, float, structs, etc.)
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            
            // Handle reference types with parameterless constructor
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                // Return null for types without parameterless constructor
                return null;
            }
        }

        internal ParameterInfo[] GetInputs()
        {
            return Type.GetParameters().Where(x => !x.IsOut).ToArray();
        }

        internal ParameterInfo[] GetOutputs()
        {
            return Type.GetParameters().Where(x => x.IsOut).ToArray();
        }

        /// <summary>
        /// Converts a camelCase or PascalCase string to Title Case with spaces
        /// </summary>
        private string ToTitleCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Insert spaces before uppercase letters (except the first one)
            var result = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    result.Append(' ');
                }
                result.Append(name[i]);
            }

            // Ensure first letter is uppercase
            if (result.Length > 0 && char.IsLower(result[0]))
            {
                result[0] = char.ToUpper(result[0]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Calculates the automatic width based on socket names
        /// </summary>
        private float CalculateAutoWidth()
        {
            float minWidth = NodeWidth; // Minimum width
            float padding = 20; // Padding between input and output names
            float socketPadding = 4; // Padding from socket to text
            float edgePadding = 5; // Padding from edge of node

            // If we have cached sockets, use their cached display names
            if (socketCache != null)
            {
                float maxInputWidth = 0;
                float maxOutputWidth = 0;

                foreach (var socket in socketCache)
                {
                    SizeF textSize = TextRenderer.MeasureText(socket.DisplayName, SystemFonts.SmallCaptionFont);
                    if (socket.Input)
                    {
                        maxInputWidth = Math.Max(maxInputWidth, textSize.Width);
                    }
                    else
                    {
                        maxOutputWidth = Math.Max(maxOutputWidth, textSize.Width);
                    }
                }

                // Calculate width for node name
                SizeF nameSize = TextRenderer.MeasureText(Name, SystemFonts.DefaultFont);
                float nameWidth = nameSize.Width + 10; // Add some padding for the name

                // Calculate total width needed
                float socketBasedWidth = SocketVisual.SocketHeight + socketPadding + maxInputWidth + padding +
                                          maxOutputWidth + socketPadding + SocketVisual.SocketHeight + edgePadding * 2;

                // Return the maximum of minimum width, name width, and socket-based width
                return Math.Max(Math.Max(minWidth, nameWidth), socketBasedWidth);
            }
            else
            {
                // Fallback: calculate without caching (initial calculation)
                float maxInputWidth = 0;
                ParameterInfo[] inputs = GetInputs();
                foreach (ParameterInfo input in inputs)
                {
                    string displayName = ToTitleCase(input.Name);
                    SizeF textSize = TextRenderer.MeasureText(displayName, SystemFonts.SmallCaptionFont);
                    maxInputWidth = Math.Max(maxInputWidth, textSize.Width);
                }

                // Add execution input if callable
                if (Callable && !ExecInit)
                {
                    SizeF textSize = TextRenderer.MeasureText("Enter", SystemFonts.SmallCaptionFont);
                    maxInputWidth = Math.Max(maxInputWidth, textSize.Width);
                }

                // Calculate maximum width needed for output socket names
                float maxOutputWidth = 0;
                ParameterInfo[] outputs = GetOutputs();
                foreach (ParameterInfo output in outputs)
                {
                    string displayName = ToTitleCase(output.Name);
                    SizeF textSize = TextRenderer.MeasureText(displayName, SystemFonts.SmallCaptionFont);
                    maxOutputWidth = Math.Max(maxOutputWidth, textSize.Width);
                }

                // Add execution output if callable
                if (Callable)
                {
                    SizeF textSize = TextRenderer.MeasureText("Exit", SystemFonts.SmallCaptionFont);
                    maxOutputWidth = Math.Max(maxOutputWidth, textSize.Width);
                }

                // Calculate width for node name
                SizeF nameSize = TextRenderer.MeasureText(Name, SystemFonts.DefaultFont);
                float nameWidth = nameSize.Width + 10; // Add some padding for the name

                // Calculate total width needed
                // Socket width + socket padding + text width + center padding + text width + socket padding + socket width
                float socketBasedWidth = SocketVisual.SocketHeight + socketPadding + maxInputWidth + padding +
                                          maxOutputWidth + socketPadding + SocketVisual.SocketHeight + edgePadding * 2;

                // Return the maximum of minimum width, name width, and socket-based width
                return Math.Max(Math.Max(minWidth, nameWidth), socketBasedWidth);
            }
        }

        /// <summary>
        /// Returns current size of the node.
        /// </summary>
        public SizeF GetNodeBounds()
        {
            SizeF csize = new SizeF();
            if (CustomEditor != null)
            {
                csize = new SizeF(CustomEditor.ClientSize.Width + 2 + 80 +SocketVisual.SocketHeight*2,
                    CustomEditor.ClientSize.Height + HeaderHeight + 8);
            }

            int inputs = GetInputs().Length;
            int outputs = GetOutputs().Length;
            if (Callable)
            {
                inputs++;
                outputs++;
            }
            float h = HeaderHeight + Math.Max(inputs*(SocketVisual.SocketHeight + ComponentPadding),
                outputs*(SocketVisual.SocketHeight + ComponentPadding)) + ComponentPadding*2f;

            // Use automatic width if no custom width is specified
            float width;
            if (CustomWidth >= 0)
            {
                width = CustomWidth;
            }
            else
            {
                // Calculate automatic width based on socket names
                float autoWidth = CalculateAutoWidth();
                width = Math.Max(csize.Width, autoWidth);
            }

            // Use custom height if specified, otherwise use calculated height
            float height;
            if (CustomHeight >= 0)
            {
                height = CustomHeight;
            }
            else
            {
                height = Math.Max(csize.Height, h);
            }

            return new SizeF(width, height);
        }

        /// <summary>
        /// Returns current size of node caption (header belt).
        /// </summary>
        /// <returns></returns>
        public SizeF GetHeaderSize()
        {
            return new SizeF(GetNodeBounds().Width, HeaderHeight);
        }

        /// <summary>
        /// Allows node to be drawn on given Graphics context.       
        /// </summary>
        /// <param name="g">Graphics context.</param>
        /// <param name="mouseLocation">Location of the mouse relative to NodesControl instance.</param>
        /// <param name="mouseButtons">Mouse buttons that are pressed while drawing node.</param>
        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            var rect = new RectangleF(new PointF(X,Y), GetNodeBounds());

            var feedrect = rect;
            feedrect.Inflate(10, 10);

            if (Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(Color.Yellow, 4), Rectangle.Round(feedrect));
            }
            else if (Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(Color.Red, 5), Rectangle.Round(feedrect));
            }

            var caption = new RectangleF(new PointF(X,Y), GetHeaderSize());
            bool mouseHoverCaption = caption.Contains(mouseLocation);

            g.FillRectangle(new SolidBrush(NodeColor), rect);

            if (IsSelected)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(180,Color.WhiteSmoke)), rect);
                g.FillRectangle(mouseHoverCaption ? Brushes.Gold : Brushes.Goldenrod, caption);
            }
            else
            {                
                g.FillRectangle(mouseHoverCaption ? Brushes.Cyan : Brushes.Aquamarine, caption);
            }
            g.DrawRectangle(Pens.Gray, Rectangle.Round(caption));
            g.DrawRectangle(Pens.Black, Rectangle.Round(rect));

            g.DrawString(Name, SystemFonts.DefaultFont, Brushes.Black, new PointF(X + 3, Y + 3));       

            var sockets = GetSockets();
            foreach (var socet in sockets)
            {
                socet.Draw(g, mouseLocation, mouseButtons);
            }
        }

        internal void Execute(INodesContext context)
        {
            context.CurrentProcessingNode = this;

            DynamicNodeContext dc = GetNodeContext();
            ParameterInfo[] methodParams = Type.GetParameters().OrderBy(x => x.Position).ToArray();
            Dictionary<string, object> parametersDict = new Dictionary<string, object>();
            object[] parameters = new object[methodParams.Length];
            
            // Convert parameters to the expected types
            for (int i = 0; i < methodParams.Length; i++)
            {
                ParameterInfo param = methodParams[i];
                object value = dc[param.Name];
                Type expectedType = param.ParameterType;
                
                // Handle ref/out parameters
                if (expectedType.IsByRef)
                {
                    expectedType = expectedType.GetElementType();
                }
                
                // Convert the value if necessary using the shared type conversion logic
                object convertedValue = TypePropagation.ConvertValue(value, expectedType);
                
                parametersDict[param.Name] = convertedValue;
                parameters[i] = convertedValue;
            }

            int ndx = 0;
            Type.Invoke(context, parameters);
            foreach (KeyValuePair<string, object> kv in parametersDict.ToArray())
            {
                parametersDict[kv.Key] = parameters[ndx];
                ndx++;
            }

            var outs = GetSockets();

            
            foreach (var parameter in dc.ToArray())
            {
                dc[parameter] = parametersDict[parameter];
                var o = outs.FirstOrDefault(x => x.Name == parameter);
                //if (o != null)
                Debug.Assert(o != null, "Output not found");
                {
                    o.Value = dc[parameter];
                }                                
            }
        }

        internal void LayoutEditor()
        {
            if (CustomEditor != null)
            {
                CustomEditor.Location = new Point((int)( X + 1 + 40 + SocketVisual.SocketHeight), (int) (Y + HeaderHeight + 4));
            }
        }
        
        /// <summary>
        /// Updates the runtime type for a socket based on connected input
        /// </summary>
        internal void UpdateSocketType(string socketName, Type newType)
        {
            if (!socketTypeInfo.ContainsKey(socketName))
            {
                // Initialize type info if not present
                SocketVisual socket = GetSockets().FirstOrDefault(s => s.Name == socketName);
                if (socket != null)
                {
                    socketTypeInfo[socketName] = new SocketTypeInfo(socket.Type);
                }
            }
            
            if (socketTypeInfo.ContainsKey(socketName))
            {
                socketTypeInfo[socketName].RuntimeType = newType;
                DiscardCache(); // Force socket refresh
            }
        }
        
        /// <summary>
        /// Gets the runtime type for a socket
        /// </summary>
        internal Type GetSocketRuntimeType(string socketName)
        {
            if (socketTypeInfo.ContainsKey(socketName))
            {
                return socketTypeInfo[socketName].RuntimeType;
            }
            
            SocketVisual socket = GetSockets().FirstOrDefault(s => s.Name == socketName);
            return socket?.Type;
        }
        
        /// <summary>
        /// Propagates type information through the node using attribute-based system
        /// </summary>
        internal void PropagateTypes(NodesGraph graph)
        {
            TypePropagation.PropagateNodeTypes(this, graph);
        }
        
        /// <summary>
        /// Resets socket types to their original static types
        /// </summary>
        internal void ResetSocketTypes()
        {
            foreach (SocketVisual socket in GetSockets())
            {
                if (socketTypeInfo.ContainsKey(socket.Name))
                {
                    socketTypeInfo[socket.Name].RuntimeType = socketTypeInfo[socket.Name].StaticType;
                }
            }
            DiscardCache();
        }
        
        /// <summary>
        /// Checks if this node has dynamic type support
        /// </summary>
        internal bool HasDynamicTypeSupport()
        {
            return TypePropagation.IsDynamicNode(this);
        }
    }
}
