using System.Collections.Generic;
using System.Linq;

namespace Domain.Entities
{
    public class DefaultMergeSettings
    {
        public string Solution { get; set; }
        public string ProjectName { get; set; }
        public string SourceBranch { get; set; }
        public IEnumerable<string> TargetBranches { get; set; }

        public bool IsValidSettings()
        {
            return !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(SourceBranch) && (TargetBranches != null) && (TargetBranches.Any());
        }
    }
}