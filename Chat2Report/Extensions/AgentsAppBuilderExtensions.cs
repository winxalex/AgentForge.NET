
using Microsoft.AutoGen.Core;

using Chat2Report.Models;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Web;
using System.Diagnostics;
using Chat2Report.Agents;

namespace Chat2Report.Extensions
{
    public static class AgentsAppBuilderExtensions
    {
        public static AgentsWebBuilder DeployAgents(this AgentsWebBuilder builder, Dictionary<string, WorkflowDefinition> allWorkflows)
        {
            ILogger<AgentsWebBuilder>? logger = builder.Logger;

            if (allWorkflows == null || !allWorkflows.Any())
            {
                throw new ArgumentException("Missing Agents configuration.");
            }

            foreach (var workflow in allWorkflows)
            {
                foreach (var agent in workflow.Value.Agents)
                {
                    //Inside TypeSubscriptions are created where agentType is mapped to topic
                    //When topic is published to, the corresponding agentType is instantiated and executed
                    //builder.AddAgent<UniversalAgent>($"{workflow.Key}.{agent.Key}", agent.Value.TopicId);
                    builder.AddAgent<UniversalAgent>($"{workflow.Key}_{agent.Key}", agent.Value.TopicId);
                    //builder.AddAgent<UniversalAgent>($"{agent.Key}", agent.Value.TopicId);

                }
            }

            return builder;
        }

    }
}