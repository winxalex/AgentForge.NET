//using Chat2Report.Agents.Generated.IO.Transform; // Генериран namespace
//using Chat2Report.Agents.Transformers;
//using Chat2Report.Models;
//using SmartFormat.Core.Output;
//using System.Collections.Generic;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Chat2Report.Agents.WorkflowSteps
//{
//    /// <summary>
//    /// Извршува JSONata трансформација врз податоците од состојбата.
//    /// Ова е моќен, генерички чекор за манипулација на податоци.
//    /// </summary>
//    public class MessageTransformerStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
//    {
//        private readonly IMessageTransformer _transformerExecutor;
//        private readonly ILogger<MessageTransformerStep> _logger;
//        // TransformOptions е класата што содржи 'Expression'
//        private TransformOptions _options = new();

//        public MessageTransformerStep(IMessageTransformer transformerExecutor, ILogger<MessageTransformerStep> logger)
//        {
//            _transformerExecutor = transformerExecutor;
//            _logger = logger;
//        }

//        public void Configure(JsonElement config)
//        {
//            _options = config.Deserialize<TransformOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
//                       ?? throw new ArgumentException("Invalid 'Config' for MessageTransformerStep.");
//        }

//        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
//        {
//            if (string.IsNullOrWhiteSpace(_options.Expression))
//            {
//                throw new InvalidOperationException("'Expression' was not provided in the 'Config' section for the Transform step.");
//            }

//            try
//            {
//                _logger.LogDebug("Applying JSONata transformation: {Expression}", _options.Expression);

//                var transformedData = await _transformerExecutor.TransformAsync(
//                    _options,
//                    inputs.CurrentStateData, // Го користиме влезот од IOContract
//                    null, // IStatePersitanceProvider - застарено
//                    context,
//                    cancellationToken).ConfigureAwait(false);

//                // Го враќаме трансформираниот речник
//                return new Outputs { TransformedData = transformedData };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "MessageTransformerStep failed with expression: {Expression}", _options.Expression);
//                throw; // Препрати го исклучокот на UniversalAgent
//            }
//        }
//    }
//}