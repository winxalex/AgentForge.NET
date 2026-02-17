namespace Chat2Report.Services.Chart
{
    public interface IChartRenderingService
    {
        IChartRenderer GetRenderer(string chartLibrary);
    }
}
