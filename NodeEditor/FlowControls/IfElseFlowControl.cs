using System;

namespace NodeEditor.FlowControls
{
    /// <summary>
    /// Flow control implementation for conditional branching
    /// </summary>
    public class IfElseFlowControl : IFlowControlNode
    {
        private const string CONDITION = "condition";
        private const string IF_TRUE = "ifTrue";
        private const string IF_FALSE = "ifFalse";
        private const string EXIT = "Exit";

        public void ExecuteFlowControl(
            INodesContext context,
            DynamicNodeContext nodeContext,
            Action<string> executeOutputPath,
            Func<bool> shouldBreak)
        {
            // Check if we should break execution
            if (shouldBreak())
            {
                return;
            }

            // Get the condition from the node context
            object conditionValue = nodeContext[CONDITION];
            bool condition;

            // Convert the condition to boolean
            if (conditionValue is bool boolValue)
            {
                condition = boolValue;
            }
            else if (conditionValue != null && bool.TryParse(conditionValue.ToString(), out bool parsedValue))
            {
                condition = parsedValue;
            }
            else
            {
                throw new ArgumentException($"IfElse node condition must be a boolean value. Received: {conditionValue?.GetType().Name ?? "null"}");
            }

            // Execute the appropriate path based on the condition
            if (condition)
            {
                executeOutputPath(IF_TRUE);
            }
            else
            {
                executeOutputPath(IF_FALSE);
            }

            // Execute the exit path after the conditional branch completes
            executeOutputPath(EXIT);
        }
    }
}