//using Xunit;
//using Moq;
//using Microsoft.Extensions.Logging;
//using Chat2Report.Agents.Transformers;
//using Chat2Report.Models;
//using Chat2Report.Utilities;
//using Chat2Report.Extensions;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using Jsonata.Net.Native.Json; // For JToken
//using System;

//namespace Chat2Report.Tests
//{
//    public class JsonataTransformerTests
//    {
//        private readonly ISerializationHelper _serializationHelper;
//        private readonly Mock<ILogger<JsonataTransformer>> _mockLogger;
//        private readonly JsonataTransformer _transformer;

//        public JsonataTransformerTests()
//        {
//            // Користиме вистински SerializationHelper за правилно да го тестираме целиот процес на серијализација,
//            // кој го вклучува и SafeJsonSerializer, како што е предвидено.
//            var serializationHelperLogger = new Mock<ILogger<SerializationHelper>>().Object;
//            _serializationHelper = new SerializationHelper(serializationHelperLogger);
//            _mockLogger = new Mock<ILogger<JsonataTransformer>>();
//            _transformer = new JsonataTransformer(_serializationHelper, _mockLogger.Object);
//        }

//        [Fact]
//        public async Task TransformAsync_ExtractsAndFiltersKeysCorrectly_ForBoundVariablesAndDataKeys()
//        {
//            // ARRANGE
//            var expression = "{ 'boundVarResult': $myBoundVar, 'dataKeyResult': $.myRootData.value, 'directDataResult': directRootData.anotherValue }";
//            var state = new Dictionary<string, object>
//            {
//                { "myBoundVar", 123 },
//                { "myRootData", new Dictionary<string, object> { { "value", "hello" } } },
//                { "directRootData", new Dictionary<string, object> { { "anotherValue", true } } },
//                { "unrelatedKey", "should_be_ignored" }
//            };

//            var options = new TransformOptions { Expression = expression };
//            var context = new Mock<IStepExecutionContext>().Object;
//            var cancellationToken = CancellationToken.None;

//            // ACT
//            var result = await _transformer.TransformAsync(options, state, context, cancellationToken);

//            // ASSERT
//            Assert.NotNull(result);
//            Assert.True(result.ContainsKey("boundVarResult"));
//            Assert.Equal(123L, result["boundVarResult"]); // JSONata often converts numbers to long
//            Assert.True(result.ContainsKey("dataKeyResult"));
//            Assert.Equal("hello", result["dataKeyResult"]);
//            Assert.True(result.ContainsKey("directDataResult"));
//            Assert.Equal(true, result["directDataResult"]);

//            // Verify that the correct bound variable was detected and logged
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Detected bound variable reference: $myBoundVar")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);

//            // Verify that the correct data keys were detected and logged
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Detected root data property reference via '$.': myRootData")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);

//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Detected root data property reference at start of expression: directRootData")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);

//            // Verify that only relevant data keys were serialized for the JSONata data input
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Serialized state for JSONata:") &&
//                                                  v.ToString().Contains("myRootData") &&
//                                                  v.ToString().Contains("directRootData") &&
//                                                  !v.ToString().Contains("myBoundVar") && // Should not be in the serialized JSON data
//                                                  !v.ToString().Contains("unrelatedKey")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);

//            // Verify that only relevant bound variables were bound to the environment
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Bound variable $myBoundVar")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);

//            // Verify that data keys were NOT bound as variables
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Trace,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Bound variable $myRootData")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Never);
//        }

//        [Fact]
//        public async Task TransformAsync_HandlesMissingKeysGracefully()
//        {
//            // ARRANGE
//            var expression = "{ 'boundVarResult': $missingBoundVar, 'dataKeyResult': $.missingData.value }";
//            var state = new Dictionary<string, object>
//            {
//                { "existingKey", "some_value" }
//            };

//            var options = new TransformOptions { Expression = expression };
//            var context = new Mock<IStepExecutionContext>().Object;
//            var cancellationToken = CancellationToken.None;

//            // ACT
//            var result = await _transformer.TransformAsync(options, state, context, cancellationToken);

//            // ASSERT
//            Assert.NotNull(result);
//            Assert.True(result.ContainsKey("boundVarResult"));
//            Assert.Null(result["boundVarResult"]); // JSONata returns null for undefined variables
//            Assert.True(result.ContainsKey("dataKeyResult"));
//            Assert.Null(result["dataKeyResult"]); // JSONata returns null for undefined properties

//            // Verify warnings for missing keys
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Warning,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Referenced key 'missingBoundVar' not found in state.")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);
//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Warning,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Referenced key 'missingData' not found in state.")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);
//        }

