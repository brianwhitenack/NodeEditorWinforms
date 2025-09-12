using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using NodeEditor;
using NodeEditor.FlowControls;

namespace MathSample
{
    // Main context of the sample, each
    // method corresponds to a node by attribute decoration
    public class MathContext : INodesContext
    {
        public NodeVisual CurrentProcessingNode { get; set; }
        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        [Node("String Value", "Input", "Basic", "Allows to output a simple string value.", false)]
        public void StringValue(string inValue, out string outValue)
        {
            outValue = inValue;
        }

        [Node("String List Value", "Input", "Basic", "Allows to output a simple string list value.", false)]
        public void StringListValue(string[] inValue, out string[] outValue)
        {
            outValue = inValue;
        }

        [Node("For Each", "Loops", "Functional", "Transforms each item in a collection and returns the results.", true,
            flowControlHandler: typeof(ForEachFlowControl), Width = 250)]
        public void ForEach(IEnumerable<object> inputCollection, [LoopFeedback] object loopResult, out object currentItemInLoop, out ExecutionPath forEachItemLoop, out IEnumerable<object> forEachResult)
        {
            // Initialize outputs - actual values will be set by the flow control handler
            currentItemInLoop = null;
            forEachItemLoop = new ExecutionPath();
            forEachResult = null;
        }

        [Node("Value", "Input", "Basic", "Allows to output a simple value.", false)]
        public void InputValue(float inValue, out float outValue)
        {
            outValue = inValue;
        }

        [Node("Add", "Operators", "Basic", "Adds two input values.", false)]
        public void Add(float a, float b, out float result)
        {
            result = a + b;
        }

        [Node("Subtract", "Operators", "Basic", "Substracts two input values.", true)]
        public void Subtract(float a, float b, out float result)
        {
            result = a - b;
        }

        [Node("Multiply", "Operators", "Basic", "Multiplies two input values.", true)]
        public void Multiply(float a, float b, out float result)
        {
            result = a * b;
        }

        [Node("Divide", "Operators", "Basic", "Divides two input values.", true)]
        public void Divide(float a, float b, out float result)
        {
            result = a / b;
        }

        [Node("Show Value", "Helper", "Basic", "Shows input value in the message box.")]
        public void ShowMessageBox(object x)
        {
            string valueToShow;
            if (x == null)
            {
                valueToShow = "null";
            }
            else if(x is IEnumerable<object> va)
            {
                valueToShow = string.Join(", ", va.Select(item => item?.ToString() ?? "null"));
            }
            else
            {
                valueToShow = x.ToString();
            }

            MessageBox.Show(valueToShow, "Show Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [Node("To Upper", "Operators", "String", "Converts a string to uppercase.", true)]
        public void ToUpper(string input, out string output)
        {
            output = input?.ToUpper();
        }

        [Node("Concatenate", "Operators", "String", "Concatenates two strings.", true)]
        public void Concatenate(string a, string b, out string result)
        {
            result = a + b;
        }

        [Node("To String", "Operators", "String", "Converts to a string.", true)]
        public void ToStringNode(object a, out string result)
        {
            result = a?.ToString();
        }

        [Node("Starter", "Helper", "Basic", "Starts execution", true, true)]
        public void Starter()
        {

        }
    }
}
