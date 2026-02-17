namespace Chat2Report.Models
{
    /// <summary>
    /// Represents a candidate domain found during the initial vector search, along with its relevance score.
    /// </summary>
    public class CandidateDomainWithScores
    {
        public DomainDefinition Domain { get; set; }
        public double Score { get; set; }
    }
}