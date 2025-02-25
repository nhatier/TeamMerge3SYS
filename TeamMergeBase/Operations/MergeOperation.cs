﻿using Domain.Entities;
using Logic.Helpers;
using Logic.Services;
using Shared.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using TeamMergeBase.Exceptions;
using TeamMergeBase.Helpers;
using TeamMergeBase.Settings.Enums;
using Branch = TeamMergeBase.Settings.Enums.Branch;

namespace TeamMergeBase.Operations
{
    public interface IMergeOperation
    {
        event EventHandler<string> MyCurrentAction;
        Task ExecuteAsync(MergeModel mergeModel);
    }

    public class MergeOperation
        : IMergeOperation
    {
        private readonly IMergeService _mergeService;
        private readonly ITeamExplorerService _teamExplorerService;
        private readonly IConfigManager _configManager;

        public MergeOperation(IMergeService mergeService, ITeamExplorerService teamExplorerService, IConfigManager configManager)
        {
            _mergeService = mergeService;
            _teamExplorerService = teamExplorerService;
            _configManager = configManager;
        }

        public event EventHandler<string> MyCurrentAction;

        public async Task ExecuteAsync(MergeModel mergeModel)
        {
            await CheckIfWorkspaceHasIncludedPendingChangesAsync(mergeModel.WorkspaceModel);

            var sourceBranch = mergeModel.SourceBranch + mergeModel.SubPath;
            var targetBranches = mergeModel.TargetBranches.Select((b) => b + mergeModel.SubPath).ToList();
            await DoGetLatestOnBranchAsync(mergeModel.WorkspaceModel, sourceBranch, targetBranches);

            SetCurrentAction(Resources.MergingBranches);

            await Task.WhenAll(targetBranches.Select((b) => _mergeService.MergeBranchesAsync(
                mergeModel.WorkspaceModel, sourceBranch, b, mergeModel.OrderedChangesets.First().ChangesetId, mergeModel.OrderedChangesets.Last().ChangesetId)));

            var workItemIds = await GetWorkItemIdsAsync(mergeModel.OrderedChangesets.Select(x => x.ChangesetId));
            var comment = GetCommentForMerge(mergeModel, workItemIds);

            _teamExplorerService.AddWorkItemsAndCommentThenNavigate(mergeModel.WorkspaceModel, comment, workItemIds);
        }

        private async Task CheckIfWorkspaceHasIncludedPendingChangesAsync(Workspace workspaceModel)
        {
            var shouldCheckForPendingChanges = _configManager.GetValue<bool>(ConfigKeys.ENABLE_WARNING_WHEN_PENDING_CHANGES);

            if (shouldCheckForPendingChanges)
            {
                SetCurrentAction(Resources.CheckingPendingChanges);
                var hasPendingChanges = await Task.Run(() => _mergeService.HasIncludedPendingChanges(workspaceModel));

                if (hasPendingChanges)
                {
                    throw new MergeActionException(Resources.WorkspaceContainsPendingChanges);
                }
            }
        }

        private async Task DoGetLatestOnBranchAsync(Workspace workspaceModel, string sourceBranch, IEnumerable<string> targetBranches)
        {
            var latestVersionForBranches = _configManager.GetValue<Branch>(ConfigKeys.LATEST_VERSION_FOR_BRANCH);

            if (latestVersionForBranches != Branch.None)
            {
                SetCurrentAction(string.Format(CultureInfo.CurrentUICulture, Resources.GettingLatestVersionForBranch, latestVersionForBranches.GetDescription().ToLower()));
                var branchNamesForLatestVersion = GetBranchesForExecutingGetLatest(latestVersionForBranches, sourceBranch, targetBranches);

                var hasConflicts = await _mergeService.GetLatestVersionAsync(workspaceModel, branchNamesForLatestVersion.ToArray());

                if (hasConflicts)
                {
                    var shouldResolveConflicts = _configManager.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS);

                    if (shouldResolveConflicts)
                    {
                        await _mergeService.ResolveConflictsAsync(workspaceModel);

                        if (_mergeService.HasConflicts(workspaceModel))
                        {
                            throw new MergeActionException(Resources.GetLatestConflicts);
                        }
                    }
                    else
                    {
                        throw new MergeActionException(Resources.GetLatestConflicts);
                    }
                }
            }
        }

        private IEnumerable<string> GetBranchesForExecutingGetLatest(Branch latestVersionForBranches, string sourceBranch, IEnumerable<string> targetBranches)
        {
            var branches = new List<string>();

            switch (latestVersionForBranches)
            {
                case Branch.Target:
                    branches.AddRange(targetBranches);
                    break;
                case Branch.Source:
                    branches.Add(sourceBranch);
                    break;
                case Branch.SourceAndTarget:
                    branches.AddRange(targetBranches);
                    branches.Add(sourceBranch);
                    break;
            }

            return branches;
        }

        private async Task<IEnumerable<int>> GetWorkItemIdsAsync(IEnumerable<int> changesetIds)
        {
            if (!_configManager.GetValue<bool>(ConfigKeys.EXCLUDE_WORK_ITEMS_FOR_MERGE))
            {
                return await _mergeService.GetWorkItemIdsAsync(changesetIds, _configManager.GetValue<IEnumerable<string>>(ConfigKeys.WORK_ITEM_TYPES_TO_EXCLUDE));
            }

            return Enumerable.Empty<int>();
        }

        private bool ShouldShowLatestVersionComment(bool isLatestVersion)
        {
            return _configManager.GetValue<bool>(ConfigKeys.SHOULD_SHOW_LATEST_VERSION_IN_COMMENT) && isLatestVersion;
        }

        private string GetCommentForMerge(MergeModel mergeModel, IEnumerable<int> workItemIds)
        {
            var checkInCommentChoice = _configManager.GetValue<CheckInComment>(ConfigKeys.CHECK_IN_COMMENT_OPTION);
            var commentFormat = _configManager.GetValue<string>(ConfigKeys.COMMENT_FORMAT);
            var commentLineFormat = _configManager.GetValue<string>(ConfigKeys.COMMENT_LINE_FORMAT);

            if (checkInCommentChoice == CheckInComment.Standard3SYS && commentFormat.IsEmpty())
            {
                commentFormat = Resources.Changeset3SYSFormat;
                commentLineFormat = Resources.Changeset3SYSLineFormat;
            }

            return CommentOutputHelper.GetCheckInComment(checkInCommentChoice, commentFormat, commentLineFormat, mergeModel.SourceBranch, mergeModel.TargetBranches.First(), workItemIds, mergeModel.OrderedChangesets, ShouldShowLatestVersionComment(mergeModel.IsLatestVersion));
        }

        private void SetCurrentAction(string currentAction)
        {
            MyCurrentAction?.Invoke(this, currentAction);
        }
    }

    public class MergeModel
    {
        public Workspace WorkspaceModel { get; set; }
        public IEnumerable<Changeset> OrderedChangesets { get; set; }
        public string SourceBranch { get; set; }
        public IEnumerable<string> TargetBranches { get; set; }
        public string SubPath { get; set; }
        public bool IsLatestVersion { get; set; }
    }
}