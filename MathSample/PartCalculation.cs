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
        public event Action OnExecutionFinished;

        public enum UnitOfMeasure
        {
            EA,
            SQFT,
        }

        public NodeVisual CurrentProcessingNode { get; set; }
        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        public List<Measurement> Measurements { get; set; }
        public List<Part> Parts { get; set; }

        public PartCalculation()
        {
            Measurements = new List<Measurement>();
            Parts = new List<Part>();
        }

        public void FinishExecution()
        {
            OnExecutionFinished?.Invoke();
        }

        [Node("Filter To Type", "Measurements", "Basic", "Filter measurements by type", false)]
        public void FilterToType(List<Measurement> measurements, string type, out List<Measurement> filteredMeasurements)
        {
            filteredMeasurements = measurements.Where(m => m.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        [Node("Measurement List", "Measurements", "Basic", "Get the current measurement list", false)]
        public void MeasurementList(out List<Measurement> measurements)
        {
            measurements = Measurements;
        }

        [Node("Number Selection", "Measurements", "Basic", "Select a number from the measurement", false)]
        public void NumberSelection(Measurement measurement, string selectionName, out double selectionValue)
        {
            if (measurement.Selections.TryGetValue(selectionName, out object value) && double.TryParse(value?.ToString(), out double parsedValue))
            {
                selectionValue = parsedValue;
            }
            else
            {
                selectionValue = 0;
            }
        }

        [Node("String Selection", "Measurements", "Basic", "Select a string from the measurement", false)]
        public void StringSelection(Measurement measurement, string selectionName, out string selectionValue)
        {
            if (measurement.Selections.TryGetValue(selectionName, out object value))
            {
                selectionValue = value.ToString();
            }
            else
            {
                selectionValue = string.Empty;
            }
        }

        [Node("Measurement Length", "Measurements", "Basic", "Get the length of the measurement", false)]
        public void MeasurementLength(Measurement measurement, out double length)
        {
            length = measurement.Length;
        }

        [Node("Measurement Length Sum", "Measurements", "Basic", "Get the length of the measurement", false)]
        public void MeasurementLengthSum(List<Measurement> measurements, out double length)
        {
            length = measurements.Sum(m => m.Length);
        }

        [Node("Measurement Area", "Measurements", "Basic", "Get the area of the measurement", false)]
        public void MeasurementArea(Measurement measurement, out double area)
        {
            area = measurement.Area;
        }

        [Node("Measurement Area Sum", "Measurements", "Basic", "Get the area of the measurement", false)]
        public void MeasurementAreaSum(List<Measurement> measurements, out double area)
        {
            area = measurements.Sum(m => m.Area);
        }

        [Node("If Else", "Flow Control", "Basic", "Standard If Else flow control", false, flowControlHandler: typeof(IfElseFlowControl))]
        public void IfElse(bool condition, ExecutionPath enter, out ExecutionPath ifTrue, out ExecutionPath ifFalse)
        {
            ifTrue = new ExecutionPath();
            ifFalse = new ExecutionPath();
        }

        [Node("Boolean Value", "Constants", "Basic", "Allows to output a simple boolean value.", false)]
        public void BooleanValue(bool inValue, out bool outValue)
        {
            outValue = inValue;
        }

        [Node("Create Part", "Parts", "Basic", "Create a part")]
        public void CreatePart(string sku, string description, string package, double quantity, string unitOfMeasure, out Part part)
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
        public void PartsList(ExecutionPath calculationEnd, List<Part> parts)
        {
            Parts = parts;
            FinishExecution();
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

        [Node("For Each", "Flow Control", "Functional", "Transforms each item in a collection and returns the results.", true,
            flowControlHandler: typeof(ForEachFlowControl), Width = 250)]
        [DynamicNode]
        public void ForEach(
            [DynamicType(TypeGroup = "InputType")] List<object> inputCollection, 
            [LoopFeedback][DynamicType(TypeGroup = "OutputType")] object loopResult,
            [DynamicType(TypeGroup = "InputType", ExtractElementType = true, DerivedFrom = nameof(inputCollection))] out object currentItemInLoop,
            out ExecutionPath forEachItemLoop,
            [DynamicType(TypeGroup = "OutputType", WrapInCollection = true)] out List<object> forEachResult)
        {
            // Initialize outputs - actual values will be set by the flow control handler
            currentItemInLoop = null;
            forEachItemLoop = new ExecutionPath();
            forEachResult = null;
        }

        [Node("Pass Through Value", "Flow Control", "Functional", "Pass through a variable with control flow.", true)]
        [DynamicNode]
        public void PassThrough(
            [DynamicType(TypeGroup = "Input")] object input,
            [DynamicType(TypeGroup = "Input")] out object output)
        {
           output = input;
        }

        [Node("Number Value", "Constants", "Basic", "Allows to output a simple value.", false)]
        public void InputValue(double inValue, out double outValue)
        {
            outValue = inValue;
        }

        [Node("TXWXL", "Dimensions", "Basic", "Creates a TXWXL string", false)]
        [DynamicNode]
        public void ThicknessWidthLength(
            [DynamicType(TypeGroup = "Thickness")] object thickness,
            [DynamicType(TypeGroup = "Width")] object width,
            [DynamicType(TypeGroup = "Length")] object length, out string dimension)
        {
            dimension = $"{thickness}X{width}X{length}";
        }

        [Node("Round", "Math", "Basic", "Rounds a number to the nearest integer.", false)]
        public void Round(double value, bool toNearestEven, out double result)
        {
            result = Math.Ceiling(value);

            if (result % 2 != 0)
                result += 1;
        }

        [Node("Add", "Math", "Basic", "Adds two input values.", false)]
        public void Add(double a, double b, out double result)
        {
            result = a + b;
        }

        [Node("Subtract", "Math", "Basic", "Substracts two input values.", false)]
        public void Subtract(double a, double b, out double result)
        {
            result = a - b;
        }

        [Node("Multiply", "Math", "Basic", "Multiplies two input values.", false)]
        public void Multiply(double a, double b, out double result)
        {
            result = a * b;
        }

        [Node("Divide", "Math", "Basic", "Divides two input values.", false)]
        public void Divide(double a, double b, out double result)
        {
            result = a / b;
        }

        [Node("Show Value", "Debug", "Basic", "Shows input value in the message box.", true)]
        [DynamicNode]
        public void ShowMessageBox([DynamicType(TypeGroup = "Input")] object x)
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

        [Node("To Upper", "Operators", "String", "Converts a string to uppercase.", false)]
        public void ToUpper(string input, out string output)
        {
            output = input?.ToUpper();
        }

        [Node("Concatenate", "Operators", "String", "Concatenates two strings.", false)]
        public void Concatenate(string a, string b, out string result)
        {
            result = a + b;
        }

        [Node("Join", "Operators", "String", "Joins two strings with a separator.", false)]
        public void Join(string separator, string a, string b, out string result)
        {
            result = string.Join(separator, a, b);
        }

        [Node("To String", "Operators", "String", "Converts to a string.", false)]
        [DynamicNode]
        public void ToStringNode([DynamicType(TypeGroup = "Input")] object a, out string result)
        {
            result = a?.ToString();
        }

        [Node("To List", "Operators", "List", "Creates a list containing a single item.", false)]
        [DynamicNode]
        public void ToListNode(
            [DynamicType(TypeGroup = "Input")] object item,
            [DynamicType(TypeGroup = "Input", WrapInCollection = true)] out List<object> list)
        {
            list = new List<object>() { item };
        }

        [Node("Starter", "Helper", "Basic", "Starts execution", true, true)]
        public void Starter()
        {

        }
    }
}