//        [Fact]
//        public async Task TransformAsync_ThrowsArgumentException_WhenExpressionIsEmpty()
//        {
//            // ARRANGE
//            var state = new Dictionary<string, object>();
//            var options = new TransformOptions { Expression = "" };
//            var context = new Mock<IStepExecutionContext>().Object;
//            var cancellationToken = CancellationToken.None;

//            // ACT & ASSERT
//            await Assert.ThrowsAsync<ArgumentException>(() =>
//                _transformer.TransformAsync(options, state, context, cancellationToken));
//        }

//        [Fact]
//        public async Task TransformAsync_ThrowsInvalidOperationException_OnJsonataExecutionFailure()
//        {
//            // ARRANGE
//            // Create an expression that will cause a JSONata error (e.g., trying to divide by zero or invalid syntax)
//            var expression = "1 / 0"; // This should cause an error in JSONata
//            var state = new Dictionary<string, object>();
//            var options = new TransformOptions { Expression = expression };
//            var context = new Mock<IStepExecutionContext>().Object;
//            var cancellationToken = CancellationToken.None;

//            // ACT & ASSERT
//            await Assert.ThrowsAsync<InvalidOperationException>(() =>
//                _transformer.TransformAsync(options, state, context, cancellationToken));

//            _mockLogger.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JSONata query execution failed.")),
//                    It.IsAny<Exception>(),
//                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
//                Times.Once);
//        }

//    //    [Fact]
//    //    public async Task TransformAsync_HandlesComplexExpressionWithCorrectCasing_Successfully()
//    //    {
//    //        // ARRANGE
//    //        var expression = "{'response': 'Задачата е извршена успешно.', 'chart_context': {'resolved_user_query': resolved_user_query, 'sql_result_key': sql_result_key, 'relevant_columns': user_analysis.RelevantColumns,'relevant_domains':relevant_domains}}";

//    //        // Simulate the state that would be passed to the transformer
//    //        // using the actual UserQueryAnalysis object for better accuracy.
//    //        var state = new Dictionary<string, object>
//    //        {
//    //            { "resolved_user_query", "Покажи ми ги сите активни задачи" },
//    //            { "sql_result_key", "some-cache-key-123" },
//    //            { 
//    //                "user_analysis", new UserQueryAnalysis
//    //                {
//    //                    RelevantColumns = new List<Dictionary<string, object>>
//    //                    {
//    //                        new() { { "name", "TaskName" }, { "description", "Name of the task" } },
//    //                        new() { { "name", "Status" }, { "description", "Current status" } },
//    //                        new() { { "name", "AssignedTo" }, { "description", "Person assigned" } }
//    //                    },
//    //                    UserQuery = "Покажи ми ги сите активни задачи"
//    //                }
//    //            },
//    //            { "relevant_domains", new List<string> { "Helpdesk", "Tasks" } },
//    //            { "unrelated_data", "this should not appear in the result" }
//    //        };

//    //        var options = new TransformOptions { Expression = expression };
//    //        var context = new Mock<IStepExecutionContext>().Object;
//    //        var cancellationToken = CancellationToken.None;

//    //        // ACT
//    //        var result = await _transformer.TransformAsync(options, state, context, cancellationToken);

//    //        // ASSERT
//    //        Assert.NotNull(result);
//    //        Assert.True(result.ContainsKey("response"));
//    //        Assert.Equal("Задачата е извршена успешно.", result["response"]);

//    //        Assert.True(result.ContainsKey("chart_context"));
//    //        var chartContext = Assert.IsType<Dictionary<string, object>>(result["chart_context"]);

//    //        Assert.True(chartContext.ContainsKey("resolved_user_query"));
//    //        Assert.Equal("Покажи ми ги сите активни задачи", chartContext["resolved_user_query"]);

//    //        Assert.True(chartContext.ContainsKey("sql_result_key"));
//    //        Assert.Equal("some-cache-key-123", chartContext["sql_result_key"]);

//    //        Assert.True(chartContext.ContainsKey("relevant_columns"));
//    //        var relevantColumnsList = Assert.IsType<List<object>>(chartContext["relevant_columns"]);
//    //        Assert.Equal(3, relevantColumnsList.Count);
//    //        var columnNames = relevantColumnsList.Select(o => ((Dictionary<string, object>)o)["name"].ToString()).ToList();
//    //        Assert.Contains("TaskName", columnNames);
//    //        Assert.Contains("Status", columnNames);
//    //        Assert.Contains("AssignedTo", columnNames);

//    //        Assert.True(chartContext.ContainsKey("relevant_domains"));
//    //        var relevantDomains = Assert.IsType<List<object>>(chartContext["relevant_domains"]);
//    //        Assert.Contains("Helpdesk", relevantDomains);
//    //        Assert.Contains("Tasks", relevantDomains);
//    //    }
//    }
//}