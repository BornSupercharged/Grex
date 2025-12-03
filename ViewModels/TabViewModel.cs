using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grex.Models;
using Grex.Services;

namespace Grex.ViewModels
{
    public class TabViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISearchService _searchService;
        private readonly ILocalizationService _localizationService = LocalizationService.Instance;
        private readonly NotificationService _notificationService = NotificationService.Instance;
        private readonly DockerSearchService _dockerSearchService;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private readonly Stopwatch _searchStopwatch = new Stopwatch();
        private bool _isReplaceOperation = false; // Tracks if current operation is a Replace (vs Search)
        private bool _isDockerSearchEnabled;
        private bool _isDockerCliAvailable;
        private IReadOnlyList<DockerContainerInfo> _dockerContainers = Array.Empty<DockerContainerInfo>();
        private DockerContainerInfo? _selectedDockerContainer;
        private DockerMirrorInfo? _activeDockerMirror;
        private IReadOnlyList<DockerContainerOption> _dockerContainerOptions = Array.Empty<DockerContainerOption>();
        private DockerContainerOption? _selectedDockerOption;
        private bool _isUpdatingDockerSelection;
        private string _searchPath = string.Empty;
        private string _searchTerm = string.Empty;
        private string _replaceWith = string.Empty;
        private bool _isRegexSearch = false;
        private bool _respectGitignore = false;
        private bool _searchCaseSensitive = false;
        private bool _includeSystemFiles = false;
        private bool _includeSubfolders = true;
        private bool _includeHiddenItems = false;
        private bool _includeBinaryFiles = false;
        private bool _includeSymbolicLinks = false;
        private bool _useWindowsSearchIndex = false;
        private bool _isWindowsSearchOptionEnabled = false;
        private string _matchFileNames = string.Empty;
        private string _excludeDirs = string.Empty;
        private bool _isFilesSearch = false;
        private string _statusText = string.Empty;
        private string _statusResourceKey = "ReadyStatus";
        private object[] _statusResourceArgs = Array.Empty<object>();
        private bool _isSearching = false;
        private string _tabTitle;
        private readonly string _originalTabTitle;
        private SearchResultSortField _currentSortField = SearchResultSortField.None;
        private bool _isSortDescending = false;
        private Models.SizeLimitType _sizeLimitType = Models.SizeLimitType.NoLimit;
        private long? _sizeLimitKB = null;
        private Models.SizeUnit _sizeUnit = Models.SizeUnit.KB;
        
        // Culture-aware string comparison settings
        private Models.StringComparisonMode _stringComparisonMode = Models.StringComparisonMode.Ordinal;
        private Models.UnicodeNormalizationMode _unicodeNormalizationMode = Models.UnicodeNormalizationMode.None;
        private bool _diacriticSensitive = true;
        private string _culture = CultureInfo.CurrentCulture.Name;
        
        // Column visibility for Content table
        private bool _contentLineColumnVisible = true;
        private bool _contentColumnColumnVisible = true;
        private bool _contentPathColumnVisible = true;
        
        // Column visibility for Files table
        private bool _filesSizeColumnVisible = true;
        private bool _filesMatchesColumnVisible = true;
        private bool _filesPathColumnVisible = true;
        private bool _filesExtColumnVisible = true;
        private bool _filesEncodingColumnVisible = true;
        private bool _filesDateModifiedColumnVisible = true;

        public TabViewModel(ISearchService searchService, string? title = null, DockerSearchService? dockerSearchService = null)
        {
            try
            {
                Log($"TabViewModel constructor: Starting, title: {title}");
                _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
                _dockerSearchService = dockerSearchService ?? DockerSearchService.Instance;
                SearchResults = new ObservableCollection<SearchResult>();
                FileSearchResults = new ObservableCollection<FileSearchResult>();
                SetStatus("ReadyStatus");
                _tabTitle = GetString("TabNewTitle");
                _originalTabTitle = title ?? GetString("TabTimestampTitleFormat", DateTime.Now.ToString("HH:mm:ss"));
                TabTitle = _originalTabTitle;

                _isDockerSearchEnabled = SettingsService.GetEnableDockerSearch();
                SettingsService.DockerSearchEnabledChanged += OnDockerSearchEnabledChanged;
                if (_isDockerSearchEnabled)
                {
                    _ = InitializeDockerSupportAsync();
                }
                RebuildDockerOptions();
                
                // Load default settings
                var defaultSettings = Services.SettingsService.GetDefaultSettings();
                IsRegexSearch = defaultSettings.IsRegexSearch;
                IsFilesSearch = defaultSettings.IsFilesSearch;
                MatchFileNames = defaultSettings.DefaultMatchFiles ?? string.Empty;
                ExcludeDirs = defaultSettings.DefaultExcludeDirs ?? string.Empty;
                RespectGitignore = defaultSettings.RespectGitignore;
                SearchCaseSensitive = defaultSettings.SearchCaseSensitive;
                IncludeSystemFiles = defaultSettings.IncludeSystemFiles;
                IncludeSubfolders = defaultSettings.IncludeSubfolders;
                IncludeHiddenItems = defaultSettings.IncludeHiddenItems;
                IncludeBinaryFiles = defaultSettings.IncludeBinaryFiles;
                IncludeSymbolicLinks = defaultSettings.IncludeSymbolicLinks;
                UseWindowsSearchIndex = defaultSettings.UseWindowsSearchIndex;
                SizeUnit = defaultSettings.SizeUnit;
                UpdateWindowsSearchOptionAvailability();
                
                // Load culture-aware string comparison settings
                StringComparisonMode = defaultSettings.StringComparisonMode;
                UnicodeNormalizationMode = defaultSettings.UnicodeNormalizationMode;
                DiacriticSensitive = defaultSettings.DiacriticSensitive;
                Culture = defaultSettings.Culture;
                ContentLineColumnVisible = defaultSettings.ContentLineColumnVisible;
                ContentColumnColumnVisible = defaultSettings.ContentColumnColumnVisible;
                ContentPathColumnVisible = defaultSettings.ContentPathColumnVisible;
                FilesSizeColumnVisible = defaultSettings.FilesSizeColumnVisible;
                FilesMatchesColumnVisible = defaultSettings.FilesMatchesColumnVisible;
                FilesPathColumnVisible = defaultSettings.FilesPathColumnVisible;
                FilesExtColumnVisible = defaultSettings.FilesExtColumnVisible;
                FilesEncodingColumnVisible = defaultSettings.FilesEncodingColumnVisible;
                FilesDateModifiedColumnVisible = defaultSettings.FilesDateModifiedColumnVisible;
                
                Log($"TabViewModel constructor: Completed, TabTitle: {TabTitle}");
            }
            catch (Exception ex)
            {
                Log($"TabViewModel constructor ERROR: {ex}");
                throw;
            }
        }

