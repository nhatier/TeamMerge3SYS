using Domain.Entities;
using Logic.Helpers;
using Logic.Services;
using Shared.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamMergeBase.Base;
using TeamMergeBase.Commands;
using TeamMergeBase.Helpers;
using TeamMergeBase.Merge.Context;
using TeamMergeBase.Operations;
using TeamMergeBase.Settings.Dialogs;

namespace TeamMergeBase.Merge
{
    public class TeamMergeCommonCommandsViewModel
        : ViewModelBase
    {
        private readonly ITeamService _teamService;
        private readonly IMergeOperation _mergeOperation;
        private readonly IConfigManager _configManager;
        private readonly ILogger _logger;
        private readonly ISolutionService _solutionService;
        private readonly Func<Func<Task>, Task> _setBusyWhileExecutingAsync;
        private List<Branch> _currentBranches;

        public TeamMergeCommonCommandsViewModel(ITeamService teamService, IMergeOperation mergeOperation, IConfigManager configManager, ILogger logger, ISolutionService solutionService, Func<Func<Task>, Task> setBusyWhileExecutingAsync)
        {
            _teamService = teamService;
            _mergeOperation = mergeOperation;
            _configManager = configManager;
            _logger = logger;
            _solutionService = solutionService;
            _setBusyWhileExecutingAsync = setBusyWhileExecutingAsync;

            MergeCommand = new AsyncRelayCommand(MergeAsync, CanMerge);
            FetchChangesetsCommand = new AsyncRelayCommand(FetchChangesetsAsync, CanFetchChangesets);
            SelectWorkspaceCommand = new RelayCommand<Workspace>(SelectWorkspace);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            SwitchTargetAndSourceBranchesCommand = new RelayCommand(SwitchTargetAndSourceBranches, CanSwitchTargetAndSourceBranches);

            DestinationBranchesChangeCommand = new RelayCommand<System.Windows.Controls.ListBox>(DestinationBranchesChangeEvent);

            SourcesBranches = new ObservableCollection<string>();
            TargetBranches = new ObservableCollection<string>();
            ProjectNames = new ObservableCollection<string>();
            SelectedTargetBranches = new ObservableCollection<string>();

            Changesets = new ObservableCollection<Changeset>();
            SelectedChangesets = new ObservableCollection<Changeset>();
        }

        public IRelayCommand ViewChangesetDetailsCommand { get; }
        public IRelayCommand DestinationBranchesChangeCommand { get; }        
        public IRelayCommand MergeCommand { get; }
        public IRelayCommand FetchChangesetsCommand { get; }
        public IRelayCommand SelectWorkspaceCommand { get; }
        public IRelayCommand OpenSettingsCommand { get; }
        public IRelayCommand SwitchTargetAndSourceBranchesCommand { get; }

        public ObservableCollection<string> ProjectNames { get; set; }
        public ObservableCollection<string> SourcesBranches { get; set; }
        public ObservableCollection<string> TargetBranches { get; set; }

        private string _selectedProjectName;

        public string SelectedProjectName
        {
            get { return _selectedProjectName; }
            set
            {
                _selectedProjectName = value;
                RaisePropertyChanged(nameof(SelectedProjectName));

                _currentBranches = _teamService.GetBranches(SelectedProjectName).ToList();

                Changesets.Clear();
                SourcesBranches.Clear();
                TargetBranches.Clear();
                SourcesBranches.AddRange(_currentBranches.Select(x => x.Name));
                SingleChangeset = null;                

                SelectedSourceBranch = $"$/{_selectedProjectName}/Principale";
            }
        }


        private void DestinationBranchesChangeEvent(System.Windows.Controls.ListBox modelListBox)
        {
            var tempModelInfo = new ObservableCollection<string>();
            foreach (string a in modelListBox.SelectedItems)
                tempModelInfo.Add(a);

            SelectedTargetBranches = tempModelInfo;
        }

        private string _selectedSourceBranch;

        public string SelectedSourceBranch
        {
            get { return _selectedSourceBranch; }
            set
            {
                _selectedSourceBranch = value;
                Changesets.Clear();
                SelectedChangeset = null;
                SingleChangeset = null;
                
                RaisePropertyChanged(nameof(SelectedChangeset));
                RaisePropertyChanged(nameof(SelectedChangesets));
                RaisePropertyChanged(nameof(SelectedSourceBranch));
                RaisePropertyChanged(nameof(SingleChangesetEnabled));

                InitializeTargetBranches();

                FetchChangesetsCommand.RaiseCanExecuteChanged();
            }
        }

        private string _sourcePath;

        public string SubPath
        {
            get { return _sourcePath; }
            set {                 

                var subPath = value.Replace("\\", "/");
                if (!subPath.IsEmpty() && !subPath.StartsWith("/"))
                    subPath = "/" + subPath;
                _sourcePath = subPath;

                RaisePropertyChanged(nameof(SubPath));
            }
        }

        public void InitializeTargetBranches()
        {
            TargetBranches.Clear();

            if (SelectedSourceBranch != null)
            {
                var selectedSource = SelectedSourceBranch.Replace("\\", "/");
                var br = _currentBranches.Select(b =>
                {
                    int len = Math.Min(selectedSource.Length, b.Name.Length);
                    int maxlen = len;
                    for (int i = 0; i < len; i++)
                    {
                        if (b.Name[i] != selectedSource[i])
                        {
                            maxlen = i;
                            break;
                        }
                    }
                    return new { Branch = b, Length = maxlen };
                }).OrderByDescending(x => x.Length).FirstOrDefault().Branch;

                var lBranchesValides = br.Branches.Where(c => !c.IsEmpty())
                    .Where(c => !c.Contains("/Principale-OLTP"))
                    .Where(c => selectedSource.IndexOf("/Principale", StringComparison.InvariantCultureIgnoreCase) > 0 ^ c.Contains("/Principale"))
                    .OrderByDescending(b => b, new Logic.Helpers.PathWithVersionComparer())
                    .Select(b => b + selectedSource.Substring(Math.Min(selectedSource.Length, br.Name.Length)))
                    .Distinct().ToList();
                ;

                if (br != null)
                    TargetBranches.AddRange(lBranchesValides);
            }
        }

        private string _selectedTargetBranch;

        public string SelectedTargetBranch
        {
            get { return _selectedTargetBranch; }
            set
            {
                _selectedTargetBranch = value;
                RaisePropertyChanged(nameof(SelectedTargetBranch));
                RaisePropertyChanged(nameof(SingleChangesetEnabled));
                FetchChangesetsCommand.RaiseCanExecuteChanged();
                SwitchTargetAndSourceBranchesCommand.RaiseCanExecuteChanged();
            }
        }

        
        private ObservableCollection<string> _selectedTargetBranches;

        public ObservableCollection<string> SelectedTargetBranches
        {
            get { return _selectedTargetBranches; }         
            set
            {
                _selectedTargetBranches = value;
                RaisePropertyChanged(nameof(SelectedTargetBranches));
                if (_selectedTargetBranches != null)
                {
                    _selectedTargetBranches.CollectionChanged += SelectedTargetBranches_CollectionChanged;
                }
            }
        }

        private void SelectedTargetBranches_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MergeCommand.RaiseCanExecuteChanged();
        }

