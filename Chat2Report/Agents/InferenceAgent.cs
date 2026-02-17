// Copyright (c) Microsoft Corporation. All rights reserved.
// InferenceAgent.cs
//using Google.Protobuf;

using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.AI;

namespace Chat2Report.Agents
{
    /// <summary>
    /// Base class for inference agents using the Microsoft.Extensions.AI library.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <param name="runtime"></param>
    /// <param name="name"></param>
    /// <param name="logger"></param>
    /// <param name="client"></param>
    public abstract class InferenceAgent : BaseAgent


    {


        private IChatClient __client;
        private IEmbeddingGenerator<string, Embedding<float>> __embeddingGenerator;
        

        public InferenceAgent(
        AgentId id,
        IAgentRuntime runtime,
        string description,
        IChatClient client,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = null,
        ILogger<BaseAgent> logger = null
        ) : base(id, runtime, description, logger)
        {
            if (client == null && embeddingGenerator == null)
            {
                throw new ArgumentException("At least one of 'client' or 'embeddingGenerator' must be provided.");
            }

            __client = client;
            __embeddingGenerator = embeddingGenerator;

        }

        protected IChatClient ChatClient => __client;
        protected IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator => __embeddingGenerator;




        protected Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
            , IEnumerable<string> prompt, CancellationToken cancellationToken)
        {
            return embeddingGenerator.GenerateAsync(prompt,null,cancellationToken);
        }

        protected Task<ChatResponse> CompleteAsync(
          IList<ChatMessage> chatMessages,
          ChatOptions? options = null,

        CancellationToken cancellationToken = default)
        {

            return ChatClient.GetResponseAsync(chatMessages, options, cancellationToken);
        }
        protected IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
            IList<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return ChatClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
        }

    }
}
