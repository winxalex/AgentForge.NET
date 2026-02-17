//// Copyright (c) Microsoft Corporation. All rights reserved.
//// Checker.cs

//#region snippet_Checker
//using System.Diagnostics;
//using Microsoft.AutoGen.Contracts;
//using Microsoft.AutoGen.Core;
//using Chat2Report.Services;

//using TerminationF = System.Func<int, bool>;

//namespace GettingStartedSample;

////[TypeSubscription("default")]
////[TypeSubscription("chat")]
//public class Checker(
//    AgentId id,
//    IAgentRuntime runtime,
//    TopicTerminationService completionService,
//    IHostApplicationLifetime hostApplicationLifetime,
//    TerminationF runUntilFunc
//    ) :
//        BaseAgent(id, runtime, "Modifier", null),
//        IHandle<Dictionary<string,object>>
//{
//    public async ValueTask HandleAsync(Dictionary<string,object> item, MessageContext messageContext)
//    {
//        var newCount=(int)item["Count"];

//        if (!runUntilFunc(newCount))
//        {
//            Debug.WriteLine($"{Id}:\n{newCount} passed the check, continue.");
//            _logger.LogTrace($"{Id}:\n{newCount} passed the check, continue.");
//            Console.WriteLine($"{Id}:\n{newCount} passed the check, continue.");
//            var updateMessage=new Dictionary<string,object>(){
//                {
//                    "New count",newCount
//                }
//            };
            
//            await this.PublishMessageAsync(updateMessage, new TopicId("default"));
//            //await this.PublishMessageAsync(new CountMessage { Content = newCount }, new TopicId("default"));
//        }
//        else
//        {
//            Debug.WriteLine($"Checker:\n{newCount} failed the check, stopping.");
//             _logger.LogTrace($"\nChecker:\n{newCount} failed the check, stopping.");
          
//            Console.WriteLine($"\nChecker:\n{newCount} failed the check, stopping.");
//            completionService.TryComplete("user",
//                    new Dictionary<string, object> { { "finalCount", newCount } });

            
//            //hostApplicationLifetime.StopApplication();
//        }
//    }
//}
//#endregion snippet_Checker
