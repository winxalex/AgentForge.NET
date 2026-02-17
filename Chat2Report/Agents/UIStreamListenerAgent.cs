//﻿using Microsoft.AutoGen.Contracts;
//using Microsoft.AutoGen.Core;
//using Chat2Report.Services;
//using Chat2Report.Models;

//namespace Chat2Report.Agents
//{
//   // [TypeSubscription("ui-stream")]
//    public class UIStreamListenerAgent : BaseAgent, IHandle<StreamPayload>
//    {
//        private readonly IUIStreamBroker _broker;

//        public UIStreamListenerAgent(
//            AgentId id,
//            IAgentRuntime runtime,
//            IUIStreamBroker broker,
//            ILogger<UIStreamListenerAgent> logger = null)
//            : base(id, runtime, "UI Stream Listener", logger)
//        {
//            _broker = broker ?? throw new ArgumentNullException(nameof(broker));

//            _logger?.LogInformation("UIStreamListenerAgent initialized and subscribing to UI stream messages.");
//        }

//        public async ValueTask HandleAsync(StreamPayload payload, MessageContext context)
//        {
//            _logger?.LogTrace("UIStreamListenerAgent received a message to broadcast to the UI.");
//            await _broker.BroadcastMessageAsync(payload);
//        }
//    }
//}