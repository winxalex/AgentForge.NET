// Copyright (c) Microsoft Corporation. All rights reserved.
// Modifier.cs
#region snippet_Modifier
using Chat2Report.Extensions;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using System.Diagnostics;
using ModifyF = System.Func<int, int>;

namespace GettingStartedSample;

//[TypeSubscription("plan")]
public class Modifier(
    AgentId id,
    IAgentRuntime runtime
  
    ) :
        BaseAgent(id, runtime, "Modifier", null),
        IHandle<Dictionary<string,object>>
{

    public async ValueTask HandleAsync(Dictionary<string,object> input, MessageContext messageContext)
    {
        Debug.WriteLine($"\n{Id}:Data input:\n{input.ToJSON()}");

        //Debug.WriteLine($"\n{Id}:\nModified {(int)item["New count"]} to {newValue}");
        //Console.WriteLine($"\n{Id}:\nModified {(int)item["New count"]} to {newValue}");



        //await this.SendMessageAsync(updateMessage, new AgentId("Checker2","chat"));
        // await this.SendMessageAsync(updateMessage, new AgentId("Checker2","default"));
    }
}
#endregion snippet_Modifier
