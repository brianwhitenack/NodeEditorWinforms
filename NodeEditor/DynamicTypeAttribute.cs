using System;

namespace NodeEditor
{
    /// <summary>
    /// Marks a parameter as supporting dynamic type inference.
    /// When applied, the parameter's type can be inferred from connected inputs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class DynamicTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the input parameter this output derives its type from.
        /// If specified, this output will match the type (or element type for collections) of the specified input.
        /// </summary>
        public string DerivedFrom { get; set; }
        
        /// <summary>
        /// Gets or sets whether this parameter extracts the element type from a collection.
        /// When true, if the input is IEnumerable<T>, this parameter becomes type T.
        /// </summary>
        public bool ExtractElementType { get; set; }
        
        /// <summary>
        /// Gets or sets whether this parameter wraps the type in a collection.
        /// When true, if the input is type T, this parameter becomes IEnumerable<T>.
        /// </summary>
        public bool WrapInCollection { get; set; }
        
        /// <summary>
        /// Gets or sets the group name for type propagation.
        /// All parameters with the same group name will share the same inferred type.
        /// </summary>
        public string TypeGroup { get; set; }
        
        public DynamicTypeAttribute()
        {
        }
        
        public DynamicTypeAttribute(string derivedFrom)
        {
            DerivedFrom = derivedFrom;
        }
    }
    
    /// <summary>
    /// Marks a method (node) as supporting dynamic type inference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DynamicNodeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether this node should propagate types through all its connections.
        /// </summary>
        public bool PropagateTypes { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether incompatible connections should be automatically disconnected
        /// when types change.
        /// </summary>
        public bool AutoDisconnectIncompatible { get; set; } = true;
    }
}