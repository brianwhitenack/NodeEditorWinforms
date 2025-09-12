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
                    Type inferredType = GetActualType(connection);
                    if (inferredType == null)
                        inferredType = connection.OutputSocket.Type;
                    
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
                            targetType = GetActualType(sourceConnection) ?? sourceConnection.OutputSocket.Type;
                            
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
            
            return connection.OutputSocket.Type;
        }
        
        /// <summary>
        /// Infers the element type from a collection type
        /// </summary>
        public static Type GetElementType(Type collectionType)
        {
            if (collectionType == null) return typeof(object);
            
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
            
            // If template is an array
            if (templateType.IsArray)
            {
                return elementType.MakeArrayType();
            }
            
            // If template is a generic type
            if (templateType.IsGenericType)
            {
                Type genericDef = templateType.GetGenericTypeDefinition();
                
                if (genericDef == typeof(IEnumerable<>) || 
                    genericDef == typeof(IList<>) || 
                    genericDef == typeof(List<>) ||
                    genericDef == typeof(ICollection<>))
                {
                    return genericDef.MakeGenericType(elementType);
                }
            }
            
            // Default to List<T>
            return typeof(List<>).MakeGenericType(elementType);
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
                return targetElement == typeof(object) || 
                       targetElement.IsAssignableFrom(sourceElement);
            }
            
            return false;
        }
        
        private static bool IsCollectionType(Type type)
        {
            if (type.IsArray) return true;
            if (type == typeof(IEnumerable)) return true;
            
            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(IEnumerable<>) ||
                       genericDef == typeof(IList<>) ||
                       genericDef == typeof(List<>) ||
                       genericDef == typeof(ICollection<>);
            }
            
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