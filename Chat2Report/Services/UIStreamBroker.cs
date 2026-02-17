﻿using Chat2Report.Models;

namespace Chat2Report.Services
{
    public class UIStreamBroker : IUIStreamBroker
    {
        public event Func<StreamPayload, Task>? StreamMessageReceived;

        public async Task BroadcastMessageAsync(StreamPayload payload,CancellationToken cancellationToken)
        {
            if (StreamMessageReceived != null)
            {
                // Invoke all handlers concurrently
                await Task.WhenAll(StreamMessageReceived.GetInvocationList()
                    .Cast<Func<StreamPayload, Task>>()
                    .Select(handler => handler(payload)));
            }
        }
    }
}
