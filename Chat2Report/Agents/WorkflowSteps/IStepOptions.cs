namespace Chat2Report.Agents.WorkflowSteps
{
    public interface IStepOptions
    {
       string OutputKey { get; set; }
        string InputKey { get; set; }

        string ErrorKey { get; set; }
    }
}