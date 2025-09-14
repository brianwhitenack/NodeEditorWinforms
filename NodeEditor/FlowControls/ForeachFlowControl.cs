using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor.FlowControls
{
    /// <summary>
    /// Flow control implementation for map operations that transform each item in a collection
    /// </summary>
    public class ForEachFlowControl : IFlowControlNode
    {
        private const string INPUT_COLLECTION = "inputCollection";
        private const string CURRENT_ITEM_IN_LOOP = "currentItemInLoop";
        private const string LOOP_RESULT = "loopResult";
        private const string FOR_EACH_RESULT = "forEachResult";
        private const string FOR_EACH_ITEM_LOOP = "forEachItemLoop";
        private const string EXIT = "Exit";
        public void ExecuteFlowControl(
            INodesContext context,
            DynamicNodeContext nodeContext,
            Action<string> executeOutputPath,
            Func<bool> shouldBreak)
        {
            // Get the collection from the node context
            IEnumerable collection = nodeContext[INPUT_COLLECTION] as IEnumerable;
            
            if (collection == null)
            {
                // If no collection, output empty array
                nodeContext[FOR_EACH_RESULT] = Array.Empty<object>();
                executeOutputPath(EXIT);
                return;
            }

            // Get the runtime type for forEachResult to create properly typed collection
            Type resultType = typeof(object);
            NodeVisual currentNode = context.CurrentProcessingNode;
            if (currentNode != null)
            {
                Type runtimeType = currentNode.GetSocketRuntimeType(FOR_EACH_RESULT);
                if (runtimeType != null && runtimeType.IsArray)
                {
                    resultType = runtimeType.GetElementType();
                }
                else if (runtimeType != null && runtimeType.IsGenericType)
                {
                    Type[] genericArgs = runtimeType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        resultType = genericArgs[0];
                    }
                }
            }
            
            // Create a properly typed list for results
            Type listType = typeof(List<>).MakeGenericType(resultType);
            System.Collections.IList results = Activator.CreateInstance(listType) as System.Collections.IList;
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
                nodeContext[CURRENT_ITEM_IN_LOOP] = item;
                
                // Clear the transformed item from previous iteration
                nodeContext[LOOP_RESULT] = null;
                
                // Execute the transform path - this should set transformedItem
                executeOutputPath(FOR_EACH_ITEM_LOOP);
                
                // Collect the transformed result
                object transformedItem = nodeContext[LOOP_RESULT];
                if (transformedItem != null)
                {
                    results.Add(transformedItem);
                }
                //else
                //{
                //    // If no transformation provided, use the original item
                //    results.Add(item);
                //}
                
                index++;
            }

            // Always return a List for consistency and better type handling
            // Lists work better with our type system and avoid array covariance issues
            nodeContext[FOR_EACH_RESULT] = results;

            // Execute the exit path with the complete results
            executeOutputPath(EXIT);
        }
    }
}