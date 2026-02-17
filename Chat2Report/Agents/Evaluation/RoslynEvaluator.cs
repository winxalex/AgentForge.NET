//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Dynamic;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.CodeAnalysis.CSharp.Scripting;
//using Microsoft.CodeAnalysis.Scripting;
//using Microsoft.CSharp.RuntimeBinder; // важно за dynamic

//// Custom класа
//public class Entity
//{
//    public string Name { get; set; }
//    public int Value { get; set; }
//}

//// Dynamic wrapper за Dictionary
//public class DynamicNode : DynamicObject
//{
//    private readonly Dictionary<string, object> _dict;
//    public DynamicNode(Dictionary<string, object> dict) => _dict = dict;

//    public override bool TryGetMember(GetMemberBinder binder, out object result)
//    {
//        if (_dict.TryGetValue(binder.Name, out var value))
//        {
//            result = Wrap(value);
//            return true;
//        }
//        result = null;
//        return true;
//    }

//    private static object Wrap(object value)
//    {
//        if (value is Dictionary<string, object> d) return new DynamicNode(d);
//        if (value is IList l) return new DynamicList(l);
//        return value;
//    }
//}

//// Dynamic wrapper за List со helper функции
//public class DynamicList : DynamicObject, IEnumerable
//{
//    private readonly IList _list;
//    public DynamicList(IList list) => _list = list;

//    public object this[int index] => Wrap(_list[index]);
//    public int Length => _list.Count;

//    public IEnumerator GetEnumerator() => _list.Cast<object>().Select(Wrap).GetEnumerator();

//    // Helper functions
//    public int SumField(string field)
//    {
//        int sum = 0;
//        foreach (var item in _list)
//        {
//            var val = item.GetType().GetProperty(field)?.GetValue(item);
//            if (val is int i) sum += i;
//        }
//        return sum;
//    }

//    public double AvgField(string field)
//    {
//        var values = _list.Cast<object>()
//            .Select(item => item.GetType().GetProperty(field)?.GetValue(item))
//            .OfType<int>();
//        return values.Any() ? values.Average() : 0;
//    }

//    public int MinField(string field)
//    {
//        var values = _list.Cast<object>()
//            .Select(item => item.GetType().GetProperty(field)?.GetValue(item))
//            .OfType<int>();
//        return values.Any() ? values.Min() : 0;
//    }

//    public int MaxField(string field)
//    {
//        var values = _list.Cast<object>()
//            .Select(item => item.GetType().GetProperty(field)?.GetValue(item))
//            .OfType<int>();
//        return values.Any() ? values.Max() : 0;
//    }

//    private static object Wrap(object value)
//    {
//        if (value is Dictionary<string, object> d) return new DynamicNode(d);
//        if (value is IList l) return new DynamicList(l);
//        return value;
//    }
//}

//// Globals за Roslyn
//public class Globals
//{
//    public dynamic state; // dynamic за dot-notation
//}

//// Roslyn boolean evaluator
//public class RoslynEvaluator
//{
//    private readonly ScriptOptions _options;
//    private readonly Globals _globals;

//    public RoslynEvaluator(dynamic state)
//    {
//        _globals = new Globals { state = state };
//        _options = ScriptOptions.Default
//            .AddReferences(
//                typeof(object).Assembly,
//                typeof(Enumerable).Assembly,
//                typeof(Binder).Assembly // Microsoft.CSharp.RuntimeBinder
//            )
//            .AddImports("System", "System.Collections.Generic", "System.Linq");
//    }

//    public async Task<bool> EvalBoolAsync(string expression)
//    {
//        return await CSharpScript.EvaluateAsync<bool>(expression, _options, _globals);
//    }
//}

//// Main program
//class Program
//{
//    static async Task Main()
//    {
//        var stateDict = new Dictionary<string, object>
//        {
//            { "user", new Dictionary<string, object>
//                {
//                    { "name", "Alex" },
//                    { "roles", new List<object> { "Admin", "Editor" } },
//                    { "entities", new List<object>
//                        {
//                            new Entity { Name = "E1", Value = 10 },
//                            new Entity { Name = "E2", Value = 20 },
//                            new Entity { Name = "E3", Value = 30 }
//                        }
//                    }
//                }
//            },
//            { "threshold", 50 }
//        };

//        dynamic dynamicState = new DynamicNode(stateDict);
//        var evaluator = new RoslynEvaluator(dynamicState);


//        bool nestedCheck = await evaluator.EvalBoolAsync(
//           "state.user.roles.Length > 0 && state.user.entities.SumField(\"Value\") > state.threshold && state.user.entities[0].Value > 5"
//       );
//        Console.WriteLine($"nestedCheck = {nestedCheck}"); // true

//        Console.WriteLine($"nestedCheck = {nestedCheck}"); // true

//        try
//        {
//            bool sumOk = await evaluator.EvalBoolAsync("state.user.entities.SumField(\"Value\") > state.threshold");
//            Console.WriteLine($"sumOk = {sumOk}"); // true
//        }
//        catch
//        {
//            Console.WriteLine($"greska"); // true
//        }


//        evaluator = new RoslynEvaluator(dynamicState);



//        // --- Чисти, читливи boolean expressions ---
//        bool rolesExist = await evaluator.EvalBoolAsync("state.user.roles.Length > 0");
//        Console.WriteLine($"rolesExist = {rolesExist}"); // true



//        bool avgOk = await evaluator.EvalBoolAsync("state.user.entities.AvgField(\"Value\") > 15");
//        Console.WriteLine($"avgOk = {avgOk}"); // true

//        bool minOk = await evaluator.EvalBoolAsync("state.user.entities.MinField(\"Value\") == 10");
//        Console.WriteLine($"minOk = {minOk}"); // true

//        bool maxOk = await evaluator.EvalBoolAsync("state.user.entities.MaxField(\"Value\") == 30");
//        Console.WriteLine($"maxOk = {maxOk}"); // true


//    }
//}
