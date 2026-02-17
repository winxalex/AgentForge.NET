using Azure;
using Azure.AI.Inference;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Services;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;


namespace Chat2Report.Agents.Factories
{
    

    public class ChatClientFactory : IChatClientFactory
    {
        private const string OPENAI = "openai";
        private const string HTTP = "http";
        private const string MCP = "mcp";
        private const string OLLAMA = "ollama";
        private const string ZAI = "zai";
        private const string OPENROUTER = "openrouter";
        private const string AZUREAI = "azureai";
        private const string GOOGLE = "google";
        private readonly AgentsConfiguration _configuration; // Use the config class directly


        // --- Improved ClientKey ---
        // Includes all properties that define a unique client instance.
        private readonly struct ClientKey : IEquatable<ClientKey>
        {
            public readonly string ClientType; // e.g., "openai", "ollama" (lowercase)
            public readonly string ModelId;
            public readonly string Endpoint;
            public readonly string ApiKey; // Store API key directly for simplicity in this context. Consider hashing if security is paramount.

            public ClientKey(string clientType, string modelId, string endpoint, string apiKey)
            {
                // Normalize and validate inputs slightly
                ClientType = clientType?.ToLowerInvariant() ?? string.Empty;
                ModelId = modelId ?? string.Empty;
                Endpoint = endpoint ?? string.Empty;
                ApiKey = apiKey ?? string.Empty; // Important for providers like OpenAI
            }

            public bool Equals(ClientKey other)
            {
                return ClientType == other.ClientType &&
                       ModelId == other.ModelId &&
                       Endpoint == other.Endpoint &&
                       ApiKey == other.ApiKey; // API Key must also match
            }

            public override bool Equals(object obj)
            {
                return obj is ClientKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                // Combine hash codes using a standard approach
                return HashCode.Combine(ClientType, ModelId, Endpoint, ApiKey);
                // Note: HashCode.Combine handles nulls appropriately.
            }
        }

        // Thread-safe cache for single chat clients (resilient clients are created on demand)
        private readonly ConcurrentDictionary<ClientKey, IChatClient> _chatClientCache = new();
        // Thread-safe cache for embedding generators
        private readonly ConcurrentDictionary<ClientKey, IEmbeddingGenerator<string, Embedding<float>>> _embeddingGeneratorCache = new();

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChatClientFactory> _logger;


        public ChatClientFactory(IOptions<AgentsConfiguration> agentsConfiguration, IServiceProvider serviceProvider, ILogger<ChatClientFactory> logger)
        {
            _configuration = agentsConfiguration.Value ?? throw new ArgumentNullException(nameof(agentsConfiguration));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        //private bool TryGetAgentOptions(string agentName, [NotNullWhen(true)] out AgentDefinition? agentOptions)
        //{
        //    agentOptions = null;
        //    if (string.IsNullOrWhiteSpace(agentName))
        //    {
        //        _logger.LogError("Agent name cannot be null or whitespace.");
        //        return false;
        //    }

        //    if (!_configuration.Agents.TryGetValue(agentName, out agentOptions) || agentOptions?.Clients == null || !agentOptions.Clients.Any())
        //    {
        //        _logger.LogWarning("Configuration for agent '{AgentName}' is missing, incomplete, or has no clients defined.", agentName);
        //        return false;
        //    }

        //    // Validate each client configuration in the list
        //    foreach (var clientConfig in agentOptions.Clients)
        //    {
        //        if (string.IsNullOrWhiteSpace(clientConfig.Type) || string.IsNullOrWhiteSpace(clientConfig.ModelId))
        //        {
        //            _logger.LogWarning("A client configuration for agent '{AgentName}' is missing Type or ModelId.", agentName);
        //            return false;
        //        }

        //        // Endpoint is not required for all providers (e.g., Google, SQL)
        //        var endpointRequiredProviders = new[] { OPENAI, ZAI, OPENROUTER, AZUREAI, OLLAMA };
        //        if (endpointRequiredProviders.Contains(clientConfig.Type.ToLowerInvariant()) && string.IsNullOrWhiteSpace(clientConfig.Endpoint))
        //        {
        //            _logger.LogWarning("An {ClientType} client configuration for agent '{AgentName}' is missing an Endpoint.", clientConfig.Type, agentName);
        //            return false;
        //        }

        //        var keyRequiredProviders = new[] { OPENAI, ZAI, OPENROUTER, AZUREAI, GOOGLE };
        //        if (keyRequiredProviders.Contains(clientConfig.Type.ToLowerInvariant()) && string.IsNullOrWhiteSpace(clientConfig.ApiKey))
        //        {
        //            _logger.LogWarning("An {ClientType} client configuration for agent '{AgentName}' is missing an ApiKey.", clientConfig.Type, agentName);
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        public IChatClient CreateChatClient(ClientConfig clientConfig)
        {
            if (clientConfig == null)
            {
                throw new ArgumentNullException(nameof(clientConfig));
            }

            var clientKey = new ClientKey(
                clientConfig.Type.ToLowerInvariant(),
                clientConfig.ModelId,
                clientConfig.Endpoint,
                clientConfig.ApiKey);

            return _chatClientCache.GetOrAdd(clientKey, key =>
            {
                _logger.LogDebug("Creating a single chat client for type '{ClientType}' model '{ModelId}'.", clientConfig.Type, clientConfig.ModelId);
                return CreateSingleChatClient(clientConfig);
            });
        }

        public IChatClient CreateChatClient(List<(string Id, ClientConfig Config)> clientConfigs, ClientTimeoutOptions timeoutOptions)
        {
            if (clientConfigs == null || !clientConfigs.Any())
            {
                throw new ArgumentException("Client configurations cannot be null or empty for a resilient client.", nameof(clientConfigs));
            }

            _logger.LogDebug("Creating a ResilientChatClient with {ClientCount} fallback clients.", clientConfigs.Count);

            var resilientClientData = clientConfigs.Select(cc =>
            {
                var client = CreateSingleChatClient(cc.Config); // Individual clients are still cached
                return (client, cc.Id, cc.Config); // Use the provided ID as the description
            }).ToList();

            return new ResilientChatClient(
                resilientClientData,
                timeoutOptions,
                _serviceProvider.GetRequiredService<IToolInstanceProvider>(),
                _serviceProvider.GetRequiredService<IDelegateCache>(),
                _serviceProvider.GetRequiredService<ILogger<ResilientChatClient>>());
        }

      
        public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ClientConfig clientConfig)
        {
            if (clientConfig == null)
            {
                throw new ArgumentNullException(nameof(clientConfig));
            }

            var clientKey = new ClientKey(
                clientConfig.Type.ToLowerInvariant(),
                clientConfig.ModelId,
                clientConfig.Endpoint,
                clientConfig.ApiKey);

            // Use GetOrAdd for thread-safe caching
            return _embeddingGeneratorCache.GetOrAdd(clientKey, key =>
            {
                return key.ClientType switch
                {
                    OLLAMA => CreateOllamaEmbedderInternal(clientConfig),
                    OPENAI => CreateOpenAIEmbedderInternal(clientConfig),
                    _ => throw new NotSupportedException($"Unsupported embedder type: '{clientConfig.Type}'.")
                };
            });
        }

