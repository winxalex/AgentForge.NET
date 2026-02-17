// wwwroot/lib/interop.js

window.blazorInterop = {
    dotNetHelper: null,

    // Blazor ќе ја повика оваа функција за да се "регистрира"
    setDotNetHelper: function (helper) {
        this.dotNetHelper = helper;
    },

    // Оваа функција ќе биде повикана од нашиот динамичен HTML
    invokeCSharp: function (action) {
        if (this.dotNetHelper) {
            // Ја повикуваме генеричката C# метода 'InvokeMethodFromJs'
            // и и ги проследуваме името на методата и параметрите
            this.dotNetHelper.invokeMethodAsync(
                'InvokeMethodFromJs',          // Фиксно име на C# методата-раскрсница
                action.methodName,             // Име на вистинската метода што сакаме да ја пуштиме
                JSON.stringify(action.parameters) // Параметрите како JSON стринг
            );
        } else {
            console.error("Blazor .NET helper not initialized.");
        }
    }
};