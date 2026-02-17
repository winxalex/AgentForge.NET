//using Chat2Report.Utilities;
//using Chat2Report.Agents.Transformers;
//using Chat2Report.Models;
//using Xunit;
//using FluentAssertions;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System.Text.Json.Serialization;

//namespace Chat2Report.Tests.Utilities;

//// ============================================================
//// UNIT TESTS ЗА SafeJsonSerializer
//// ============================================================

//public class SafeJsonSerializerTests
//{
//    private readonly SerializationHelper _helper;
//    private readonly Mock<ILogger> _mockLogger;

//    public SafeJsonSerializerTests()
//    {
//        _mockLogger = new Mock<ILogger>();
//        _helper = new SerializationHelper();
//    }

//    [Fact]
//    public void Serialize_SimpleTypes_SerializesCorrectly()
//    {
//        // Arrange
//        var state = new Dictionary<string, object>
//        {
//            ["string"] = "test",
//            ["number"] = 42,
//            ["decimal"] = 3.14m,
//            ["bool"] = true,
//            ["date"] = DateTime.Parse("2024-01-01")
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper, _mockLogger.Object);

//        // Assert
//        json.Should().Contain("\"string\":\"test\"");
//        json.Should().Contain("\"number\":42");
//        json.Should().Contain("\"decimal\":3.14");
//        json.Should().Contain("\"bool\":true");
//    }

//    [Fact]
//    public void Serialize_WithIgnoredTypes_ExcludesThem()
//    {
//        // Arrange
//        var state = new Dictionary<string, object>
//        {
//            ["data"] = "value",
//            ["stream"] = new MemoryStream() // Should be ignored
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper);

//        // Assert
//        json.Should().Contain("data");
//        json.Should().NotContain("stream");
//    }

//    [Fact]
//    public void Serialize_WithCustomStrategy_UsesStrategy()
//    {
//        // Arrange
//        _helper.RegisterStrategy(typeof(CustomClass), obj =>
//        {
//            var custom = (CustomClass)obj;
//            return new Dictionary<string, object>
//            {
//                ["ModifiedName"] = custom.Name + "_custom"
//            };
//        });

//        var state = new Dictionary<string, object>
//        {
//            ["custom"] = new CustomClass { Name = "Test" }
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper);

//        // Assert
//        json.Should().Contain("ModifiedName");
//        json.Should().Contain("Test_custom");
//    }

//    [Fact]
//    public void Serialize_NestedObjects_HandlesCorrectly()
//    {
//        // Arrange
//        var state = new Dictionary<string, object>
//        {
//            ["nested"] = new NestedClass
//            {
//                Name = "Parent",
//                Child = new CustomClass { Name = "Child" }
//            }
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper);

//        // Assert
//        json.Should().Contain("Parent");
//        json.Should().Contain("Child");
//    }

//    [Fact]
//    public void Serialize_Collections_HandlesCorrectly()
//    {
//        // Arrange
//        var state = new Dictionary<string, object>
//        {
//            ["list"] = new List<string> { "a", "b", "c" },
//            ["array"] = new[] { 1, 2, 3 }
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper);

//        // Assert
//        json.Should().Contain("\"list\":[\"a\",\"b\",\"c\"]");
//        json.Should().Contain("\"array\":[1,2,3]");
//    }

//    [Fact]
//    public void Serialize_NestedCollections_HandlesCorrectly()
//    {
//        // Arrange
//        var state = new Dictionary<string, object>
//        {
//            ["complex"] = new List<CustomClass>
//            {
//                new CustomClass { Name = "Item1" },
//                new CustomClass { Name = "Item2" }
//            }
//        };

//        // Act
//        var json = SafeJsonSerializer.Serialize(state, _helper);

//        // Assert
//        json.Should().Contain("Item1");
//        json.Should().Contain("Item2");
//    }

//    // Test models
//    private class CustomClass
//    {
//        public string Name { get; set; } = string.Empty;
//    }

//    private class NestedClass
//    {
//        public string Name { get; set; } = string.Empty;
//        public CustomClass? Child { get; set; }
//    }
//}

