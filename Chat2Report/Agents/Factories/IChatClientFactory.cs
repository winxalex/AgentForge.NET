using Chat2Report.Models;
using Microsoft.Extensions.AI;
using System;

namespace Chat2Report.Agents.Factories
{
    public interface IChatClientFactory
    {
        /// <summary>
        /// Creates or retrieves a cached chat client based on a specific client configuration.
        /// </summary>
        /// <param name="clientConfig">The client configuration to use.</param>
        /// <returns>An instance of <see cref="IChatClient"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the client type specified in the configuration is unknown.</exception>
        IChatClient CreateChatClient(ClientConfig clientConfig);

        IChatClient CreateChatClient(List<(string Id, ClientConfig Config)> clientConfigs, ClientTimeoutOptions timeoutOptions);
        IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ClientConfig clientConfig);
        IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(List<ClientConfig> clientConfigs);
    }
}
