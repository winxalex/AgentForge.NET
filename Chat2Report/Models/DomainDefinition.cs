using Microsoft.Extensions.VectorData;

namespace Chat2Report.Models
{
    public class DomainDefinition
    {
        [VectorStoreRecordKey]
        public ulong Id { get; set; }

        [VectorStoreRecordData]
        public string Name { get; set; }

        [VectorStoreRecordData]
        public string Description { get; set; }

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }

        [VectorStoreRecordData(IsFilterable = true)]
        public List<string> TableNames { get; set; } = new List<string>();

        [VectorStoreRecordData(IsFilterable = true)]
        public List<string> ViewNames { get; set; } = new List<string>();
    }
}