using Chat2Report.Models;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Генерички договор кој секој типски-безбеден workflow чекор мора да го имплементира.
    /// Source Generator-от ќе креира TInput и TOutput класи.
    /// </summary>
    public interface IWorkflowStep<TInput, TOutput> : IBaseWorkflowStep
     where TInput : class
     where TOutput : class
    {
        /// <summary>
        /// Го извршува чекорот со силно-типизирани влезни податоци и враќа
        /// силно-типизиран резултат.
        /// </summary>
        Task<TOutput> ExecuteAsync(TInput inputs, IStepExecutionContext context, CancellationToken cancellationToken);

    }
}
