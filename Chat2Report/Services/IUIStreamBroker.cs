using Chat2Report.Models;

namespace Chat2Report.Services
{
    public interface IUIStreamBroker
    {
        event Func<StreamPayload, Task>? StreamMessageReceived;
        Task BroadcastMessageAsync(StreamPayload message, CancellationToken cancellationToken);
    }
}