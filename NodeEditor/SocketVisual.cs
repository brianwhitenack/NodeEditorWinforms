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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NodeEditor
{
    internal class SocketVisual
    {
        public const float SocketHeight = 16;

        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }
        public bool Input { get; set; }
        public object Value { get; set; }
        public bool IsMainExecution { get; set; }
        
        /// <summary>
        /// Runtime type for dynamic sockets (may differ from static Type)
        /// </summary>
        public Type RuntimeType { get; set; }

        public bool IsExecution
        {
            get { return Type.Name.Replace("&", "") == typeof (ExecutionPath).Name; }
        }

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {            
            RectangleF socketRect = new RectangleF(X, Y, Width, Height);
            bool hover = socketRect.Contains(mouseLocation);
            Brush fontBrush = Brushes.Black;

            if (hover)
            {
                socketRect.Inflate(4, 4);
                fontBrush = Brushes.Blue;
            }

            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.InterpolationMode = InterpolationMode.Low;
            
            if (Input)
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;                
                g.DrawString(Name,SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(X+Width+2,Y,1000,Height), sf);
            }
            else
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Name, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(X-1000, Y, 1000, Height), sf);
            }

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            if (IsExecution)
            {
                g.DrawImage(Resources.exec, socketRect);
            }
            else
            {
                // Draw socket with type-based color
                DrawColoredSocket(g, socketRect);
            }
        }
        
        private void DrawColoredSocket(Graphics g, RectangleF socketRect)
        {
            // Get the effective type (runtime type if available, otherwise static type)
            Type effectiveType = RuntimeType ?? Type;
            if (effectiveType == null) 
            {
                g.DrawImage(Resources.socket, socketRect);
                return;
            }
            
            // Handle ref/out types first before checking collection type
            if (effectiveType.IsByRef)
            {
                effectiveType = effectiveType.GetElementType();
            }
            
            // Check if it's a collection type
            bool isCollection = IsCollectionType(effectiveType);
            
            // Generate color from type (get base element type for collections)
            Color typeColor = GetTypeColor(effectiveType, isCollection);
            
            // Create a colored version of the socket
            using (Bitmap coloredSocket = new Bitmap((int)socketRect.Width, (int)socketRect.Height))
            {
                using (Graphics tempG = Graphics.FromImage(coloredSocket))
                {
                    tempG.SmoothingMode = SmoothingMode.AntiAlias;
                    
                    // Draw colored shape - square for collections, circle for single values
                    using (Brush brush = new SolidBrush(typeColor))
                    {
                        if (isCollection)
                        {
                            // Draw a rounded square for collections
                            tempG.FillRectangle(brush, 1, 1, coloredSocket.Width - 3, coloredSocket.Height - 3);
                        }
                        else
                        {
                            // Draw a circle for single values
                            tempG.FillEllipse(brush, 0, 0, coloredSocket.Width - 1, coloredSocket.Height - 1);
                        }
                    }
                    
                    // Draw a border
                    using (Pen pen = new Pen(Color.FromArgb(100, Color.Black), 1))
                    {
                        if (isCollection)
                        {
                            tempG.DrawRectangle(pen, 1, 1, coloredSocket.Width - 3, coloredSocket.Height - 3);
                        }
                        else
                        {
                            tempG.DrawEllipse(pen, 0, 0, coloredSocket.Width - 1, coloredSocket.Height - 1);
                        }
                    }
                }
                
                g.DrawImage(coloredSocket, socketRect);
            }
        }
        
        private bool IsCollectionType(Type type)
        {
            if (type.IsArray) return true;
            if (type == typeof(System.Collections.IEnumerable)) return true;
            
            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(IEnumerable<>) ||
                       genericDef == typeof(List<>) ||
                       genericDef == typeof(IList<>) ||
                       genericDef == typeof(ICollection<>);
            }
            
            return typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }
        
        private Color GetTypeColor(Type type, bool isCollection = false)
        {
            // For collections, get the element type color
            if (isCollection && type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(IEnumerable<>) || genericDef == typeof(List<>) || genericDef == typeof(IList<>) || genericDef == typeof(ICollection<>))
                {
                    Type elementType = type.GetGenericArguments()[0];
                    return GetTypeColor(elementType, false); // Get base element color
                }
            }
            
            // For arrays, get the element type color
            if (isCollection && type.IsArray)
            {
                Type elementType = type.GetElementType();
                return GetTypeColor(elementType, false);
            }
            
            // Special colors for common types
            if (type == typeof(string))
                return Color.FromArgb(255, 184, 107); // Orange
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return Color.FromArgb(107, 203, 119); // Green
            if (type == typeof(bool))
                return Color.FromArgb(255, 133, 133); // Red
            if (type == typeof(ExecutionPath))
                return Color.White;
            if (type == typeof(object))
                return Color.FromArgb(200, 200, 200); // Gray for generic object
            
            // For custom types, generate color from hash
            int hash = type.FullName.GetHashCode();
            
            // Use hash bits directly to generate HSL values
            // This gives us deterministic colors without Random
            float hue = (hash & 0xFFFF) / 65535f * 360f; // Lower 16 bits for hue
            float saturation = 0.6f + ((hash >> 16) & 0xFF) / 255f * 0.3f; // Next 8 bits for saturation (60-90%)
            float lightness = 0.5f + ((hash >> 24) & 0xFF) / 255f * 0.2f; // Next 8 bits for lightness (50-70%)
            
            return ColorFromHSL(hue, saturation, lightness);
        }
        
        private Color ColorFromHSL(float hue, float saturation, float lightness)
        {
            float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            float m = lightness - c / 2;
            
            float r = 0, g = 0, b = 0;
            
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            
            return Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255)
            );
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }
}
