using System;

namespace NodeEditor
{
    /// <summary>
    /// Interface for nodes that control execution flow (loops, conditionals, etc.)
    /// </summary>
    public interface IFlowControlNode
    {
        /// <summary>
        /// Executes the flow control logic for this node
        /// </summary>
        /// <param name="context">The nodes context</param>
        /// <param name="nodeContext">The dynamic context containing node inputs/outputs</param>
        /// <param name="executeOutputPath">Callback to execute a named output execution path</param>
        /// <param name="shouldBreak">Function to check if execution should be interrupted</param>
        void ExecuteFlowControl(
            INodesContext context,
            DynamicNodeContext nodeContext,
            Action<string> executeOutputPath,
            Func<bool> shouldBreak);
    }
}