        private Changeset _selectedChangeset;

        public Changeset SelectedChangeset
        {
            get { return _selectedChangeset; }
            set
            {
                _selectedChangeset = value;
                RaisePropertyChanged(nameof(SelectedChangeset));
            }
        }

        CancellationTokenSource c = new CancellationTokenSource();

        private bool _reentrant = false;
        private int? _singleChangesetId;
        public int? SingleChangesetId
        {
            get { return _singleChangesetId; }
            set
            {
                if (_reentrant) return;
                _reentrant = true;
                _singleChangesetId = value;
                RaisePropertyChanged(nameof(SingleChangesetId));

                if (_singleChangesetId == null ^ SingleChangeset == null ||
                    _singleChangesetId != null && _singleChangesetId.Value != SingleChangeset.ChangesetId)
                {
                   
                        SingleChangeset = null;
                        c.Cancel();
                        SelectedChangesets.Clear();
                        if (value.HasValue)
                        {
                            c = new CancellationTokenSource();
#pragma warning disable CS4014
                            FetchSingleChangeset(value.Value, c);
#pragma warning restore CS4014
                        }
                    
                }
                _reentrant = false;
            }
        }

        async Task FetchSingleChangeset(int changesetId, CancellationTokenSource c)
        {
            await Task.Delay(250, c.Token);
            if (!c.IsCancellationRequested)
            {
                var lChangeSet = Changesets.Where((ch) => ch.ChangesetId == _singleChangesetId).FirstOrDefault();
                if (lChangeSet != null)
                {
                    SingleChangeset = lChangeSet;
                    SelectedChangeset = lChangeSet;
                    SelectedChangesets.Clear();
                    SelectedChangesets.Add(lChangeSet);
                    RaisePropertyChanged(nameof(SelectedChangeset));
                    RaisePropertyChanged(nameof(SelectedChangesets));
                    RaisePropertyChanged(nameof(SingleChangesetEnabled));
                }
                else
                {
                    var changeset = await _teamService.GetChangesetAsync(changesetId);
                    SingleChangeset = changeset;
                }
            }
        }

