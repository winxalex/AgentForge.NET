using Chat2Report.Models;

namespace Chat2Report.Options
{
    public class ValueMatcherОptions
    {
        public ClientConfig EmbeddingClient { get; set; }
        public double GoodValueMatchThreshold { get; set; } = 0.36;
    }
}