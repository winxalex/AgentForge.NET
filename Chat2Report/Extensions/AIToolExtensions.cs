using Microsoft.Extensions.AI;
using System.Text;

namespace Chat2Report.Extensions
{
    public static class AIToolExtensions
    {
        public static string ToDetailString(this AITool tool)
        {
            StringBuilder stringBuilder = new StringBuilder(tool.Name);
            string description = tool.Description;
            if (description != null && !string.IsNullOrEmpty(description))
            {
                stringBuilder.Append(" (").Append(description).Append(')');
            }

            foreach (KeyValuePair<string, object> additionalProperty in tool.AdditionalProperties)
            {
                stringBuilder.Append(", ").Append(additionalProperty.Key).Append(" = ")
                    .Append(additionalProperty.Value);
            }

            return stringBuilder.ToString();
        }
    }

}
