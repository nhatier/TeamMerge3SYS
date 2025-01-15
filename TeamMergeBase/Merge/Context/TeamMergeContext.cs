using Domain.Entities;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamMergeBase.Merge.Context
{
    public class TeamMergeContext
    {
        public string SourceBranch { get; set; }

        public IEnumerable<string> TargetBranches { get; set; }

        public string SelectedProjectName { get; set; }

        public ObservableCollection<Changeset> Changesets { get; set; }
    }
}