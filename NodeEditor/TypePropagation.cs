using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NodeEditor
{
    /// <summary>
    /// Handles dynamic type propagation for generic nodes in the node editor
    /// </summary>
    internal static class TypePropagation
    {
        /// <summary>
        /// Gets parameter info with dynamic type attributes for a node
        /// </summary>
        public static Dictionary<string, DynamicTypeAttribute> GetDynamicParameters(NodeVisual node)
        {
            Dictionary<string, DynamicTypeAttribute> result = new Dictionary<string, DynamicTypeAttribute>();
            
            if (node?.Type == null) return result;
            
            foreach (ParameterInfo param in node.Type.GetParameters())
            {
                DynamicTypeAttribute dynamicAttr = param.GetCustomAttribute<DynamicTypeAttribute>();
                if (dynamicAttr != null)
                {
                    result[param.Name] = dynamicAttr;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Checks if a node supports dynamic typing
        /// </summary>
        public static bool IsDynamicNode(NodeVisual node)
        {
            if (node?.Type == null) return false;
            return node.Type.GetCustomAttribute<DynamicNodeAttribute>() != null;
        }
        
        /// <summary>
        /// Propagates types through a node based on its dynamic type attributes
        /// </summary>
        public static void PropagateNodeTypes(NodeVisual node, NodesGraph graph)
        {
            if (!IsDynamicNode(node)) return;
            
            Dictionary<string, DynamicTypeAttribute> dynamicParams = GetDynamicParameters(node);
            if (dynamicParams.Count == 0) return;
            
            // Group parameters by TypeGroup
            Dictionary<string, Type> typeGroups = new Dictionary<string, Type>();
            
            // First pass: determine types from inputs
            foreach (KeyValuePair<string, DynamicTypeAttribute> param in dynamicParams)
            {
                NodeConnection connection = graph.Connections.FirstOrDefault(
                    c => c.InputNode == node && c.InputSocketName == param.Key);
                    
                if (connection != null)
                {
                    Type inferredType = ResolveTypeRecursively(connection, graph);
                    if (inferredType == null)
                        inferredType = connection.OutputSocket.Type;
                    
                    // For type propagation, use the actual source type, not the converted type
                    // This preserves type information for ExtractElementType operations
                    // For example, double[] should remain double[] for type propagation,
                    // even though it converts to IEnumerable<object> at runtime
                    
                    // Store the inferred type for this parameter's group
                    if (!string.IsNullOrEmpty(param.Value.TypeGroup))
                    {
                        typeGroups[param.Value.TypeGroup] = inferredType;
                    }
                }
            }
            
            // Second pass: apply types to all parameters based on attributes
            foreach (KeyValuePair<string, DynamicTypeAttribute> param in dynamicParams)
            {
                Type targetType = null;
                
                // If this parameter derives from another
                if (!string.IsNullOrEmpty(param.Value.DerivedFrom))
                {
                    // First check if it's deriving from a type group output
                    if (!string.IsNullOrEmpty(param.Value.TypeGroup) && typeGroups.ContainsKey(param.Value.TypeGroup))
                    {
                        // Use the type group value
                        targetType = typeGroups[param.Value.TypeGroup];
                        
                        if (param.Value.ExtractElementType)
                        {
                            targetType = GetElementType(targetType);
                        }
                        else if (param.Value.WrapInCollection)
                        {
                            targetType = CreateTypedCollectionType(targetType, typeof(List<object>));
                        }
                    }
                    else
                    {
                        // Find the source parameter's type
                        NodeConnection sourceConnection = graph.Connections.FirstOrDefault(
                            c => c.InputNode == node && c.InputSocketName == param.Value.DerivedFrom);
                        
                        if (sourceConnection != null)
                        {
                            targetType = ResolveTypeRecursively(sourceConnection, graph) ?? sourceConnection.OutputSocket.Type;
                            
                            if (param.Value.ExtractElementType)
                            {
                                targetType = GetElementType(targetType);
                            }
                            else if (param.Value.WrapInCollection)
                            {
                                targetType = CreateTypedCollectionType(targetType, typeof(List<object>));
                            }
                        }
                        else
                        {
                            // No connection found for the source parameter, use default type
                            ParameterInfo paramInfo = node.Type.GetParameters().FirstOrDefault(p => p.Name == param.Key);
                            if (paramInfo != null)
                            {
                                targetType = paramInfo.ParameterType;
                                if (targetType.IsByRef)
                                    targetType = targetType.GetElementType();
                            }
                        }
                    }
                }
                // Otherwise use the type group
                else if (!string.IsNullOrEmpty(param.Value.TypeGroup) && typeGroups.ContainsKey(param.Value.TypeGroup))
                {
                    targetType = typeGroups[param.Value.TypeGroup];
                    
                    if (param.Value.ExtractElementType)
                    {
                        targetType = GetElementType(targetType);
                    }
                    else if (param.Value.WrapInCollection)
                    {
                        targetType = CreateTypedCollectionType(targetType, typeof(List<object>));
                    }
                }
                else if (!string.IsNullOrEmpty(param.Value.TypeGroup))
                {
                    // Type group exists but no type found, reset to default
                    ParameterInfo paramInfo = node.Type.GetParameters().FirstOrDefault(p => p.Name == param.Key);
                    if (paramInfo != null)
                    {
                        targetType = paramInfo.ParameterType;
                        if (targetType.IsByRef)
                            targetType = targetType.GetElementType();
                    }
                }
                
                // Update the socket's runtime type
                if (targetType != null)
                {
                    node.UpdateSocketType(param.Key, targetType);
                }
            }
        }
        
        /// <summary>
        /// Checks if a socket has dynamic type support
        /// </summary>
        public static bool IsDynamicSocket(NodeVisual node, string socketName)
        {
            if (node?.Type == null) return false;
            
            ParameterInfo param = node.Type.GetParameters().FirstOrDefault(p => p.Name == socketName);
            if (param == null) return false;
            
            return param.GetCustomAttribute<DynamicTypeAttribute>() != null;
        }
        
        /// <summary>
        /// Checks if a socket is generic (accepts/outputs object or IEnumerable of object)
        /// </summary>
        public static bool IsGenericSocket(SocketVisual socket)
        {
            if (socket == null || socket.Type == null) return false;
            
            Type type = socket.Type;
            
            // Handle ref/out parameters
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            
            // Check for object type
            if (type == typeof(object))
            {
                return true;
            }
            
            // Check for IEnumerable<object>
            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                Type[] genericArgs = type.GetGenericArguments();
                
                if ((genericDef == typeof(IEnumerable<>) || 
                     genericDef == typeof(IList<>) || 
                     genericDef == typeof(List<>)) &&
                    genericArgs.Length == 1 && 
                    genericArgs[0] == typeof(object))
                {
                    return true;
                }
            }
            
            // Check for non-generic IEnumerable
            if (type == typeof(IEnumerable))
            {
                return true;
            }
            
            // Check for object array
            if (type.IsArray && type.GetElementType() == typeof(object))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Recursively resolves the concrete type through chains of dynamic nodes
        /// </summary>
        private static Type ResolveTypeRecursively(NodeConnection connection, NodesGraph graph, HashSet<NodeVisual> visited = null)
        {
            if (connection == null || connection.OutputSocket == null) 
                return null;
                
            // Prevent infinite recursion
            if (visited == null)
                visited = new HashSet<NodeVisual>();
            if (visited.Contains(connection.OutputNode))
                return connection.OutputSocket.Type;
            visited.Add(connection.OutputNode);
            
            // If the output socket has a runtime type set, use it
            Type runtimeType = connection.OutputNode.GetSocketRuntimeType(connection.OutputSocketName);
            if (runtimeType != null && runtimeType != typeof(object))
            {
                return runtimeType;
            }
            
            // If this is a dynamic node with generic output, trace back further
            if (IsDynamicNode(connection.OutputNode) && IsGenericSocket(connection.OutputSocket))
            {
                // Find the input connections to this node
                Dictionary<string, DynamicTypeAttribute> outputNodeDynamicParams = GetDynamicParameters(connection.OutputNode);
                
                // Look for the parameter that corresponds to this output
                foreach (KeyValuePair<string, DynamicTypeAttribute> param in outputNodeDynamicParams)
                {
                    if (param.Key == connection.OutputSocketName)
                    {
                        // If this output derives from an input, trace that input
                        if (!string.IsNullOrEmpty(param.Value.DerivedFrom))
                        {
                            NodeConnection upstreamConnection = graph.Connections.FirstOrDefault(
                                c => c.InputNode == connection.OutputNode && c.InputSocketName == param.Value.DerivedFrom);
                            
                            if (upstreamConnection != null)
                            {
                                Type upstreamType = ResolveTypeRecursively(upstreamConnection, graph, visited);
                                
                                // Apply transformations if needed
                                if (param.Value.ExtractElementType)
                                {
                                    upstreamType = GetElementType(upstreamType);
                                }
                                else if (param.Value.WrapInCollection)
                                {
                                    upstreamType = CreateTypedCollectionType(upstreamType, typeof(List<object>));
                                }
                                
                                return upstreamType;
                            }
                        }
                        // If it's part of a type group, find inputs with the same type group
                        else if (!string.IsNullOrEmpty(param.Value.TypeGroup))
                        {
                            foreach (KeyValuePair<string, DynamicTypeAttribute> inputParam in outputNodeDynamicParams)
                            {
                                if (inputParam.Value.TypeGroup == param.Value.TypeGroup && inputParam.Key != param.Key)
                                {
                                    NodeConnection upstreamConnection = graph.Connections.FirstOrDefault(
                                        c => c.InputNode == connection.OutputNode && c.InputSocketName == inputParam.Key);
                                    
                                    if (upstreamConnection != null)
                                    {
                                        Type upstreamType = ResolveTypeRecursively(upstreamConnection, graph, visited);
                                        
                                        // Apply transformations from the output parameter
                                        if (param.Value.ExtractElementType)
                                        {
                                            upstreamType = GetElementType(upstreamType);
                                        }
                                        else if (param.Value.WrapInCollection)
                                        {
                                            upstreamType = CreateTypedCollectionType(upstreamType, typeof(List<object>));
                                        }
                                        
                                        return upstreamType;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return connection.OutputSocket.Type;
        }
        
        /// <summary>
        /// Gets the actual runtime type from a connection's source
        /// </summary>
        public static Type GetActualType(NodeConnection connection)
        {
            if (connection == null || connection.OutputSocket == null) 
                return null;
                
            DynamicNodeContext outputContext = connection.OutputNode.GetNodeContext();
            object value = outputContext[connection.OutputSocketName];
            
            if (value != null)
            {
                return value.GetType();
            }
            
            // If the output socket has a runtime type set (from type propagation), use it
            Type runtimeType = connection.OutputNode.GetSocketRuntimeType(connection.OutputSocketName);
            if (runtimeType != null && runtimeType != typeof(object))
            {
                return runtimeType;
            }
            
            return connection.OutputSocket.Type;
        }
        
        /// <summary>
        /// Infers the element type from a collection type
        /// </summary>
        public static Type GetElementType(Type collectionType)
        {
            if (collectionType == null) return typeof(object);
            
            if (collectionType.IsByRef)
            {
                collectionType = collectionType.GetElementType();
            }

            // Handle arrays
            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }
            
            // Handle generic collections
            if (collectionType.IsGenericType)
            {
                Type genericDef = collectionType.GetGenericTypeDefinition();
                Type[] genericArgs = collectionType.GetGenericArguments();
                
                if ((genericDef == typeof(IEnumerable<>) || 
                     genericDef == typeof(IList<>) || 
                     genericDef == typeof(List<>) ||
                     genericDef == typeof(ICollection<>)) &&
                    genericArgs.Length == 1)
                {
                    return genericArgs[0];
                }
            }
            
            // Try to find IEnumerable<T> in interfaces
            foreach (Type interfaceType in collectionType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && 
                    interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return interfaceType.GetGenericArguments()[0];
                }
            }
            
            return typeof(object);
        }
        
        /// <summary>
        /// Creates a properly typed collection for a given element type
        /// </summary>
        public static Type CreateTypedCollectionType(Type elementType, Type templateType)
        {
            if (elementType == null) elementType = typeof(object);
            
            if (elementType.IsByRef)
            {
                elementType = elementType.GetElementType();
            }

            // Always use List<T> for consistency
            // This avoids array covariance issues and simplifies type handling
            return typeof(List<>).MakeGenericType(elementType);
        }
        
        /// <summary>
        /// Converts a value to the expected type for method invocation or data flow
        /// </summary>
        public static object ConvertValue(object value, Type expectedType)
        {
            // If value is null, return null (or default for value types)
            if (value == null)
            {
                return expectedType.IsValueType ? Activator.CreateInstance(expectedType) : null;
            }
            
            Type actualType = value.GetType();
            
            // If types already match, no conversion needed
            if (expectedType.IsAssignableFrom(actualType))
            {
                return value;
            }
            
            // Always convert arrays to Lists for consistency
            if (actualType.IsArray && IsCollectionType(expectedType))
            {
                // Convert array to List
                Type elementType = actualType.GetElementType();
                Type listType = typeof(List<>).MakeGenericType(elementType);
                System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);
                
                foreach (object item in (Array)value)
                {
                    list.Add(item);
                }
                
                // Now convert the List to the expected type if needed
                return ConvertCollection(list, listType, expectedType);
            }
            
            // Handle collection type conversions
            if (IsCollectionType(expectedType) && IsCollectionType(actualType))
            {
                return ConvertCollection(value, actualType, expectedType);
            }
            
            // Try standard type conversion
            try
            {
                if (expectedType.IsEnum && value is string stringValue)
                {
                    return Enum.Parse(expectedType, stringValue);
                }
                
                if (typeof(IConvertible).IsAssignableFrom(actualType) && typeof(IConvertible).IsAssignableFrom(expectedType))
                {
                    return Convert.ChangeType(value, expectedType);
                }
            }
            catch
            {
                // Conversion failed, return original value and let caller handle it
            }
            
            // If no conversion worked, return the original value
            // The caller might still work with implicit conversions
            return value;
        }
        
        /// <summary>
        /// Converts between collection types
        /// </summary>
        private static object ConvertCollection(object value, Type actualType, Type expectedType)
        {
            // Special handling for IEnumerable<T>
            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                Type expectedElementType = expectedType.GetGenericArguments()[0];
                
                // If expecting IEnumerable<object>, we can return the original collection
                // The framework will handle boxing during iteration
                if (expectedElementType == typeof(object) && value is IEnumerable)
                {
                    // Return the original collection to preserve type information
                    // Boxing will happen automatically during enumeration
                    return value;
                }
                
                // Try to convert to the expected IEnumerable type
                if (value is IEnumerable enumerable)
                {
                    // Create a List<T> of the expected element type
                    Type listType = typeof(List<>).MakeGenericType(expectedElementType);
                    System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);
                    
                    foreach (object item in enumerable)
                    {
                        // Recursively convert each item if needed
                        object convertedItem = ConvertValue(item, expectedElementType);
                        list.Add(convertedItem);
                    }
                    
                    return list;
                }
            }
            
            // Convert arrays to Lists to maintain consistency
            // Even if expectedType is an array, we return a List
            if (expectedType.IsArray && value is IEnumerable)
            {
                Type elementType = expectedType.GetElementType();
                Type listType = typeof(List<>).MakeGenericType(elementType);
                System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);
                
                foreach (object item in (IEnumerable)value)
                {
                    list.Add(ConvertValue(item, elementType));
                }
                
                // Return List instead of array - maintain List purity!
                return list;
            }
            
            // Handle List<T> conversions
            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type expectedElementType = expectedType.GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(expectedElementType);
                System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);
                
                if (value is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        object convertedItem = ConvertValue(item, expectedElementType);
                        list.Add(convertedItem);
                    }
                }
                
                return list;
            }
            
            // If we can't convert, return the original value
            return value;
        }
        
        /// <summary>
        /// Checks if two types are compatible for connection
        /// </summary>
        public static bool AreTypesCompatible(Type sourceType, Type targetType)
        {
            if (sourceType == null || targetType == null) return false;
            
            // Handle ref/out parameters
            if (sourceType.IsByRef) sourceType = sourceType.GetElementType();
            if (targetType.IsByRef) targetType = targetType.GetElementType();
            
            // Exact match
            if (sourceType == targetType) return true;
            
            // Check if target can accept source (inheritance/interface)
            if (targetType.IsAssignableFrom(sourceType)) return true;
            
            // Special handling for collections
            if (IsCollectionType(sourceType) && IsCollectionType(targetType))
            {
                Type sourceElement = GetElementType(sourceType);
                Type targetElement = GetElementType(targetType);
                
                // Allow connection if element types are compatible
                // This includes object accepting any type (including value types that can be boxed)
                if (targetElement == typeof(object))
                {
                    return true; // Any type can be boxed to object
                }
                
                // Check standard type compatibility
                return targetElement.IsAssignableFrom(sourceElement) ||
                       sourceElement.IsSubclassOf(targetElement) ||
                       (targetElement.IsInterface && targetElement.IsAssignableFrom(sourceElement));
            }
            
            // Also allow connecting a collection to IEnumerable or IEnumerable<T>
            if (IsCollectionType(sourceType) && 
                (targetType == typeof(IEnumerable) || 
                 (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
            {
                if (targetType == typeof(IEnumerable))
                    return true;
                    
                Type sourceElement = GetElementType(sourceType);
                Type targetElement = targetType.GetGenericArguments()[0];
                
                // Allow any type to be converted to object (including value types via boxing)
                if (targetElement == typeof(object))
                    return true;
                    
                return targetElement.IsAssignableFrom(sourceElement);
            }
            
            return false;
        }
        
        public static bool IsCollectionType(Type type)
        {
            // Simplified - we primarily care about Lists now
            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(List<>) ||
                       genericDef == typeof(IList<>) ||
                       genericDef == typeof(IEnumerable<>) ||
                       genericDef == typeof(ICollection<>);
            }
            
            // Still support arrays for backward compatibility
            if (type.IsArray) return true;
            if (type == typeof(IEnumerable)) return true;
            
            return typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
    
    /// <summary>
    /// Stores runtime type information for a socket
    /// </summary>
    internal class SocketTypeInfo
    {
        public Type StaticType { get; set; }  // The type declared in code
        public Type RuntimeType { get; set; }  // The actual type at runtime
        public bool IsGeneric { get; set; }    // Whether this socket accepts generic types
        
        public SocketTypeInfo(Type staticType)
        {
            StaticType = staticType;
            RuntimeType = staticType;
            IsGeneric = TypePropagation.IsGenericSocket(new SocketVisual { Type = staticType });
        }
    }
}