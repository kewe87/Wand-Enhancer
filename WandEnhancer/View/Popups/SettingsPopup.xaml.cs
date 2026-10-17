using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WandEnhancer.Core;
using WandEnhancer.Core.Services;
using WandEnhancer.View.MainWindow;

namespace WandEnhancer.View.Popups
{
    public partial class SettingsPopup : UserControl
    {
        private CultureInfo _selectedLanguage;

        public SettingsPopup()
        {
            InitializeComponent();
            LoadLanguages();
#if ENABLE_UPDATE_NOTIFICATIONS
            var savedSettings = SettingsManager.LoadSettings();
            UpdateCheckCheckBox.IsChecked = savedSettings?.CheckUpdates ?? true;
            CheckPrereleasesCheckBox.IsChecked = savedSettings?.CheckPrereleases ?? false;
#else
            UpdateCheckRows.Visibility = Visibility.Collapsed;
#endif
        }

        private void LoadLanguages()
        {
            var items = LocalizationManager.SupportedLanguages
                .Select(c => new LanguageItem
                {
                    Culture = c,
                    DisplayName = LocalizationManager.GetLanguageDisplayName(c)
                })
                .ToList();

            LanguageComboBox.ItemsSource = items;
            
            var currentItem = items.FirstOrDefault(i => i.Culture.Name == LocalizationManager.CurrentLanguage?.Name);
            if (currentItem != null)
            {
                LanguageComboBox.SelectedItem = currentItem;
            }
            
            _selectedLanguage = LocalizationManager.CurrentLanguage;
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is LanguageItem item)
            {
                _selectedLanguage = item.Culture;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (_selectedLanguage != null &&
                (LocalizationManager.CurrentLanguage == null ||
                 _selectedLanguage.Name != LocalizationManager.CurrentLanguage.Name))
            {
                LocalizationManager.CurrentLanguage = _selectedLanguage;
            }

#if ENABLE_UPDATE_NOTIFICATIONS
            // CurrentLanguage already saved the merged settings; persist the checkbox on top.
            var settings = SettingsManager.LoadSettings() ?? new AppSettings();
            settings.CheckUpdates = UpdateCheckCheckBox.IsChecked == true;
            settings.CheckPrereleases = CheckPrereleasesCheckBox.IsChecked == true;
            SettingsManager.SaveSettings(settings);
#endif
            MainWindow.MainWindow.Instance?.ClosePopup();
        }

        // The bridge keeps the token it loaded until Wand restarts, so paired phones keep working
        // until then; the next start writes a new token and only a fresh QR scan pairs again.
        private void OnResetRemotePairingClick(object sender, RoutedEventArgs e)
        {
            string tokenPath = Path.Combine(Path.GetTempPath(), Constants.RemoteAccessTokenFileName);
            string message;
            try
            {
                File.Delete(tokenPath);
                message = LocalizationManager.Get("settings_remote_pairing_done");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                message = LocalizationManager.Format("settings_remote_pairing_failed", ex.Message);
            }

            RemotePairingStatus.Text = message;
            RemotePairingStatus.Visibility = Visibility.Visible;
        }

        private class LanguageItem
        {
            public CultureInfo Culture { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
