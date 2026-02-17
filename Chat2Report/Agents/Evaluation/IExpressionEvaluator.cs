﻿﻿﻿namespace Chat2Report.Agents.Evaluation
{
    public interface IExpressionEvaluator
    {
        /// <summary>
        /// Evaluates an expression against a JSON string and returns a boolean result.
        /// Ideal for conditional checks within templates.
        /// </summary>
        Task<bool> EvaluateAsBoolean(string expression, string json,CancellationToken cancellationToken=default);

        /// <summary>
        /// Evaluates an expression against a JSON string and returns the result as a new string.
        /// This is perfect for data transformations (e.g., returning a transformed JSON).
        /// </summary>
        Task<string> EvaluateAsString(string expression, string json, CancellationToken cancellationToken=default);

        /// <summary>
        /// Evaluates an expression against a dictionary and returns a generic object result.
        /// </summary>
        Task<object?> Evaluate(string expression, IReadOnlyDictionary<string, object> context, CancellationToken cancellationToken);
    }
}
