using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NodeEditor.Serialization
{
    /// <summary>
    /// Root model for serializing the entire node graph
    /// </summary>
    public class NodeGraphModel
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1001;

        [JsonProperty("nodes")]
        public List<NodeModel> Nodes { get; set; } = new List<NodeModel>();

        [JsonProperty("connections")]
        public List<ConnectionModel> Connections { get; set; } = new List<ConnectionModel>();

        [JsonProperty("metadata")]
        public GraphMetadata Metadata { get; set; } = new GraphMetadata();
    }

    /// <summary>
    /// Model for serializing individual nodes
    /// </summary>
    public class NodeModel
    {
        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("callable")]
        public bool Callable { get; set; }

        [JsonProperty("execInit")]
        public bool ExecInit { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; }

        [JsonProperty("xmlExportName")]
        public string XmlExportName { get; set; }

        [JsonProperty("customWidth")]
        public int CustomWidth { get; set; }

        [JsonProperty("customHeight")]
        public int CustomHeight { get; set; }

        [JsonProperty("int32Tag")]
        public int Int32Tag { get; set; }

        [JsonProperty("nodeColor")]
        public int NodeColor { get; set; }

        [JsonProperty("methodName")]
        public string MethodName { get; set; }

        [JsonProperty("customEditor")]
        public CustomEditorInfo CustomEditor { get; set; }

        [JsonProperty("context")]
        public Dictionary<string, ContextProperty> Context { get; set; } = new Dictionary<string, ContextProperty>();

        [JsonProperty("flowControlHandler")]
        public string FlowControlHandler { get; set; }
    }

    /// <summary>
    /// Model for custom editor information
    /// </summary>
    public class CustomEditorInfo
    {
        [JsonProperty("assemblyName")]
        public string AssemblyName { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }
    }

    /// <summary>
    /// Model for context properties
    /// </summary>
    public class ContextProperty
    {
        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("actualType")]
        public string ActualType { get; set; }  // Store the actual runtime type for proper deserialization
    }

    /// <summary>
    /// Model for serializing connections
    /// </summary>
    public class ConnectionModel
    {
        [JsonProperty("outputNodeId")]
        public string OutputNodeId { get; set; }

        [JsonProperty("outputSocketName")]
        public string OutputSocketName { get; set; }

        [JsonProperty("inputNodeId")]
        public string InputNodeId { get; set; }

        [JsonProperty("inputSocketName")]
        public string InputSocketName { get; set; }
    }

    /// <summary>
    /// Model for graph metadata
    /// </summary>
    public class GraphMetadata
    {
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonProperty("modifiedAt")]
        public DateTime ModifiedAt { get; set; } = DateTime.Now;

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }
}