//// ============================================================
//// UNIT TESTS ЗА SerializationHelper
//// ============================================================

//public class SerializationHelperTests
//{
//    [Fact]
//    public void GetSerializableView_SimpleTypes_ReturnsSameValue()
//    {
//        // Arrange
//        var helper = new SerializationHelper();

//        // Act & Assert
//        helper.GetSerializableView(42).Should().Be(42);
//        helper.GetSerializableView("text").Should().Be("text");
//        helper.GetSerializableView(3.14m).Should().Be(3.14m);
//        helper.GetSerializableView(true).Should().Be(true);
//    }

//    [Fact]
//    public void GetSerializableView_IgnoredTypes_ReturnsNull()
//    {
//        // Arrange
//        var helper = new SerializationHelper();

//        // Act & Assert
//        helper.GetSerializableView(new MemoryStream()).Should().BeNull();
//        helper.GetSerializableView(Task.CompletedTask).Should().BeNull();
//        helper.GetSerializableView(CancellationToken.None).Should().BeNull();
//    }

//    [Fact]
//    public void GetSerializableView_WithCustomStrategy_UsesStrategy()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        helper.RegisterStrategy(typeof(TestClass), obj =>
//        {
//            var test = (TestClass)obj;
//            return new Dictionary<string, object> { ["Custom"] = test.Value * 2 };
//        });

//        var testObj = new TestClass { Value = 10 };

//        // Act
//        var result = helper.GetSerializableView(testObj);

//        // Assert
//        result.Should().BeOfType<Dictionary<string, object>>();
//        var dict = result as Dictionary<string, object>;
//        dict.Should().ContainKey("Custom").WhoseValue.Should().Be(20);
//    }

//    [Fact]
//    public void GetSerializableView_ComplexObject_ConvertsToDictionary()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var obj = new TestClass
//        {
//            Value = 123,
//            Text = "Test"
//        };

//        // Act
//        var result = helper.GetSerializableView(obj);

//        // Assert
//        result.Should().BeOfType<Dictionary<string, object>>();
//        var dict = result as Dictionary<string, object>;
//        dict.Should().ContainKey("Value").WhoseValue.Should().Be(123);
//        dict.Should().ContainKey("Text").WhoseValue.Should().Be("Test");
//    }

//    [Fact]
//    public void GetSerializableView_WithJsonIgnoreProperty_SkipsProperty()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var obj = new ClassWithIgnoredProperty
//        {
//            PublicProperty = "visible",
//            IgnoredProperty = "hidden"
//        };

//        // Act
//        var result = helper.GetSerializableView(obj);

//        // Assert
//        var dict = result as Dictionary<string, object>;
//        dict.Should().ContainKey("PublicProperty");
//        dict.Should().NotContainKey("IgnoredProperty");
//    }

//    [Fact]
//    public void GetSerializableView_NestedIgnoredTypes_RemovesThem()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var obj = new ComplexTestClass
//        {
//            Name = "Test",
//            ValidChild = new TestClass { Value = 42 },
//            InvalidChild = new MemoryStream() // Should be removed
//        };

//        // Act
//        var result = helper.GetSerializableView(obj);

//        // Assert
//        var dict = result as Dictionary<string, object>;
//        dict.Should().ContainKey("Name");
//        dict.Should().ContainKey("ValidChild");
//        dict.Should().NotContainKey("InvalidChild"); // Ignored type
//    }

//    [Fact]
//    public void GetSerializableView_List_CleansElements()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var list = new List<object>
//        {
//            "valid string",
//            42,
//            new MemoryStream() // Should be removed
//        };

//        // Act
//        var result = helper.GetSerializableView(list);

//        // Assert
//        result.Should().BeOfType<List<object>>();
//        var cleanList = result as List<object>;
//        cleanList.Should().HaveCount(3);
//        cleanList![0].Should().Be("valid string");
//        cleanList[1].Should().Be(42);
//        cleanList[2].Should().BeNull(); // Ignored type becomes null
//    }

