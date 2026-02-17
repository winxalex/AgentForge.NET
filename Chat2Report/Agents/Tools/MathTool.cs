namespace Chat2Report.Tools
{

    using Microsoft.Extensions.AI;
    using System.ComponentModel;

    public class MathTool
    {
        // Ова е едноставна алатка без DI, но може да има зависности
        private readonly ILogger<MathTool> _logger;
        public MathTool(ILogger<MathTool> logger) => _logger = logger;

        [Description] // Овој атрибут е потребен за автоматско генерирање на шема
        public int Add([Description("The first number.")] int a, [Description("The second number.")] int b)
        {
            _logger.LogInformation("Executing Add with {A} and {B}", a, b);
            return a + b;
        }

        [Description]
        public int Subtract([Description("The first number.")] int a, [Description("The second number.")] int b)
        {
            _logger.LogInformation("Executing Add with {A} and {B}", a, b);
            return a - b;
        }

        [Description("Multiply a with b")]
        public int Multiply([Description("The first number.")] int a, [Description("The second number.")] int b)
        {
            _logger.LogInformation("Executing Add with {A} and {B}", a, b);
            return a * b;
        }
    }
}