        private Changeset _singleChangeset;
        public Changeset SingleChangeset
        {
            get { return _singleChangeset; }
            set
            {
                _singleChangeset = value;                                     
                RaisePropertyChanged(nameof(SingleChangeset));
                RaisePropertyChanged(nameof(SingleChangesetText));
                RaisePropertyChanged(nameof(SingleChangesetEnabled));
                MergeCommand.RaiseCanExecuteChanged();

                if (value == null && _singleChangesetId.HasValue)
                {
                    SingleChangesetId = null;
                }
                else if (value != null && (_singleChangesetId == null || _singleChangesetId != value.ChangesetId))
                {
                    SingleChangesetId = value.ChangesetId;
                }
            }
        }

        public string SingleChangesetText
        {
            get { return _singleChangeset?.Comment; }
/*            set
            {
                RaisePropertyChanged(nameof(SingleChangesetText));
            }*/
        }

        public bool SingleChangesetEnabled
        {
            get
            {
                return CanFetchChangesets() && SelectedChangesets.Distinct().Count() <= 1;
            }
            set
            {
                RaisePropertyChanged(nameof(SingleChangesetEnabled));
            }
        }

        private ObservableCollection<Changeset> _selectedChangesets;

        public ObservableCollection<Changeset> SelectedChangesets
        {
            get { return _selectedChangesets; }
            set
            {
                _selectedChangesets = value;
                RaisePropertyChanged(nameof(SelectedChangesets));

                if (_selectedChangesets != null)
                {
                    _selectedChangesets.CollectionChanged += SelectedChangesets_CollectionChanged;
                }
            }
        }

        private void SelectedChangesets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var first = SelectedChangesets.FirstOrDefault();

            if (first != null)
            {
                Changeset _single = null;
                if (!SelectedChangesets.Skip(1).Any())
                    _single = first;

                if (_single != SingleChangeset)
                    SingleChangeset = _single;
            }