//    [Fact]
//    public void GetSerializableView_Dictionary_CleansValues()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var dict = new Dictionary<string, object>
//        {
//            ["valid"] = "value",
//            ["invalid"] = new MemoryStream()
//        };

//        // Act
//        var result = helper.GetSerializableView(dict);

//        // Assert
//        result.Should().BeOfType<Dictionary<string, object>>();
//        var cleanDict = result as Dictionary<string, object>;
//        cleanDict.Should().ContainKey("valid");
//        cleanDict!["invalid"].Should().BeNull(); // Ignored type
//    }

//    // Test models
//    private class TestClass
//    {
//        public int Value { get; set; }
//        public string Text { get; set; } = string.Empty;
//    }

//    private class ClassWithIgnoredProperty
//    {
//        public string PublicProperty { get; set; } = string.Empty;

//        [JsonIgnore]
//        public string IgnoredProperty { get; set; } = string.Empty;
//    }

//    private class ComplexTestClass
//    {
//        public string Name { get; set; } = string.Empty;
//        public TestClass? ValidChild { get; set; }
//        public Stream? InvalidChild { get; set; }
//    }
//}

//// ============================================================
//// INTEGRATION TESTS СО JsonataTransformer
//// ============================================================

//public class JsonataTransformerIntegrationTests
//{
//    [Fact]
//    public async Task TransformAsync_SimpleExpression_WorksCorrectly()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var transformer = new JsonataTransformer(helper);

//        var state = new Dictionary<string, object>
//        {
//            ["name"] = "John",
//            ["age"] = 30
//        };

//        var options = new TransformOptions
//        {
//            Expression = "{'fullName': $name, 'years': $age}"
//        };

//        // Act
//        var result = await transformer.TransformAsync(
//            options,
//            state,
//            null,
//            CancellationToken.None);

//        // Assert
//        result.Should().ContainKey("fullName").WhoseValue.Should().Be("John");
//        result.Should().ContainKey("years").WhoseValue.Should().Be(30);
//    }

//    [Fact]
//    public async Task TransformAsync_WithComplexObject_UsesSerializationHelper()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        helper.RegisterStrategy(typeof(ChartContent), obj =>
//        {
//            var chart = (ChartContent)obj;
//            return new Dictionary<string, object>
//            {
//                ["Type"] = chart.Type,
//                ["Library"] = chart.Library
//            };
//        });

//        var transformer = new JsonataTransformer(helper);

//        var state = new Dictionary<string, object>
//        {
//            ["chart"] = new ChartContent
//            {
//                Type = "pie",
//                Library = "Mermaid"
//            }
//        };

//        var options = new TransformOptions
//        {
//            Expression = "{'chartType': $chart.Type, 'lib': $chart.Library}"
//        };

//        // Act
//        var result = await transformer.TransformAsync(
//            options,
//            state,
//            null,
//            CancellationToken.None);

//        // Assert
//        result.Should().ContainKey("chartType").WhoseValue.Should().Be("pie");
//        result.Should().ContainKey("lib").WhoseValue.Should().Be("Mermaid");
//    }

//    [Fact]
//    public async Task TransformAsync_OnlySerializesInvolvedKeys()
//    {
//        // Arrange
//        var helper = new SerializationHelper();
//        var transformer = new JsonataTransformer(helper);

//        var state = new Dictionary<string, object>
//        {
//            ["key1"] = "value1",
//            ["key2"] = "value2",
//            ["key3"] = "value3", // Not used in expression
//            ["stream"] = new MemoryStream() // Not used and not serializable
//        };

//        var options = new TransformOptions
//        {
//            Expression = "{'first': $key1, 'second': $key2}"
//        };

//        // Act
//        var result = await transformer.TransformAsync(
//            options,
//            state,
//            null,
//            CancellationToken.None);

//        // Assert
//        result.Should().ContainKey("first").WhoseValue.Should().Be("value1");
//        result.Should().ContainKey("second").WhoseValue.Should().Be("value2");
//        // key3 and stream should not have been serialized
//    }

//    // Test model
//    private class ChartContent
//    {
//        public string Type { get; set; } = string.Empty;
//        public string Library { get; set; } = string.Empty;
//    }
//}