using System;

namespace NodeEditor
{
    /// <summary>
    /// Marks a parameter as a loop feedback input that should not be resolved during normal resolution
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class LoopFeedbackAttribute : Attribute
    {
        public LoopFeedbackAttribute()
        {
        }
    }
}