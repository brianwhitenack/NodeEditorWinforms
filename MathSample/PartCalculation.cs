using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using NodeEditor;
using NodeEditor.FlowControls;

namespace MathSample
{
    // Main context of the sample, each
    // method corresponds to a node by attribute decoration
    public class PartCalculation : INodesContext
    {
        public NodeVisual CurrentProcessingNode { get; set; }
        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        [Node("Create Part", "Parts", "Basic", "Create a part", true)]
        public void CreatePart(string sku, string description, string package, float quantity, string unitOfMeasure, out Part part)
        {
            part = new Part
            {
                Sku = sku,
                Description = description,
                Package = package,
                Quantity = quantity,
                UnitOfMeasure = unitOfMeasure
            };
        }

        [Node("Parts List", "Parts", "Basic", "Create a list of parts", false)]
        public void PartsList(ExecutionPath calculationEnd, IEnumerable<Part> parts)
        {

        }

        [Node("String Value", "Constants", "Basic", "Allows to output a simple string value.", false)]
        public void StringValue(string inValue, out string outValue)
        {
            outValue = inValue;
        }

        [Node("String List Value", "Constants", "Basic", "Allows to output a simple string list value.", false)]
        public void StringListValue(string[] inValue, out string[] outValue)
        {
            outValue = inValue;
        }

        [Node("For Each", "Loops", "Functional", "Transforms each item in a collection and returns the results.", true,
            flowControlHandler: typeof(ForEachFlowControl), Width = 250)]
        [DynamicNode]
        public void ForEach(
            [DynamicType(TypeGroup = "InputType")] IEnumerable<object> inputCollection, 
            [LoopFeedback][DynamicType(TypeGroup = "OutputType")] object loopResult,
            [DynamicType(TypeGroup = "InputType", ExtractElementType = true, DerivedFrom = nameof(inputCollection))] out object currentItemInLoop,
            out ExecutionPath forEachItemLoop,
            [DynamicType(TypeGroup = "OutputType", WrapInCollection = true)] out IEnumerable<object> forEachResult)
        {
            // Initialize outputs - actual values will be set by the flow control handler
            currentItemInLoop = null;
            forEachItemLoop = new ExecutionPath();
            forEachResult = null;
        }

        [Node("Value", "Constants", "Basic", "Allows to output a simple value.", false)]
        public void InputValue(float inValue, out float outValue)
        {
            outValue = inValue;
        }

        [Node("Add", "Math", "Basic", "Adds two input values.", false)]
        public void Add(float a, float b, out float result)
        {
            result = a + b;
        }

        [Node("Subtract", "Math", "Basic", "Substracts two input values.", true)]
        public void Subtract(float a, float b, out float result)
        {
            result = a - b;
        }

        [Node("Multiply", "Math", "Basic", "Multiplies two input values.", true)]
        public void Multiply(float a, float b, out float result)
        {
            result = a * b;
        }

        [Node("Divide", "Math", "Basic", "Divides two input values.", true)]
        public void Divide(float a, float b, out float result)
        {
            result = a / b;
        }

        [Node("Show Value", "Debug", "Basic", "Shows input value in the message box.")]
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

        [Node("To List", "Operators", "List", "Converts to a string.", true)]
        public void ToListNode(object a, out List<Object> list)
        {
            list = new List<object>() { a };
        }

        [Node("Starter", "Helper", "Basic", "Starts execution", true, true)]
        public void Starter()
        {

        }
    }
}
