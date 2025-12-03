using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Grex.Services;

namespace Grex.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISearchService _searchService;
        private TabViewModel? _selectedTab;

        public MainViewModel(ISearchService? searchService = null)
        {
            try
            {
                Log("MainViewModel constructor: Starting");
                _searchService = searchService ?? new SearchService();
                Log("MainViewModel constructor: SearchService created");
                Tabs = new ObservableCollection<TabViewModel>();
                Log("MainViewModel constructor: Tabs collection created");
                
                // Create initial tab
                var initialTab = new TabViewModel(_searchService, "Search 1");
                Log("MainViewModel constructor: Initial tab created");
                Tabs.Add(initialTab);
                SelectedTab = initialTab;
                Log("MainViewModel constructor: Completed");
            }
            catch (Exception ex)
            {
                Log($"MainViewModel constructor ERROR: {ex}");
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

        public ObservableCollection<TabViewModel> Tabs { get; }

        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                }
            }
        }

        public void AddTab()
        {
            var newTab = new TabViewModel(_searchService, $"Search {Tabs.Count + 1}");
            Tabs.Add(newTab);
            SelectedTab = newTab;
            OnPropertyChanged(nameof(Tabs));
            OnPropertyChanged(nameof(CanRemoveTab));
        }

        public bool CanRemoveTab => Tabs.Count > 1;

        public void RemoveTab(TabViewModel tab)
        {
            if (Tabs.Count <= 1)
                return; // Must have at least one tab

            var index = Tabs.IndexOf(tab);
            tab.Dispose();
            Tabs.Remove(tab);
            OnPropertyChanged(nameof(Tabs));
            OnPropertyChanged(nameof(CanRemoveTab));

            // Select another tab if the removed tab was selected
            if (SelectedTab == tab)
            {
                if (index > 0)
                    SelectedTab = Tabs[index - 1];
                else if (Tabs.Count > 0)
                    SelectedTab = Tabs[0];
                else
                    SelectedTab = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            foreach (var tab in Tabs.ToList())
            {
                tab.Dispose();
            }
        }
    }
}