            MergeCommand.RaiseCanExecuteChanged();
        }

        private ObservableCollection<Changeset> _changesets;

        public ObservableCollection<Changeset> Changesets
        {
            get { return _changesets; }
            protected set
            {
                _changesets = value;
                RaisePropertyChanged(nameof(Changesets));
            }
        }

        private ObservableCollection<Workspace> _workspaces;

        public ObservableCollection<Workspace> Workspaces
        {
            get { return _workspaces; }
            set { _workspaces = value; RaisePropertyChanged(nameof(Workspaces)); }
        }

        private Workspace _selectedWorkspace;

        public Workspace SelectedWorkspace
        {
            get { return _selectedWorkspace; }
            set { _selectedWorkspace = value; RaisePropertyChanged(nameof(SelectedWorkspace)); }
        }

        private string _myCurrentAction;

        public string MyCurrentAction
        {
            get { return _myCurrentAction; }
            set
            {
                _myCurrentAction = value;
                RaisePropertyChanged(nameof(MyCurrentAction));
            }
        }

        private bool _shouldShowButtonSwitchingSourceTargetBranch;

        public bool ShouldShowButtonSwitchingSourceTargetBranch
        {
            get { return _shouldShowButtonSwitchingSourceTargetBranch; }
            set { _shouldShowButtonSwitchingSourceTargetBranch = value; RaisePropertyChanged(nameof(ShouldShowButtonSwitchingSourceTargetBranch)); }
        }

        private async Task MergeAsync()
        {
            await _setBusyWhileExecutingAsync(async () =>
            {

                List<Changeset> orderedSelectedChangesets;
                if (SingleChangeset != null && !SingleChangesetText.IsEmpty())
                    orderedSelectedChangesets = new[] { SingleChangeset }.ToList();
                else
                    orderedSelectedChangesets = SelectedChangesets.OrderBy(x => x.ChangesetId).ToList();

                _mergeOperation.MyCurrentAction += MergeOperation_MyCurrentAction;

                await _mergeOperation.ExecuteAsync(new MergeModel
                {
                    WorkspaceModel = SelectedWorkspace,
                    OrderedChangesets = orderedSelectedChangesets,
                    SourceBranch = SelectedSourceBranch,
                    TargetBranches = SelectedTargetBranches,
                    SubPath = SubPath,
                    IsLatestVersion = SelectedChangesets.Count == Changesets.Count
                });

                SaveDefaultSettings();
                SaveDefaultSettingsSolutionWide();
            });

            MyCurrentAction = null;
            _mergeOperation.MyCurrentAction -= MergeOperation_MyCurrentAction;
        }

        private void SaveDefaultSettingsSolutionWide()
        {
            _solutionService.SaveDefaultMergeSettingsForCurrentSolution(new DefaultMergeSettings
            {
                ProjectName = _selectedProjectName,
                SourceBranch = _selectedSourceBranch,
                TargetBranches = _selectedTargetBranches
            });
        }

        private void SaveDefaultSettings()
        {
            _configManager.AddValue(ConfigKeys.SELECTED_PROJECT_NAME, SelectedProjectName);
            _configManager.AddValue(ConfigKeys.SOURCE_BRANCH, SelectedSourceBranch);
            _configManager.AddValue(ConfigKeys.TARGET_BRANCH, SelectedTargetBranches);

            _configManager.SaveDictionary();
        }

        private void MergeOperation_MyCurrentAction(object sender, string e)
        {
            MyCurrentAction = e;
        }

        private bool CanMerge()
        {
            return SelectedChangesets != null
                && SelectedChangesets.Any()
                && Changesets.Count(x => x.ChangesetId >= SelectedChangesets.Min(y => y.ChangesetId) &&
                                         x.ChangesetId <= SelectedChangesets.Max(y => y.ChangesetId)) == SelectedChangesets.Count
                || SingleChangeset != null && !SingleChangesetText.IsEmpty();
        }

        private async Task FetchChangesetsAsync()
        {
            await _setBusyWhileExecutingAsync(async () =>
            {
                Changesets.Clear();

                var results = await Task.WhenAll(SelectedTargetBranches.Select((b) => _teamService.GetChangesetsAsync(SelectedSourceBranch + SubPath, b + SubPath)));
                
                var changesets = results.SelectMany((c) => c).Distinct().OrderByDescending((c) => c.ChangesetId);

                Changesets = new ObservableCollection<Changeset>(changesets);

                if (_configManager.GetValue<bool>(ConfigKeys.ENABLE_AUTO_SELECT_ALL_CHANGESETS))
                {
                    SelectedChangesets.AddRange(Changesets.Except(SelectedChangesets));
                    RaisePropertyChanged(nameof(SelectedChangesets));
                }
            });

            MergeCommand.RaiseCanExecuteChanged();
        }

        private bool CanFetchChangesets()
        {
            return SelectedSourceBranch != null && SelectedTargetBranch != null;
        }

        private void SelectWorkspace(Workspace workspace)
        {
            SelectedWorkspace = workspace;
        }

        public async Task InitializeAsync(TeamMergeContext teamMergeContext)
        {
            await _setBusyWhileExecutingAsync(async () =>
            {
                var projectNames = await _teamService.GetProjectNamesAsync();

                Workspaces = new ObservableCollection<Workspace>(await _teamService.AllWorkspacesAsync());
                SelectedWorkspace = _teamService.CurrentWorkspace() ?? Workspaces.First();

                projectNames.ToList().ForEach(x => ProjectNames.Add(x));

                if (teamMergeContext != null)
                {
                    RestoreContext(teamMergeContext);
                }
                else
                {
                    SetSavedSelectedBranches();
                }

                ReadSettingsFromConfigManager();
            });
        }

        private void SetSavedSelectedBranches()
        {
            var saveSelectedBranchSettingsBySolution = _configManager.GetValue<bool>(ConfigKeys.SAVE_BRANCH_PERSOLUTION);
            if (saveSelectedBranchSettingsBySolution)
            {
                SetDefaultSelectedSettingsPerSolution();
            }
            else
            {
                SetDefaultSelectedSettings();
            }
        }

        private void SetDefaultSelectedSettingsPerSolution()
        {
            var defaultMergeSettings = _solutionService.GetDefaultMergeSettingsForCurrentSolution();

            if (defaultMergeSettings != null && defaultMergeSettings.IsValidSettings())
            {
                SelectedProjectName = defaultMergeSettings.ProjectName;

                if (_currentBranches.Any(x => x.Name == defaultMergeSettings.SourceBranch))
                {
                    SelectedSourceBranch = defaultMergeSettings.SourceBranch;
                    SelectedTargetBranches = new ObservableCollection<string>(defaultMergeSettings.TargetBranches);
                }
            }
            else
            {
                SetDefaultSelectedSettings();
            }
        }

        private void SetDefaultSelectedSettings()
        {
            var projectName = _configManager.GetValue<string>(ConfigKeys.SELECTED_PROJECT_NAME);

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                SelectedProjectName = projectName;

                var savedSourceBranch = _configManager.GetValue<string>(ConfigKeys.SOURCE_BRANCH);

                if (_currentBranches.Any(x => x.Name == savedSourceBranch))
                {
                    SelectedSourceBranch = savedSourceBranch;
                    SelectedTargetBranches = _configManager.GetValue<ObservableCollection<string>>(ConfigKeys.TARGET_BRANCH);                    
                }
            }
        }

        private void ReadSettingsFromConfigManager()
        {
            ShouldShowButtonSwitchingSourceTargetBranch = _configManager.GetValue<bool>(ConfigKeys.SHOULD_SHOW_BUTTON_SWITCHING_SOURCE_TARGET_BRANCH);
        }

        public void OpenSettings()
        {
            var viewModel = new SettingsDialogViewModel(_configManager, _teamService);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _ = viewModel.InitializeAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            var window = new SettingsDialog
            {
                DataContext = viewModel
            };

            window.Closing += (e, cancelEventArgs) => viewModel.OnCloseWindowRequest(cancelEventArgs);
            viewModel.RequestClose += () => window.Close();

            window.ShowDialog();

            ReadSettingsFromConfigManager();
        }

        private void RestoreContext(TeamMergeContext context)
        {
            SelectedProjectName = context.SelectedProjectName;
            Changesets = context.Changesets;

            if (_currentBranches.Any(x => x.Name == context.SourceBranch))
            {
                SelectedSourceBranch = context.SourceBranch;
                SelectedTargetBranches = new ObservableCollection<string>(context.TargetBranches);
            }
        }

        public TeamMergeContext CreateContext()
        {
            return new TeamMergeContext
            {
                SelectedProjectName = SelectedProjectName,
                Changesets = Changesets,
                SourceBranch = SelectedSourceBranch,
                TargetBranches = SelectedTargetBranches
            };
        }

        public void Cleanup()
        {
            if (_selectedChangesets != null)
            {
                _selectedChangesets.CollectionChanged -= SelectedChangesets_CollectionChanged;
            }
        }

        private void SwitchTargetAndSourceBranches()
        {
            (SelectedTargetBranch, SelectedSourceBranch) = (SelectedSourceBranch, SelectedTargetBranch);
        }

        private bool CanSwitchTargetAndSourceBranches() =>
            // If selected source branch is null, this command will be effectively "use target branch as source".
            // Other way round - having target branch not selected while having source branch selected is useless,
            // as after switching source will be empty and changing it to anything will clear target branch combo anyway.
            SelectedTargetBranch != null;
    }
}