        public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(List<ClientConfig> clientConfigs)
        {
            if (clientConfigs == null || !clientConfigs.Any())
            {
                throw new ArgumentException("Client configurations cannot be null or empty.", nameof(clientConfigs));
            }

            // TODO: Implement a ResilientEmbeddingGenerator similar to ResilientChatClient
            // For now, we'll just use the first one as a fallback.
            return CreateEmbeddingGenerator(clientConfigs.First());
        }

        // --- Internal Creation Methods ---

        private IChatClient CreateSingleChatClient(ClientConfig config)
        {
            IChatClient client;
            switch (config.Type.ToLowerInvariant())
            {
                case ZAI:
                case OPENAI:
                case OPENROUTER:
                    var openAIClientOptions = new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) };
                    var apiKey = new ApiKeyCredential(config.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                    client = new OpenAIClient(apiKey, openAIClientOptions).GetChatClient(config.ModelId).AsIChatClient();
                    break;

                case AZUREAI:
                    var endpoint = new Uri(config.Endpoint);
                    var credential = new AzureKeyCredential(config.ApiKey);
                    client = new ChatCompletionsClient(endpoint, credential).AsIChatClient(config.ModelId);
                    break;

                case OLLAMA:
                    client = new OllamaChatClient(new Uri(config.Endpoint), config.ModelId);
                    break;

                case GOOGLE:
                    // The official Google library handles the endpoint internally.

                    client = new GenerativeAIChatClient(config.ApiKey, config.ModelId);
                    break;
                default:
                    throw new NotSupportedException($"Chat client provider '{config.Type}' is not supported.");
            }

            return DecorateClient(client, config);
        }

        private IEmbeddingGenerator<string, Embedding<float>> CreateOllamaEmbedderInternal(ClientConfig options)
        {
            // Assuming Uri constructor throws if endpoint is invalid format
            return new OllamaEmbeddingGenerator(new Uri(options.Endpoint), options.ModelId);
        }

        private IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbedderInternal(ClientConfig config)
        {
            // Endpoint and ApiKey null/whitespace checks are done in TryGetOptionsAndKey
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint)
                // Add other config like RetryPolicy, Transport, etc. if needed from configuration
            };

            var credential = new ApiKeyCredential(config.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

            // Return the specific embedding generator interface
            return new OpenAIClient(credential, clientOptions).GetEmbeddingClient(config.ModelId).AsIEmbeddingGenerator();
                    
        }

        private IChatClient DecorateClient(IChatClient client, ClientConfig config)
        {
            if (config.Log)
            {
                client = new LoggingChatClient(client, _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<LoggingChatClient>());
            }
           


            if (config?.Options?.Tools != null)
            {
                client = new FunctionInvokingChatClient(client, _serviceProvider.GetRequiredService<ILoggerFactory>(),_serviceProvider);
            }

           

            return client;
        }
    }
}