        private static void Log(string message)
        {
            try
            {
                var logFile = Path.Combine(Path.GetTempPath(), "Grex.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(logFile, $"[{timestamp}] {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public string TabTitle
        {
            get => _tabTitle;
            set
            {
                if (_tabTitle != value)
                {
                    _tabTitle = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SearchPath
        {
            get => _searchPath;
            set
            {
                if (_searchPath != value)
                {
                    _searchPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSearch));
                    OnPropertyChanged(nameof(CanSearchOrStop));
                    OnPropertyChanged(nameof(CanReplace));
                    OnPropertyChanged(nameof(CanReplaceOrStop));
                    UpdateTabTitle();
                    UpdateWindowsSearchOptionAvailability();
                }
            }
        }

        public bool IsDockerSearchGloballyEnabled
        {
            get => _isDockerSearchEnabled;
            private set
            {
                if (_isDockerSearchEnabled != value)
                {
                    _isDockerSearchEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSelectDockerContainer));
                    OnPropertyChanged(nameof(IsDockerModeActive));
                    OnPropertyChanged(nameof(IsFileBrowserEnabled));
                    UpdateWindowsSearchOptionAvailability();

                    if (!value)
                    {
                        SelectedDockerContainer = null;
                        RebuildDockerOptions();
                    }
                }
            }
        }

        public bool CanSelectDockerContainer => _isDockerSearchEnabled && _isDockerCliAvailable;

        public bool IsDockerModeActive => CanSelectDockerContainer && _selectedDockerContainer != null;

        public bool IsFileBrowserEnabled => !IsDockerModeActive;

        public IReadOnlyList<DockerContainerInfo> DockerContainers
        {
            get => _dockerContainers;
            private set
            {
                if (!ReferenceEquals(_dockerContainers, value))
                {
                    _dockerContainers = value;
                    OnPropertyChanged();
                }
            }
        }

        public IReadOnlyList<DockerContainerOption> DockerContainerOptions
        {
            get => _dockerContainerOptions;
            private set
            {
                if (!ReferenceEquals(_dockerContainerOptions, value))
                {
                    _dockerContainerOptions = value;
                    OnPropertyChanged();
                }
            }
        }

        public DockerContainerOption? SelectedDockerOption
        {
            get => _selectedDockerOption;
            set
            {
                if (_selectedDockerOption != value)
                {
                    _selectedDockerOption = value;
                    OnPropertyChanged();
                    _isUpdatingDockerSelection = true;
                    SelectedDockerContainer = value?.Container;
                    _isUpdatingDockerSelection = false;
                }
            }
        }

        public DockerContainerInfo? SelectedDockerContainer
        {
            get => _selectedDockerContainer;
            set
            {
                if (_selectedDockerContainer != value)
                {
                    _selectedDockerContainer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDockerModeActive));
                    OnPropertyChanged(nameof(IsFileBrowserEnabled));
                    OnPropertyChanged(nameof(CanSearch));
                    OnPropertyChanged(nameof(CanSearchOrStop));
                    OnPropertyChanged(nameof(CanReplace));
                    OnPropertyChanged(nameof(CanReplaceOrStop));
                    UpdateWindowsSearchOptionAvailability();
                    ScheduleMirrorCleanup();

                    if (!_isUpdatingDockerSelection)
                    {
                        var option = DockerContainerOptions.FirstOrDefault(o =>
                            o.Container != null &&
                            _selectedDockerContainer != null &&
                            string.Equals(o.Container.Id, _selectedDockerContainer.Id, StringComparison.OrdinalIgnoreCase));

                        if (option == null && DockerContainerOptions.Count > 0)
                        {
                            option = DockerContainerOptions.First();
                        }

                        _selectedDockerOption = option;
                        OnPropertyChanged(nameof(SelectedDockerOption));
                    }
                }
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSearch));
                    OnPropertyChanged(nameof(CanSearchOrStop));
                    OnPropertyChanged(nameof(CanReplace));
                    OnPropertyChanged(nameof(CanReplaceOrStop));
                }
            }
        }

        public string ReplaceWith
        {
            get => _replaceWith;
            set
            {
                if (_replaceWith != value)
                {
                    _replaceWith = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanReplace));
                    OnPropertyChanged(nameof(CanReplaceOrStop));
                }
            }
        }

        public bool IsRegexSearch
        {
            get => _isRegexSearch;
            set
            {
                if (_isRegexSearch != value)
                {
                    _isRegexSearch = value;
                    OnPropertyChanged();
                    UpdateWindowsSearchOptionAvailability();
                }
            }
        }

        public bool RespectGitignore
        {
            get => _respectGitignore;
            set
            {
                if (_respectGitignore != value)
                {
                    _respectGitignore = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SearchCaseSensitive
        {
            get => _searchCaseSensitive;
            set
            {
                if (_searchCaseSensitive != value)
                {
                    _searchCaseSensitive = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeSystemFiles
        {
            get => _includeSystemFiles;
            set
            {
                if (_includeSystemFiles != value)
                {
                    _includeSystemFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (_includeSubfolders != value)
                {
                    _includeSubfolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeHiddenItems
        {
            get => _includeHiddenItems;
            set
            {
                if (_includeHiddenItems != value)
                {
                    _includeHiddenItems = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeBinaryFiles
        {
            get => _includeBinaryFiles;
            set
            {
                if (_includeBinaryFiles != value)
                {
                    _includeBinaryFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeSymbolicLinks
        {
            get => _includeSymbolicLinks;
            set
            {
                if (_includeSymbolicLinks != value)
                {
                    _includeSymbolicLinks = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseWindowsSearchIndex
        {
            get => _useWindowsSearchIndex;
            set
            {
                var newValue = value && IsWindowsSearchOptionEnabled;
                if (_useWindowsSearchIndex != newValue)
                {
                    _useWindowsSearchIndex = newValue;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsWindowsSearchOptionEnabled
        {
            get => _isWindowsSearchOptionEnabled;
            private set
            {
                if (_isWindowsSearchOptionEnabled != value)
                {
                    _isWindowsSearchOptionEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MatchFileNames
        {
            get => _matchFileNames;
            set
            {
                if (_matchFileNames != value)
                {
                    _matchFileNames = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExcludeDirs
        {
            get => _excludeDirs;
            set
            {
                if (_excludeDirs != value)
                {
                    _excludeDirs = value;
                    OnPropertyChanged();
                }
            }
        }

        public Models.SizeLimitType SizeLimitType
        {
            get => _sizeLimitType;
            set
            {
                if (_sizeLimitType != value)
                {
                    _sizeLimitType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSizeLimitEnabled));
                }
            }
        }

        public long? SizeLimitKB
        {
            get => _sizeLimitKB;
            set
            {
                if (_sizeLimitKB != value)
                {
                    _sizeLimitKB = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSizeLimitEnabled => _sizeLimitType != Models.SizeLimitType.NoLimit;

        // Content table column visibility
        public bool ContentLineColumnVisible
        {
            get => _contentLineColumnVisible;
            set
            {
                if (_contentLineColumnVisible != value)
                {
                    _contentLineColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultContentLineColumnVisible(value);
                }
            }
        }

        public bool ContentColumnColumnVisible
        {
            get => _contentColumnColumnVisible;
            set
            {
                if (_contentColumnColumnVisible != value)
                {
                    _contentColumnColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultContentColumnColumnVisible(value);
                }
            }
        }

        public bool ContentPathColumnVisible
        {
            get => _contentPathColumnVisible;
            set
            {
                if (_contentPathColumnVisible != value)
                {
                    _contentPathColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultContentPathColumnVisible(value);
                }
            }
        }

        // Files table column visibility
        public bool FilesSizeColumnVisible
        {
            get => _filesSizeColumnVisible;
            set
            {
                if (_filesSizeColumnVisible != value)
                {
                    _filesSizeColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesSizeColumnVisible(value);
                }
            }
        }

        public bool FilesMatchesColumnVisible
        {
            get => _filesMatchesColumnVisible;
            set
            {
                if (_filesMatchesColumnVisible != value)
                {
                    _filesMatchesColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesMatchesColumnVisible(value);
                }
            }
        }

        public bool FilesPathColumnVisible
        {
            get => _filesPathColumnVisible;
            set
            {
                if (_filesPathColumnVisible != value)
                {
                    _filesPathColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesPathColumnVisible(value);
                }
            }
        }

        public bool FilesExtColumnVisible
        {
            get => _filesExtColumnVisible;
            set
            {
                if (_filesExtColumnVisible != value)
                {
                    _filesExtColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesExtColumnVisible(value);
                }
            }
        }

        public bool FilesEncodingColumnVisible
        {
            get => _filesEncodingColumnVisible;
            set
            {
                if (_filesEncodingColumnVisible != value)
                {
                    _filesEncodingColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesEncodingColumnVisible(value);
                }
            }
        }

        public bool FilesDateModifiedColumnVisible
        {
            get => _filesDateModifiedColumnVisible;
            set
            {
                if (_filesDateModifiedColumnVisible != value)
                {
                    _filesDateModifiedColumnVisible = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultFilesDateModifiedColumnVisible(value);
                }
            }
        }

        public Models.SizeUnit SizeUnit
        {
            get => _sizeUnit;
            set
            {
                if (_sizeUnit != value)
                {
                    _sizeUnit = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultSizeUnit(value);
                }
            }
        }

        // Culture-aware string comparison properties
        public Models.StringComparisonMode StringComparisonMode
        {
            get => _stringComparisonMode;
            set
            {
                if (_stringComparisonMode != value)
                {
                    _stringComparisonMode = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultStringComparisonMode(value);
                }
            }
        }

        public Models.UnicodeNormalizationMode UnicodeNormalizationMode
        {
            get => _unicodeNormalizationMode;
            set
            {
                if (_unicodeNormalizationMode != value)
                {
                    _unicodeNormalizationMode = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultUnicodeNormalizationMode(value);
                }
            }
        }

        public bool DiacriticSensitive
        {
            get => _diacriticSensitive;
            set
            {
                if (_diacriticSensitive != value)
                {
                    _diacriticSensitive = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultDiacriticSensitive(value);
                }
            }
        }

        public string Culture
        {
            get => _culture;
            set
            {
                if (_culture != value)
                {
                    _culture = value;
                    OnPropertyChanged();
                    Services.SettingsService.SetDefaultCulture(value);
                }
            }
        }

        public bool IsFilesSearch
        {
            get => _isFilesSearch;
            set
            {
                if (_isFilesSearch != value)
                {
                    _isFilesSearch = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            private set => SetIsSearching(value);
        }

        public bool CanSearch => !string.IsNullOrWhiteSpace(SearchPath) && 
                                 !string.IsNullOrWhiteSpace(SearchTerm) && 
                                 !_isSearching;

        /// <summary>
        /// Returns true when the Search/Stop button should be enabled.
        /// Enabled when: can start a search, OR a Search operation is in progress (to allow stopping).
        /// Disabled when a Replace operation is in progress.
        /// </summary>
        public bool CanSearchOrStop => (!string.IsNullOrWhiteSpace(SearchPath) && 
                                        !string.IsNullOrWhiteSpace(SearchTerm) && 
                                        !_isSearching) || 
                                       (_isSearching && !_isReplaceOperation);

        public bool CanReplace => !string.IsNullOrWhiteSpace(SearchPath) && 
                                  !string.IsNullOrWhiteSpace(SearchTerm) && 
                                  !string.IsNullOrWhiteSpace(ReplaceWith) && 
                                  !_isSearching &&
                                  !IsDockerModeActive;

        /// <summary>
        /// Returns true when the Replace/Stop button should be enabled.
        /// Enabled when: can start a replace, OR a Replace operation is in progress (to allow stopping).
        /// Disabled when a Search operation is in progress.
        /// </summary>
        public bool CanReplaceOrStop => ((!string.IsNullOrWhiteSpace(SearchPath) && 
                                          !string.IsNullOrWhiteSpace(SearchTerm) && 
                                          !string.IsNullOrWhiteSpace(ReplaceWith) && 
                                          !_isSearching &&
                                          !IsDockerModeActive) || 
                                        (_isSearching && _isReplaceOperation));

        public ObservableCollection<SearchResult> SearchResults { get; }
        public ObservableCollection<FileSearchResult> FileSearchResults { get; }


        public async Task PerformSearchAsync()
        {
            if (!CanSearch)
                return;

            // Cancel any existing search
            CancelSearch();
            
            // Create new cancellation token source for this search
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            // Mark this as a Search operation (not Replace)
            _isReplaceOperation = false;
            
            SetIsSearching(true);
            SearchResults.Clear();
            FileSearchResults.Clear();
            SetStatus("SearchingStatus");
            
            // Start timing the search
            _searchStopwatch.Restart();

            try
            {
                var effectiveSearchPath = SearchPath;
                List<SearchResult>? dockerGrepResults = null;

                if (IsDockerModeActive && SelectedDockerContainer != null)
                {
                    try
                    {
                        await EnsureDockerReadyAsync(cancellationToken);
                        
                        // First, try to search directly in the container using Docker API + grep
                        // This is faster as it doesn't require copying files locally
                        var grepResult = await _dockerSearchService.SearchInContainerAsync(
                            SelectedDockerContainer,
                            SearchPath,
                            SearchTerm,
                            IsRegexSearch,
                            SearchCaseSensitive,
                            RespectGitignore,
                            IncludeSystemFiles,
                            IncludeSubfolders,
                            IncludeHiddenItems,
                            IncludeBinaryFiles,
                            IncludeSymbolicLinks,
                            MatchFileNames,
                            ExcludeDirs,
                            cancellationToken);
                        
                        if (grepResult.Success)
                        {
                            // Grep succeeded, use its results directly
                            Log($"Docker grep search succeeded with {grepResult.Results.Count} results");
                            dockerGrepResults = grepResult.Results;
                        }
                        else if (grepResult.GrepNotAvailable)
                        {
                            // Grep not available in container, fall back to mirror approach
                            Log($"Docker grep not available ({grepResult.ErrorMessage}), falling back to mirror approach");
                            effectiveSearchPath = await PrepareDockerMirrorAsync(SelectedDockerContainer, cancellationToken);
                        }
                        else
                        {
                            // Grep failed for another reason, log and fall back to mirror approach
                            Log($"Docker grep failed ({grepResult.ErrorMessage}), falling back to mirror approach");
                            effectiveSearchPath = await PrepareDockerMirrorAsync(SelectedDockerContainer, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _searchStopwatch.Stop();
                        // For symlink errors, show a more user-friendly message
                        if (ex is Services.DockerSymlinkException symlinkEx)
                        {
                            SetStatus("ErrorStatus", GetString("DockerSymlinkErrorMessage", symlinkEx.OriginalError));
                        }
                        else
                        {
                            SetStatus("ErrorStatus", ex.Message);
                        }
                        NotifyDockerError("DockerMirrorErrorMessage", ex);
                        return;
                    }
                }
                else
                {
                    await _dockerSearchService.CleanupMirrorAsync(_activeDockerMirror, CancellationToken.None);
                    _activeDockerMirror = null;
                }

                // Use Docker grep results if available, otherwise perform local search
                List<SearchResult> results;
                if (dockerGrepResults != null)
                {
                    results = dockerGrepResults;
                }
                else
                {
                    results = await _searchService.SearchAsync(
                        effectiveSearchPath,
                        SearchTerm,
                        IsRegexSearch,
                        RespectGitignore,
                        SearchCaseSensitive,
                        IncludeSystemFiles,
                        IncludeSubfolders,
                        IncludeHiddenItems,
                        IncludeBinaryFiles,
                        IncludeSymbolicLinks,
                        SizeLimitType,
                        SizeLimitKB,
                        SizeUnit,
                        MatchFileNames,
                        ExcludeDirs,
                        UseWindowsSearchIndex,
                        StringComparisonMode,
                        UnicodeNormalizationMode,
                        DiacriticSensitive,
                        Culture,
                        cancellationToken);
                }
                
                if (IsFilesSearch)
                {
                    // Aggregate results by file
                    var fileGroups = results.GroupBy(r => r.FullPath);
                    
                    foreach (var group in fileGroups)
                    {
                        var firstResult = group.First();
                        var filePath = firstResult.FullPath;
                        
                        try
                        {
                            // Convert WSL path to Windows path if needed
                            string windowsPath = ConvertWslPathToWindows(filePath);
                            
                            var fileInfo = new FileInfo(windowsPath);
                            string encoding = DetectFileEncoding(windowsPath);
                            
                            var fileResult = new FileSearchResult
                            {
                                FileName = firstResult.FileName,
                                Size = fileInfo.Exists ? fileInfo.Length : 0,
                                MatchCount = group.Sum(r => r.MatchCount),
                                FullPath = filePath,
                                RelativePath = firstResult.RelativePath,
                                Extension = Path.GetExtension(filePath).TrimStart('.'),
                                Encoding = encoding,
                                DateModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue
                            };
                            
                            FileSearchResults.Add(fileResult);
                        }
                        catch (Exception ex)
                        {
                            Log($"Error getting file info for {filePath}: {ex.Message}");
                            // If we can't get file info, still add the result with defaults
                            // This is a minor error, so we don't show a notification
                            var fileResult = new FileSearchResult
                            {
                                FileName = firstResult.FileName,
                                Size = 0,
                                MatchCount = group.Sum(r => r.MatchCount),
                                FullPath = filePath,
                                RelativePath = firstResult.RelativePath,
                                Extension = Path.GetExtension(filePath).TrimStart('.'),
                                Encoding = "Unknown",
                                DateModified = DateTime.MinValue
                            };
                            
                            FileSearchResults.Add(fileResult);
                        }
                    }
                    
                    int totalMatches = FileSearchResults.Sum(f => f.MatchCount);
                    int fileCount = FileSearchResults.Count;
                    
                    // Apply default sorting by MatchCount descending
                    _currentSortField = SearchResultSortField.MatchCount;
                    _isSortDescending = true;
                    
                    Func<FileSearchResult, object> keySelector = r => r.MatchCount;
                    IEnumerable<FileSearchResult> ordered = FileSearchResults.OrderByDescending(keySelector);
                    var sortedList = ordered.ToList();
                    FileSearchResults.Clear();
                    foreach (var result in sortedList)
                    {
                        FileSearchResults.Add(result);
                    }
                    
                    _searchStopwatch.Stop();
                    SetStatus("FoundMatchesStatus", totalMatches, fileCount, FormatElapsedTime(_searchStopwatch.Elapsed));
                }
                else
                {
                    foreach (var result in results)
                    {
                        SearchResults.Add(result);
                    }

                    int totalMatches = results.Sum(r => r.MatchCount);
                    int fileCount = results.GroupBy(r => r.FullPath).Count();
                    
                    // Apply default sorting by FileName ascending
                    _currentSortField = SearchResultSortField.None;
                    _isSortDescending = false;
                    SortResults(SearchResultSortField.FileName);
                    
                    _searchStopwatch.Stop();
                    SetStatus("FoundMatchesStatus", totalMatches, fileCount, FormatElapsedTime(_searchStopwatch.Elapsed));
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled by user
                _searchStopwatch.Stop();
                SetStatus("ReadyStatus");
            }
            catch (Exception ex)
            {
                _searchStopwatch.Stop();
                SetStatus("ErrorStatus", ex.Message);
                _notificationService.ShowError(
                    GetString("SearchErrorTitle"),
                    GetString("SearchErrorMessage", ex.Message));
            }
            finally
            {
                SetIsSearching(false);
            }
        }

        /// <summary>
        /// Cancels the current search operation if one is in progress.
        /// </summary>
        public void CancelSearch()
        {
            try
            {
                if (_searchCancellationTokenSource != null && !_searchCancellationTokenSource.IsCancellationRequested)
                {
                    _searchCancellationTokenSource.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
                // Token source was already disposed, ignore
            }
            catch (Exception ex)
            {
                Log($"CancelSearch error: {ex.Message}");
            }
            finally
            {
                _searchCancellationTokenSource?.Dispose();
                _searchCancellationTokenSource = null;
            }
        }

        public async Task PerformReplaceAsync()
        {
            if (!CanSearch || string.IsNullOrWhiteSpace(ReplaceWith))
                return;

            // Cancel any existing search
            CancelSearch();
            
            // Create new cancellation token source for this replace operation
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            // Mark this as a Replace operation
            _isReplaceOperation = true;
            
            // Automatically switch to Files mode for replace operations
            IsFilesSearch = true;

            SetIsSearching(true);
            SearchResults.Clear();
            FileSearchResults.Clear();
            SetStatus("ReplacingStatus");
            
            // Start timing the replace operation
            _searchStopwatch.Restart();

            try
            {
                var results = await _searchService.ReplaceAsync(
                    SearchPath,
                    SearchTerm,
                    ReplaceWith,
                    IsRegexSearch,
                    RespectGitignore,
                    SearchCaseSensitive,
                    IncludeSystemFiles,
                    IncludeSubfolders,
                    IncludeHiddenItems,
                    IncludeBinaryFiles,
                    IncludeSymbolicLinks,
                    SizeLimitType,
                    SizeLimitKB,
                    SizeUnit,
                    MatchFileNames,
                    ExcludeDirs,
                    StringComparisonMode,
                    UnicodeNormalizationMode,
                    DiacriticSensitive,
                    Culture,
                    cancellationToken);

                foreach (var result in results)
                {
                    FileSearchResults.Add(result);
                }

                int totalMatches = results.Sum(r => r.MatchCount);
                int fileCount = results.Count;
                _searchStopwatch.Stop();
                SetStatus("ReplacedMatchesStatus", totalMatches, fileCount, FormatElapsedTime(_searchStopwatch.Elapsed));
            }
            catch (OperationCanceledException)
            {
                // Replace was cancelled by user
                _searchStopwatch.Stop();
                SetStatus("ReadyStatus");
            }
            catch (Exception ex)
            {
                _searchStopwatch.Stop();
                SetStatus("ErrorStatus", ex.Message);
                _notificationService.ShowError(
                    GetString("SearchErrorTitle"),
                    GetString("SearchErrorMessage", ex.Message));
            }
            finally
            {
                SetIsSearching(false);
            }
        }

        public void ClearResults()
        {
            SearchResults.Clear();
            FileSearchResults.Clear();
            SetStatus("ReadyStatus");
            SetIsSearching(false);
            ScheduleMirrorCleanup();
        }

        public void SortResults(SearchResultSortField field)
        {
            if (field == SearchResultSortField.None)
                return;

            if (IsFilesSearch)
            {
                if (FileSearchResults.Count <= 1)
                    return;

                if (_currentSortField == field)
                {
                    _isSortDescending = !_isSortDescending;
                }
                else
                {
                    _currentSortField = field;
                    _isSortDescending = false;
                }

                Func<FileSearchResult, object> keySelector = field switch
                {
                    SearchResultSortField.FileName => r => r.FileName,
                    SearchResultSortField.RelativePath => r => r.RelativePath,
                    SearchResultSortField.Extension => r => r.Extension,
                    SearchResultSortField.Encoding => r => r.Encoding,
                    SearchResultSortField.MatchCount => r => r.MatchCount,
                    _ => r => r.FileName
                };

                IEnumerable<FileSearchResult> ordered = _isSortDescending
                    ? FileSearchResults.OrderByDescending(keySelector)
                    : FileSearchResults.OrderBy(keySelector);

                var sortedList = ordered.ToList();
                FileSearchResults.Clear();
                foreach (var result in sortedList)
                {
                    FileSearchResults.Add(result);
                }
            }
            else
            {
                if (SearchResults.Count <= 1)
                    return;

                if (_currentSortField == field)
                {
                    _isSortDescending = !_isSortDescending;
                }
                else
                {
                    _currentSortField = field;
                    _isSortDescending = false;
                }

                Func<SearchResult, object> keySelector = field switch
                {
                    SearchResultSortField.FileName => r => r.FileName,
                    SearchResultSortField.LineNumber => r => r.LineNumber,
                    SearchResultSortField.ColumnNumber => r => r.ColumnNumber,
                    SearchResultSortField.RelativePath => r => r.RelativePath,
                    _ => r => r.FileName
                };

                IEnumerable<SearchResult> ordered = _isSortDescending
                    ? SearchResults.OrderByDescending(keySelector)
                    : SearchResults.OrderBy(keySelector);

                var sortedList = ordered.ToList();
                SearchResults.Clear();
                foreach (var result in sortedList)
                {
                    SearchResults.Add(result);
                }
            }
        }

        private void SetIsSearching(bool value)
        {
            if (_isSearching == value)
            {
                return;
            }

            _isSearching = value;
            OnPropertyChanged(nameof(IsSearching));
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(CanSearchOrStop));
            OnPropertyChanged(nameof(CanReplace));
            OnPropertyChanged(nameof(CanReplaceOrStop));
        }

        private async Task InitializeDockerSupportAsync()
        {
            try
            {
                _isDockerCliAvailable = await _dockerSearchService.IsDockerAvailableAsync();
                OnPropertyChanged(nameof(CanSelectDockerContainer));

                if (_isDockerCliAvailable)
                {
                    var containers = await _dockerSearchService.GetContainersAsync();
                    UpdateDockerContainers(containers);
                }
            }
            catch (Exception ex)
            {
                _isDockerCliAvailable = false;
                OnPropertyChanged(nameof(CanSelectDockerContainer));
                NotifyDockerError("DockerInitializationErrorMessage", ex);
            }
        }

        public async Task RefreshDockerContainersAsync(CancellationToken cancellationToken = default)
        {
            if (!IsDockerSearchGloballyEnabled)
                return;

            if (!_isDockerCliAvailable)
            {
                _isDockerCliAvailable = await _dockerSearchService.IsDockerAvailableAsync(cancellationToken);
                OnPropertyChanged(nameof(CanSelectDockerContainer));
            }

            if (!_isDockerCliAvailable)
                return;

            try
            {
                var containers = await _dockerSearchService.GetContainersAsync(cancellationToken);
                UpdateDockerContainers(containers);
            }
            catch (Exception ex)
            {
                NotifyDockerError("DockerRefreshErrorMessage", ex);
            }
        }

        private void UpdateDockerContainers(IReadOnlyList<DockerContainerInfo> containers)
        {
            DockerContainers = containers;

            if (_selectedDockerContainer != null &&
                !containers.Any(c => string.Equals(c.Id, _selectedDockerContainer.Id, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedDockerContainer = null;
            }

            RebuildDockerOptions();
        }

        private void RebuildDockerOptions()
        {
            var options = new List<DockerContainerOption>
            {
                new DockerContainerOption
                {
                    Label = GetString("DockerLocalDiskOption"),
                    Container = null
                }
            };

            foreach (var container in _dockerContainers)
            {
                options.Add(new DockerContainerOption
                {
                    Label = container.DisplayName,
                    Container = container
                });
            }

            DockerContainerOptions = options;

            var matchingOption = options.FirstOrDefault(o =>
                o.Container != null &&
                _selectedDockerContainer != null &&
                string.Equals(o.Container.Id, _selectedDockerContainer.Id, StringComparison.OrdinalIgnoreCase));

            _selectedDockerOption = matchingOption ?? options.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedDockerOption));
        }

        private async Task EnsureDockerReadyAsync(CancellationToken cancellationToken)
        {
            if (!_isDockerCliAvailable)
            {
                _isDockerCliAvailable = await _dockerSearchService.IsDockerAvailableAsync(cancellationToken);
                OnPropertyChanged(nameof(CanSelectDockerContainer));
                if (!_isDockerCliAvailable)
                {
                    var message = GetString("DockerUnavailableMessage");
                    _notificationService.ShowError(GetString("DockerSearchErrorTitle"), message);
                    throw new InvalidOperationException(message);
                }
            }

            if (DockerContainers.Count == 0)
            {
                await RefreshDockerContainersAsync(cancellationToken);
            }

            if (_selectedDockerContainer == null && DockerContainers.Count > 0)
            {
                SelectedDockerContainer = DockerContainers[0];
            }

            if (_selectedDockerContainer == null)
            {
                var message = GetString("DockerContainerNotSelectedMessage");
                _notificationService.ShowError(GetString("DockerSearchErrorTitle"), message);
                throw new InvalidOperationException(message);
            }
        }

        private async Task<string> PrepareDockerMirrorAsync(DockerContainerInfo container, CancellationToken cancellationToken)
        {
            await _dockerSearchService.CleanupMirrorAsync(_activeDockerMirror, cancellationToken);
            _activeDockerMirror = null;

            var mirror = await _dockerSearchService.MirrorPathAsync(container, SearchPath, IncludeSymbolicLinks, cancellationToken);
            _activeDockerMirror = mirror;
            return mirror.LocalSearchPath;
        }

        private void ScheduleMirrorCleanup()
        {
            if (_activeDockerMirror == null)
                return;

            var mirror = _activeDockerMirror;
            _activeDockerMirror = null;
            _ = _dockerSearchService.CleanupMirrorAsync(mirror, CancellationToken.None);
        }

        private void NotifyDockerError(string messageResourceKey, Exception ex)
        {
            try
            {
                Log($"Docker error: {ex}");
                
                // Check if this is a symlink-related error
                if (ex is Services.DockerSymlinkException symlinkEx)
                {
                    var symlinkMessage = GetString("DockerSymlinkErrorMessage", symlinkEx.OriginalError);
                    _notificationService.ShowError(GetString("DockerSearchErrorTitle"), symlinkMessage);
                    return;
                }
                
                var messageTemplate = GetString(messageResourceKey);
                var errorMessage = string.Format(messageTemplate, ex.Message);
                _notificationService.ShowError(GetString("DockerSearchErrorTitle"), errorMessage);
            }
            catch (Exception logrex)
            {
                Log($"NotifyDockerError fallback ERROR: {logrex}");
            }
        }

        public string ResolveDockerPath(string? localPath)
        {
            if (!IsDockerModeActive || _activeDockerMirror == null || string.IsNullOrWhiteSpace(localPath))
            {
                return localPath ?? string.Empty;
            }

            try
            {
                var relative = Path.GetRelativePath(_activeDockerMirror.LocalSearchPath, localPath);
                if (string.IsNullOrWhiteSpace(relative) || relative == "." || relative == ".\\")
                {
                    return _activeDockerMirror.ContainerPath;
                }

                var basePath = _activeDockerMirror.ContainerPath.TrimEnd('/');
                var combined = $"{basePath}/{relative.Replace('\\', '/')}";
                while (combined.Contains("//", StringComparison.Ordinal))
                {
                    combined = combined.Replace("//", "/", StringComparison.Ordinal);
                }
                return combined;
            }
            catch
            {
                return localPath;
            }
        }

        private void OnDockerSearchEnabledChanged(object? sender, bool enabled)
        {
            IsDockerSearchGloballyEnabled = enabled;
            if (enabled)
            {
                _ = InitializeDockerSupportAsync();
            }
            else
            {
                ScheduleMirrorCleanup();
            }
        }

        public void Dispose()
        {
            SettingsService.DockerSearchEnabledChanged -= OnDockerSearchEnabledChanged;
            ScheduleMirrorCleanup();
        }

        private string ConvertWslPathToWindows(string wslPath)
        {
            // If it's already a Windows path, return as-is
            if (!wslPath.StartsWith("/") || (wslPath.Length > 2 && wslPath[1] == ':'))
            {
                return wslPath;
            }

            // Use wsl.localhost format (keep the same format as the search path)
            string wslPrefix = "\\\\wsl.localhost";
            
            if (string.IsNullOrWhiteSpace(SearchPath))
            {
                // Fallback: try to construct from the WSL path directly
                var defaultDistribution = "Ubuntu-24.04";
                var fallbackWslParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var fallbackPath = $"{wslPrefix}\\{defaultDistribution}\\{string.Join("\\", fallbackWslParts)}";
                return fallbackPath;
            }

            // If search path is a WSL path (\\wsl.localhost\... or \\wsl$\...)
            if (SearchPath.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase) ||
                SearchPath.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the distribution name
                var parts = SearchPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var distribution = parts[1];
                    
                    // Use the same prefix format as the search path
                    if (SearchPath.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase))
                    {
                        wslPrefix = "\\\\wsl$";
                    }
                    
                    // Convert WSL path to Windows path
                    var wslPathParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var windowsPath = $"{wslPrefix}\\{distribution}\\{string.Join("\\", wslPathParts)}";
                    return windowsPath;
                }
            }

            // Fallback: try to construct from the WSL path directly
            var defaultDist = "Ubuntu-24.04";
            var finalWslParts = wslPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var fallback = $"{wslPrefix}\\{defaultDist}\\{string.Join("\\", finalWslParts)}";
            return fallback;
        }

        private string DetectFileEncoding(string filePath)
        {
            try
            {
                // For WSL paths, we can't reliably detect encoding, so return UTF-8
                if (filePath.StartsWith("\\\\wsl", StringComparison.OrdinalIgnoreCase))
                {
                    return "UTF-8";
                }

                if (!File.Exists(filePath))
                {
                    return "Unknown";
                }

                // Read first few bytes to detect encoding
                using (var fileStream = File.OpenRead(filePath))
                {
                    var buffer = new byte[Math.Min(4096, fileStream.Length)];
                    int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        return "UTF-8";
                    }

                    // Check for BOM
                    if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    {
                        return "UTF-8";
                    }
                    if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                    {
                        return "UTF-16 LE";
                    }
                    if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                    {
                        return "UTF-16 BE";
                    }

                    // Try to detect encoding by attempting to decode
                    // This is a simple heuristic - for production, consider using a library
                    try
                    {
                        var utf8 = Encoding.UTF8.GetString(buffer);
                        // If it decodes without errors, likely UTF-8
                        return "UTF-8";
                    }
                    catch
                    {
                        return "Binary/Unknown";
                    }
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private void UpdateTabTitle()
        {
            if (string.IsNullOrWhiteSpace(SearchPath))
            {
                // Reset to original title if path is cleared
                TabTitle = _originalTabTitle;
                return;
            }

            try
            {
                var path = SearchPath.TrimEnd('\\', '/');
                
                // For very short paths (like "D:\" or "D:\files"), show the whole path
                if (path.Length <= 30)
                {
                    TabTitle = path;
                    return;
                }

                // Split path into parts
                var parts = new List<string>();
                
                // Handle UNC paths (\\server\share\...)
                if (path.StartsWith(@"\\"))
                {
                    var uncParts = path.Substring(2).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (uncParts.Length > 0)
                    {
                        // Add the server name as first part
                        parts.Add(@"\\" + uncParts[0]);
                        // Add remaining parts
                        for (int i = 1; i < uncParts.Length; i++)
                        {
                            parts.Add(uncParts[i]);
                        }
                    }
                }
                else
                {
                    // Handle regular paths (C:\folder\...)
                    // Split already handles drive letters correctly: "C:\Users" -> ["C:", "Users"]
                    parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // If we have 2 or fewer parts, show the whole path
                if (parts.Count <= 2)
                {
                    TabTitle = path;
                    return;
                }

                // For longer paths, show first part + ... + last part
                var firstPart = parts[0];
                var lastPart = parts[parts.Count - 1];
                TabTitle = $"{firstPart}\\...\\{lastPart}";
            }
            catch
            {
                // Keep current title if path parsing fails
            }
        }

        public void RefreshLocalization()
        {
            try
            {
                StatusText = FormatStatus(_statusResourceKey, _statusResourceArgs);
                RebuildDockerOptions();
            }
            catch (Exception ex)
            {
                Log($"RefreshLocalization error: {ex.Message}");
            }
        }

        private void SetStatus(string key, params object[] args)
        {
            _statusResourceKey = string.IsNullOrWhiteSpace(key) ? "ReadyStatus" : key;
            _statusResourceArgs = args?.Length > 0 ? args.ToArray() : Array.Empty<object>();
            StatusText = FormatStatus(_statusResourceKey, _statusResourceArgs);
        }

        private string FormatStatus(string key, object[] args)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return args != null && args.Length > 0
                ? GetString(key, args)
                : GetString(key);
        }

        private string GetString(string key) =>
            _localizationService.GetLocalizedString(key);

        private string GetString(string key, params object[] args) =>
            _localizationService.GetLocalizedString(key, args);

        /// <summary>
        /// Formats a TimeSpan into a human-readable string.
        /// - Less than 30 seconds: shows with milliseconds (e.g., "12.43 seconds")
        /// - 30-59 seconds: shows whole seconds (e.g., "45 seconds")
        /// - 60+ seconds but under 60 minutes: shows minutes and seconds (e.g., "1 minute 9 seconds")
        /// - 60+ minutes: shows hours and minutes (e.g., "1 hour 9 minutes")
        /// </summary>
        private string FormatElapsedTime(TimeSpan elapsed)
        {
            var totalSeconds = elapsed.TotalSeconds;
            var totalMinutes = elapsed.TotalMinutes;

            if (totalMinutes >= 60)
            {
                // 60+ minutes: show hours and minutes
                int hours = (int)Math.Floor(totalMinutes / 60);
                int minutes = (int)Math.Floor(totalMinutes % 60);
                
                string hourUnit = hours == 1 ? GetString("TimeHourSingular") : GetString("TimeHourPlural");
                string minuteUnit = minutes == 1 ? GetString("TimeMinuteSingular") : GetString("TimeMinutePlural");
                
                if (minutes == 0)
                {
                    return $"{hours} {hourUnit}";
                }
                return $"{hours} {hourUnit} {minutes} {minuteUnit}";
            }
            else if (totalSeconds >= 60)
            {
                // 60+ seconds but under 60 minutes: show minutes and seconds
                int minutes = (int)Math.Floor(totalSeconds / 60);
                int seconds = (int)Math.Floor(totalSeconds % 60);
                
                string minuteUnit = minutes == 1 ? GetString("TimeMinuteSingular") : GetString("TimeMinutePlural");
                string secondUnit = seconds == 1 ? GetString("TimeSecondSingular") : GetString("TimeSecondPlural");
                
                if (seconds == 0)
                {
                    return $"{minutes} {minuteUnit}";
                }
                return $"{minutes} {minuteUnit} {seconds} {secondUnit}";
            }
            else if (totalSeconds >= 30)
            {
                // 30-59 seconds: show whole seconds only
                int seconds = (int)Math.Round(totalSeconds);
                string secondUnit = seconds == 1 ? GetString("TimeSecondSingular") : GetString("TimeSecondPlural");
                return $"{seconds} {secondUnit}";
            }
            else
            {
                // Less than 30 seconds: show with milliseconds
                string secondUnit = totalSeconds == 1.0 ? GetString("TimeSecondSingular") : GetString("TimeSecondPlural");
                return $"{totalSeconds:F2} {secondUnit}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateWindowsSearchOptionAvailability()
        {
            var shouldEnable = !IsRegexSearch && !IsDockerModeActive && IsWindowsSearchEligiblePath(SearchPath);

            if (!shouldEnable && _useWindowsSearchIndex)
            {
                _useWindowsSearchIndex = false;
                OnPropertyChanged(nameof(UseWindowsSearchIndex));
            }

            IsWindowsSearchOptionEnabled = shouldEnable;
        }

        private static bool IsWindowsSearchEligiblePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var trimmed = path.Trim();

            if (trimmed.StartsWith("\\\\wsl", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("\\mnt\\", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/"))
            {
                return false;
            }

            return trimmed.Length >= 2 &&
                   trimmed[1] == ':' &&
                   char.IsLetter(trimmed[0]);
        }
    }
}

