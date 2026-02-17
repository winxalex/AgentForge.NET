using Chat2Report.Models;
using Chat2Report.Providers;
using System.Threading.Tasks;

namespace Chat2Report.Agents.Transformers
{
    public interface IMessageTransformer
    {
        Task<Dictionary<string, object>> TransformAsync(TransformOptions options, Dictionary<string,object> messsage, IStepExecutionContext transformContext,CancellationToken cancellationToken);
    }
}