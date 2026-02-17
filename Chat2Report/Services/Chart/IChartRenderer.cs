using Chat2Report.Models;

namespace Chat2Report.Services.Chart
{
    public interface IChartRenderer
    {    
        Task RenderAsync( string containerId, ChartContent chartContent);
    }
}
