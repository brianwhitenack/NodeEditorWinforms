using System;
using System.Collections;
using System.Collections.Generic;

namespace NodeEditor.FlowControls
{
    /// <summary>
    /// Flow control implementation for map operations that transform each item in a collection
    /// </summary>
    public class ForEachFlowControl : IFlowControlNode
    {
        public void ExecuteFlowControl(
            INodesContext context,
            DynamicNodeContext nodeContext,
            Action<string> executeOutputPath,
            Func<bool> shouldBreak)
        {
            // Get the collection from the node context
            IEnumerable collection = nodeContext["inputCollection"] as IEnumerable;
            
            if (collection == null)
            {
                // If no collection, output empty array
                nodeContext["forEachResult"] = Array.Empty<object>();
                executeOutputPath("Exit");
                return;
            }

            // List to collect transformed results
            List<object> results = new List<object>();
            int index = 0;

            // Iterate through the collection
            foreach (var item in collection)
            {
                // Check if we should break execution
                if (shouldBreak())
                {
                    break;
                }

                // Set the current item in the context
                nodeContext["currentItemInLoop"] = item;
                
                // Clear the transformed item from previous iteration
                nodeContext["loopResult"] = null;
                
                // Execute the transform path - this should set transformedItem
                executeOutputPath("forEachItemLoop");
                
                // Collect the transformed result
                object transformedItem = nodeContext["loopResult"];
                if (transformedItem != null)
                {
                    results.Add(transformedItem);
                }
                else
                {
                    // If no transformation provided, use the original item
                    results.Add(item);
                }
                
                index++;
            }

            // Set the final results array
            nodeContext["forEachResult"] = results.ToArray();

            // Execute the exit path with the complete results
            executeOutputPath("Exit");
        }
    }
}