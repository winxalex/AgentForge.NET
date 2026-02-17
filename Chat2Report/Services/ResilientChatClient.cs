// Services/ResilientChatClient.cs
using Chat2Report.Models;
using Chat2Report.Providers;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chat2Report.Services
{
    /// <summary>
    /// An IChatClient implementation that provides resilience by trying a sequence of configured
    /// chat clients. If one client fails with a "Too Many Requests" error, it automatically
    /// falls back to the next client in the list.
    /// </summary>
    public class ResilientChatClient : IChatClient
    {
        private readonly ILogger<ResilientChatClient> _logger;
        private readonly IToolInstanceProvider _toolInstanceProvider;
        private readonly IDelegateCache _delegateCache;
        private readonly List<IChatClient> _clients;
        private readonly List<ClientConfig> _clientConfigs;
        private readonly List<string> _clientDescriptions;
        private readonly int _requestTimeoutMinutes;
        private readonly int _streamingInactivityTimeoutMinutes;
        private bool _disposed;

        public ResilientChatClient(
            IEnumerable<(IChatClient client, string description, ClientConfig config)> clients,
            ClientTimeoutOptions timeoutOptions,
            IToolInstanceProvider toolInstanceProvider,
            IDelegateCache delegateCache,
            ILogger<ResilientChatClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var clientList = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));

            if (!clientList.Any())
            {
                throw new InvalidOperationException("Cannot create a ResilientChatClient with no clients.");
            }

            // Initialize the global client order if it's the first time.
            ClientOrderService.Initialize(clientList.Select(c => c.description));

            // Get the globally sorted list of client IDs.
            var orderedIds = ClientOrderService.GetOrderedIds();
            _logger.LogDebug("Current global client order: {OrderedIds}", string.Join(", ", orderedIds));

            // Create a dictionary for quick lookups of the incoming clients.
            var clientMap = clientList.ToDictionary(c => c.description);

            // Sort the client list for this instance based on the global order.
            var sortedClientList = orderedIds
                .Where(id => clientMap.ContainsKey(id)) // Only take clients that are present in this resilient client's config
                .Select(id => clientMap[id])
                .ToList();

            // Add any clients from this instance's config that might not be in the global order list yet (e.g., newly added in appsettings).
            // They will be appended to the end of the sorted list.
            sortedClientList.AddRange(clientList.Where(c => !orderedIds.Contains(c.description)));

            _clients = sortedClientList.Select(c => c.client).ToList();
            _clientConfigs = sortedClientList.Select(c => c.config).ToList();
            _clientDescriptions = sortedClientList.Select(c => c.description).ToList();

            _logger.LogDebug("Effective client order for this instance: {EffectiveOrder}", string.Join(", ", _clientDescriptions));

            _toolInstanceProvider = toolInstanceProvider ?? throw new ArgumentNullException(nameof(toolInstanceProvider));
            _delegateCache = delegateCache ?? throw new ArgumentNullException(nameof(delegateCache));

            // Set timeouts, with defaults.
            _requestTimeoutMinutes = timeoutOptions?.RequestTimeoutMinutes ?? 2;
            _streamingInactivityTimeoutMinutes = timeoutOptions?.StreamingInactivityTimeoutMinutes ?? 5;
            _disposed = false;
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, Microsoft.Extensions.AI.ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (!_clients.Any())
            {
                throw new InvalidOperationException("No available chat clients to process the request.");
            }

            // The internal lists are already sorted correctly by the constructor.
            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                var description = _clientDescriptions[i]; // This is the unique ID
                var clientOptions = CreateOptionsForClient(i);

                try
                {
                    _logger.LogInformation($"Attempting to get response using client: {description} with timeout of {_requestTimeoutMinutes} minutes.");
                    var apiCallTask = client.GetResponseAsync(messages, clientOptions, cancellationToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(_requestTimeoutMinutes), cancellationToken);

                    var completedTask = await Task.WhenAny(apiCallTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning($"Client {description} timed out after {_requestTimeoutMinutes} minutes. Moving to end of global list and falling back.", _requestTimeoutMinutes);
                        ClientOrderService.MoveToEnd(description);
                        continue;
                    }

                    return await apiCallTask;
                }
                catch (ClientResultException ex) when (ex.Status == 429) // Too Many Requests
                {
                    _logger.LogWarning(ex, "Client {ClientDescription} failed with status 429 (Too Many Requests). Moving to end of global list and falling back.", description);
                    ClientOrderService.MoveToEnd(description);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Operation was canceled by the caller.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Client {ClientDescription} failed with an unexpected error. Moving to end of global list and falling back.", description);
                    ClientOrderService.MoveToEnd(description);
                }
            }

            throw new InvalidOperationException("All configured chat clients failed to provide a response.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_clients.Any())
            {
                throw new InvalidOperationException("No available chat clients to process the request.");
            }

            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                var description = _clientDescriptions[i]; // This is the unique ID
                var clientOptions = CreateOptionsForClient(i);
                _logger.LogInformation("Attempting to get streaming response using client: {ClientDescription}", description);

                IAsyncEnumerator<ChatResponseUpdate> enumerator;
                try
                {
                    enumerator = client.GetStreamingResponseAsync(messages, clientOptions, cancellationToken)
                                       .GetAsyncEnumerator(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Client {ClientDescription} failed during initialization. Moving to end of global list and falling back.", description);
                    ClientOrderService.MoveToEnd(description);
                    continue;
                }

                bool hasFailed = false;
                try
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        Task<bool> moveNextTask;
                        try
                        {
                            moveNextTask = enumerator.MoveNextAsync().AsTask();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error starting MoveNextAsync for {ClientDescription}. Moving to end of global list and falling back.", description);
                            ClientOrderService.MoveToEnd(description);
                            hasFailed = true;
                            break;
                        }

                        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(_streamingInactivityTimeoutMinutes), cancellationToken);
                        var completedTask = await Task.WhenAny(moveNextTask, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            _logger.LogWarning("Client {ClientDescription} timed out after {TimeoutMinutes} minutes of inactivity. Moving to end of global list and falling back.", description, _streamingInactivityTimeoutMinutes);
                            ClientOrderService.MoveToEnd(description);
                            hasFailed = true;
                            break;
                        }

                        bool hasMore;
                        try
                        {
                            hasMore = await moveNextTask;
                        }
                        catch (ClientResultException ex) when (ex.Status == 429)
                        {
                            _logger.LogWarning(ex, "Client {ClientDescription} failed with status 429 (Too Many Requests). Moving to end of global list and falling back.", description);
                            ClientOrderService.MoveToEnd(description);
                            hasFailed = true;
                            break;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogInformation("Operation was canceled by the caller.");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Client {ClientDescription} failed with an unexpected error during streaming. Moving to end of global list and falling back.", description);
                            ClientOrderService.MoveToEnd(description);
                            hasFailed = true;
                            break;
                        }

                        if (!hasMore) break;
                        yield return enumerator.Current;
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }

                if (!hasFailed)
                {
                    yield break;
                }
            }

            throw new InvalidOperationException("All configured chat clients failed to provide a streaming response.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(IChatClient))
            {
                return this;
            }
            _logger.LogDebug("GetService requested for type {ServiceType} with key {ServiceKey}, but no specific service is registered in ResilientChatClient.", serviceType, serviceKey);
            return null;
        }

        private Microsoft.Extensions.AI.ChatOptions CreateOptionsForClient(int clientIndex)
        {
           
            var clientConfig = _clientConfigs[clientIndex];
            var clientOptions = clientConfig?.Options ?? new();

            var chatOptions = clientOptions.Clone();

            return chatOptions;
        }

        private IList<AITool> FunctionFactory(List<ToolConfig> tools)
        {

           

            var aiTools = new List<AITool>();
            if (tools == null) return aiTools;

            foreach (var item in tools)
            {
                if (string.IsNullOrWhiteSpace(item.AssemblyQualifiedName) || string.IsNullOrWhiteSpace(item.Method))
                {
                    _logger.LogWarning("Skipping tool with missing AssemblyQualifiedName or FunctionName: {@ToolOptions}", item);
                    continue;
                }

                try
                {
                    MethodInfo toolMethodInfo = _delegateCache.GetOrAddMethodInfo(item.AssemblyQualifiedName, item.Method);

                    object? target = null;
                    if (!toolMethodInfo.IsStatic)
                    {
                        Type declaringType = toolMethodInfo.DeclaringType;
                        if (declaringType == null)
                        {
                            _logger.LogError("Could not determine DeclaringType for non-static method '{FunctionName}' in '{TypeName}'. Skipping tool.", item.Method, item.AssemblyQualifiedName);
                            continue;
                        }

                        try
                        {
                            target = _toolInstanceProvider.GetInstance(declaringType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to get instance of type '{DeclaringType}' for tool '{FunctionName}'. Skipping tool.", declaringType.FullName, item.Method);
                            continue;
                        }
                    }
                   

                    AITool aiTool = AIFunctionFactory.Create(toolMethodInfo, target, item.DescriptiveFunctionName, item.Description);
                  


                    aiTools.Add(aiTool);
                }
                catch (TypeLoadException ex)
                {
                    _logger.LogError(ex, "Failed to load type '{TypeName}' for tool '{FunctionName}'. Check AssemblyQualifiedName.", item.AssemblyQualifiedName, item.Method);
                }
                catch (MissingMethodException ex)
                {
                    _logger.LogError(ex, "Failed to find method '{FunctionName}' in type '{TypeName}' for tool. Check method name, parameters (if applicable), and BindingFlags.", item.Method, item.AssemblyQualifiedName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create or add tool '{FunctionName}' from type '{TypeName}' using MethodInfo.", item.Method, item.AssemblyQualifiedName);
                }
            }
            return aiTools;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var client in _clients)
            {
                if (client is IDisposable disposableClient)
                {
                    try
                    {
                        disposableClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing a chat client: {ClientDescription}", client.GetType().Name);
                    }
                }
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
