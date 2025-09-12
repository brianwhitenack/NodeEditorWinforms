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
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NodeEditor
{
    /// <summary>
    /// Class used as internal context of each node.
    /// </summary>
    [TypeConverter(typeof(DynamicNodeContextConverter))]
    public class DynamicNodeContext : DynamicObject, IEnumerable<String>
    {
        private readonly IDictionary<string, object> dynamicProperties =
            new Dictionary<string, object>();

        internal byte[] Serialize()
        {
            using (BinaryWriter bw = new BinaryWriter(new MemoryStream()))
            {
                foreach (KeyValuePair<string, object> prop in dynamicProperties)
                {
                    if (prop.Value != null && prop.Value.GetType().IsSerializable)
                    {
                        using (var ps = new MemoryStream())
                        {
                            new BinaryFormatter().Serialize(ps, prop.Value);
                            bw.Write(prop.Key);
                            bw.Write((int)ps.Length);
                            bw.Write(ps.ToArray());
                        }
                    }
                }
                return (bw.BaseStream as MemoryStream).ToArray();
            }
        }

        internal void Deserialize(byte[] data)
        {
            dynamicProperties.Clear();
            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    var key = br.ReadString();
                    var propData = br.ReadBytes(br.ReadInt32());
                    using (var ms = new MemoryStream(propData))
                    {
                        var val = new BinaryFormatter().Deserialize(ms);
                        dynamicProperties.Add(key, val);
                    }
                }
            }
        }

        internal Dictionary<string, Serialization.ContextProperty> GetPropertiesForSerialization()
        {
            Dictionary<string, Serialization.ContextProperty> result = new Dictionary<string, Serialization.ContextProperty>();
            foreach (KeyValuePair<string, object> prop in dynamicProperties)
            {
                result[prop.Key] = new Serialization.ContextProperty
                {
                    Value = prop.Value,
                    Type = prop.Value?.GetType().FullName ?? "",
                    ActualType = prop.Value?.GetType().AssemblyQualifiedName ?? ""  // Store full type info for deserialization
                };
            }
            return result;
        }

        internal void SetPropertiesFromSerialization(Dictionary<string, Serialization.ContextProperty> properties, NodeVisual node)
        {
            dynamicProperties.Clear();

            foreach (KeyValuePair<string, Serialization.ContextProperty> prop in properties)
            {
                object propertyValue = prop.Value.Value;
                if (propertyValue == null)
                {
                    continue;
                }

                string propertyName = prop.Key;
                SocketVisual matchingSocket = node.GetSockets().Single(s => s.Name == propertyName);

                Type targetType;
                try
                {
                    targetType = Type.GetType(prop.Value.ActualType);
                }
                catch
                {
                    // Fall back to socket type if actual type can't be loaded
                    targetType = matchingSocket.Type;
                }

                // Handle reference types (ref/out parameters) - strip the & suffix
                if (targetType.IsByRef)
                {
                    targetType = targetType.GetElementType();
                }

                    // Convert JToken types to appropriate CLR types
                if (propertyValue is JToken jToken)
                {
                    if (targetType.IsInterface)
                    {
                        // Map common interfaces to concrete types
                        if (targetType.IsGenericType)
                        {
                            Type genericDef = targetType.GetGenericTypeDefinition();
                            if (genericDef == typeof(IEnumerable<>) ||
                                genericDef == typeof(IList<>) ||
                                genericDef == typeof(ICollection<>))
                            {
                                // Use List<T> for these interfaces
                                Type elementType = targetType.GetGenericArguments()[0];
                                targetType = typeof(List<>).MakeGenericType(elementType);
                            }
                            else if (genericDef == typeof(IDictionary<,>))
                            {
                                // Use Dictionary<K,V> for IDictionary
                                Type[] genericArgs = targetType.GetGenericArguments();
                                targetType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
                            }
                        }
                        else if (targetType == typeof(IEnumerable))
                        {
                            // Use object[] for non-generic IEnumerable
                            targetType = typeof(object[]);
                        }
                    }

                    propertyValue = jToken.ToObject(targetType);
                }
                // Handle numeric type conversions for primitive types
                else if (targetType.IsPrimitive)
                {
                    propertyValue = Convert.ChangeType(propertyValue, targetType);
                }
                
                dynamicProperties[propertyName] = propertyValue;
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string memberName = binder.Name;
            return dynamicProperties.TryGetValue(memberName, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            string memberName = binder.Name;
            dynamicProperties[memberName] = value;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return dynamicProperties.Keys;
        }

        public object this[string key]
        {
            get
            {
                if (!dynamicProperties.ContainsKey(key)) return null;
                return dynamicProperties[key];
            }
            set
            {
                if (!dynamicProperties.ContainsKey(key))
                {
                    dynamicProperties.Add(key, value);
                }
                else
                {
                    dynamicProperties[key] = value;
                }
            }
        }

        public IEnumerator<String> GetEnumerator()
        {
            return dynamicProperties.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
