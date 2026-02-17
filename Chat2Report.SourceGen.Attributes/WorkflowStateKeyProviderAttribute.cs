using System;

namespace Chat2Report.SourceGen.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class WorkflowStateKeyProviderAttribute : Attribute
    {
        public string JsonFilePath { get; }
        public WorkflowStateKeyProviderAttribute(string jsonFilePath)
        {
            JsonFilePath = jsonFilePath;
        }
    }
}