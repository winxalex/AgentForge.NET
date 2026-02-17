﻿// SchemaProcessingSettings.cs
using Chat2Report.Models;
using System.Collections.Generic;

namespace Chat2Report.Options
{
    public class SchemaProcessingSettings
    {
        public List<DomainConfig> Domains { get; set; } = new List<DomainConfig>();
        public List<string> SchemasToScan { get; set; } = new List<string>();
        public string TableDescriptionProperty { get; set; } = "MS_Description_EN";
        public string ViewDescriptionProperty { get; set; } = "MS_Description_EN";
        public string ColumnDescriptionProperty { get; set; } = "MS_Description_EN";
        public string FunctionDescriptionProperty { get; set; } = "MS_Description_EN";
        public string DomainCollectionName { get; set; } = "AllDomains";
        public string TableCollectionName { get; set; } = "AllTableDefinitions";
        public string ViewCollectionName { get; set; } = "AllViewDefinitions";
        public string ColumnCollectionName { get; set; } = "AllColumnDefinitions";
        public int DomainSearchTopK { get; set; } = 3;
        public double DomainSearchScoreThreshold { get; set; } = 1.0;
        public string DescriptionPropertiesBackupFileName { get; set; } = "ExtendedPropertiesBackup.sql";
    }
}