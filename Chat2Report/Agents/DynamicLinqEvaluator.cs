//// --- In a new file: IExpressionEvaluator.cs ---
//using System.Linq.Dynamic.Core;
//using System.Linq.Expressions;




//// --- In a new file: DynamicLinqEvaluator.cs ---

//namespace Chat2Report.Agents.Evaluation
//{
//    public class DynamicLinqEvaluator : IExpressionEvaluator
//    {
//        private readonly ILogger<DynamicLinqEvaluator> _logger;

//        public DynamicLinqEvaluator(ILogger<DynamicLinqEvaluator> _logger = null)
//        {
//            _logger = _logger;
//        }

//        public bool Evaluate(string expression, Dictionary<string, object> message, IDictionary<string, object> sharedMemory)
//        {
//            if (string.IsNullOrWhiteSpace(expression) || expression.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
//            {
//                // No condition or always true
//                return true;
//            }

//            try
//            {
//                // Define parameters for the dynamic expression
//                var messageParam = Expression.Parameter(typeof(Dictionary<string, object>), "message");
//                var memoryParam = Expression.Parameter(typeof(IDictionary<string, object>), "sharedMemory");

//                // Parse the expression using the parameters
//                var lambda = DynamicExpressionParser.ParseLambda(
//                    new[] { messageParam, memoryParam },
//                    typeof(bool), // Expected return type
//                    expression);

//                // Compile and invoke the expression
//                var func = (Func<Dictionary<string, object>, IDictionary<string, object>, bool>)lambda.Compile();
//                return func(message, sharedMemory);
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "Error evaluating condition expression: {Expression}", expression);
//                // Default to false if evaluation fails
//                return false;
//            }
//        }
//    }
//}