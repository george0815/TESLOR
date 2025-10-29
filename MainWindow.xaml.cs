using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;


namespace SimpleLoadOrderOrganizer
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : System.Windows.Window
    {


        private Games games = new();
        private readonly DataContractJsonSerializer serializer = new(typeof(Games));
        private readonly FileStream? fs;
        int index;
        bool conflictCheckLock = false;




        public MainWindow(){InitializeComponent(); }


        private async Task RunAsyncOperation(Func<IProgress<double>, Task> operation, Action onCompleted, string progressMsg)
        {
            SetUIEnabled(false, progressMsg);
            var progress = new Progress<double>(p => loadingBar.Value = p);
            await operation(progress);
            onCompleted();
        }

        // Async wrapper for plugin loading
        private async Task LoadPluginsAsync(){await RunAsyncOperation(progress => Task.Run(() => games.gamesList[index].LoadPlugins(progress)), BackgroundWorker_OnCompleted,$"Loading {games.gamesList[index].Name} plugins...");}


        // Async wrapper for conflict check
        private async Task CheckConflictsAsync() { await RunAsyncOperation(progress => Task.Run(() => games.gamesList[index].OverlapCheck(progress)),BackgroundWorker_OnCompletedConflict, "Checking for conflicts...");}


        //WHEN LOADING PLUGINS FINISHES
        private void BackgroundWorker_OnCompleted()
        {
            conflictCheckLock = true;
            RefreshDataContext(game.SelectedIndex);

            loadingBar.Value = 0;
            loadingBar.Visibility = Visibility.Hidden;

            if (games.gamesList[game.SelectedIndex].ConflictCheck)
            {
                progressLabel.Content = "Checking for conflicts...";
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;
                _ = CheckConflictsAsync();
            }
            else
            {
                SetUIEnabled(true);
            }
        }



        //WHEN CHECKING FOR PLUGIN CONFLICTS FINISHES
        private void BackgroundWorker_OnCompletedConflict()
        {
            loadingBar.Value = 0;
            warningLabel.Visibility = Visibility.Hidden;
            loadingBar.Visibility = Visibility.Hidden;
            conflictCheckLock = true;

            RefreshDataContext(game.SelectedIndex);
            SetUIEnabled(true);
        }



        //WHEN COMBOBOX INDEX CHANGES
        public async void Game_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            conflictCheckLock = true;
            games.GameID = game.SelectedIndex;

            if (games.gamesList[game.SelectedIndex].LoadOrder == null &&
                IsValidGame(game.SelectedIndex))
            {
                index = game.SelectedIndex;
                await LoadPluginsAsync();
            }
            else if (IsValidGame(game.SelectedIndex))
            {
                DataContext = games.gamesList[game.SelectedIndex];
                index = game.SelectedIndex;
                editMasters.IsEnabled = true;
                conflictCheckBox.IsEnabled = true;
                warningLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                index = game.SelectedIndex;
                DataContext = games.gamesList[game.SelectedIndex];
                editMasters.IsEnabled = false;
                conflictCheckBox.IsEnabled = false;
                warningLabel.Visibility = Visibility.Visible;
                progressLabel.Content = "Created by George S.";
            }
            conflictCheckLock = false;
        }



        //SAVES CONFIG TO WORKING DIRECTORY
        public void SaveConfig()
        {
            //Tries to save config by serializing the class into JSON, displays messagebox is there is an exception
            try
            {
                using var fs = File.Create("cfg.json");
                serializer.WriteObject(fs, games);
            }
            catch (Exception) { System.Windows.Forms.MessageBox.Show("Error saving config file, this may be because the file is open in another program, or that this program wasn't ran as administrator...", "Error"); }

        }



        //GAME FOLDER BUTTON
        private void GameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string expectedFile = game.SelectedIndex == 0 ? "Data Files" : "Data";

            // Use your helper method
            if (TrySelectFolder(out string? selectedPath, expectedFile, false))
            {
                gameFolderBox.Text = selectedPath!;
                games.gamesList[game.SelectedIndex].GameFolder = selectedPath!;
                LoadPlugins();
            }
            
        }



        //PLUGIN CONFIG FOLDER BUTTTON
        private void PluginFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string expectedFile = games.GameID != 0 ? "plugins.txt" : "morrowind.ini";

            if (TrySelectFolder(out string? selectedPath, expectedFile, true))
            {
                // Set the config file path
                pluginsTextBox.Text = Path.Combine(selectedPath!, expectedFile);
                games.gamesList[game.SelectedIndex].ConfigFolder = Path.Combine(selectedPath!, expectedFile);

                // If the game folder is already valid
                if (Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder))
                {
                    LoadPlugins();
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Please enter a valid path for the game directory.", "Error");
                }
            }
        }



        //ON WINDOW LOADED
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {



            //checks if config exists, if it doesn't, create it, if it does, deserialize it into class  
            #region CONFIG CHECK

            //if config file exist, read it
            if (System.IO.File.Exists("cfg.json"))
            {

                //try to read it, if it fails, display a message box and recreate config file
                try
                {
                    using var fs = File.OpenRead("cfg.json");
                    games = serializer.ReadObject(fs) as Games ?? new Games();
                    index = games!.GameID;
                    fs.Close();
                }
                catch (Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Error reading config file, creating new one...", "Error");
                    fs?.Close();
                    System.IO.File.Delete("cfg.json");
                    games = new Games();
                    index = games.GameID;
                    SaveConfig();
                }
            }
            //if config doesn't exist, create one
            else
            {
                games = new Games();
                index = games.GameID;
                SaveConfig();
            }

            game.SelectedIndex = index;

            #endregion


            //adds selectionChanged handler, this is becuase selectionChanged is called before the above code when set in XAML, and the handler will then try to access objects that are null 
            game.SelectionChanged += Game_SelectionChanged;


            //searches for folder/plugin.txt paths for each game 
            #region FOLDER SEARCH

            foreach (Game game in games.gamesList)
            {
                //searches for game directories
                #region GAME FOLDER

                //if game directory is unknown, interegate the registry entry and find the directory 
                if (string.IsNullOrEmpty(game.GameFolder))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(game.RegKey!, writable: false);
                    if (key != null)
                    {
                        if (key.GetValue("installed path") is string path &&
                            !string.IsNullOrWhiteSpace(path) &&
                            Directory.Exists(path))
                        {
                            game.GameFolder = path;
                        }
                    }
                }


                #endregion


                //searches for plugin config directories
                #region CONFIG FILE

                //if game directory is unknown, search for it in the default directory
                //..^4 == "length - 4"
                if (game.ConfigFolder == "")
                {

                    //morrowind uses Morrowind.ini in it's game folder to store active plugins so we have to look in the game folder rather than appdata
                    if (game.Name == "The Elder Scrolls III: Morrowind" && System.IO.File.Exists(game.GameFolder + game.DefaultConfigFolder))
                    {
                        game.ConfigFolder = game.GameFolder + game.DefaultConfigFolder;
                        File.Copy(game.ConfigFolder, game.ConfigFolder[..^4] + "_backup.txt", true);
                    }
                    else if ((game.Name != "The Elder Scrolls III: Morrowind" && System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.DefaultConfigFolder)))
                    {
                        game.ConfigFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.DefaultConfigFolder;
                        File.Copy(game.ConfigFolder, game.ConfigFolder[..^4] + "_backup.txt", true);
                    }


                }
                else if (System.IO.File.Exists(game.ConfigFolder))
                {
                    File.Copy(game.ConfigFolder, game.ConfigFolder[..^4] + "_backup.txt", true);
                }

                #endregion

            }

            #endregion






            //if both folders exist,remove notice text and start loading plugins
            if (File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder))
            {
                //loads plugins and sets DataContext
                LoadPlugins();
            }
            //if not make sure the notice text is visible
            else
            {
                editMasters.IsEnabled = false;
                conflictCheckBox.IsEnabled = false;
                warningLabel.Visibility = Visibility.Visible;
                progressLabel.Content = "Created by George S.";
            }





        }

        //LOADS PLUGINS
        void LoadPlugins()
        {
            DataContext = null;
            warningLabel.Visibility = Visibility.Hidden;
            SetUIEnabled(false, "Loading " + games.gamesList[game.SelectedIndex].Name + " plugins...");
             _ = LoadPluginsAsync();
        }



        //WHEN LOAD ORDER CHANGED
        private void PluginsBox_Drop(object sender, System.Windows.DragEventArgs e) { games.gamesList[game.SelectedIndex].WasChanged = true; }// when order is changed

        private void CheckBox_Checked(object sender, RoutedEventArgs e) { games.gamesList[game.SelectedIndex].WasChanged = true; }// when plugin is enabled

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e) { games.gamesList[game.SelectedIndex].WasChanged = true; } // when plugin is disabled



        //SAVE BUTTON
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            System.IO.File.Delete("cfg.json");
            SaveConfig();

            MessageBoxResult dialogResult = System.Windows.MessageBox.Show("Currently active game's settings/load order will be saved. Do you want to save every games' settings/load order?", "Save", MessageBoxButton.YesNo);
            if (dialogResult == MessageBoxResult.Yes)
            {
                //if a games load order was changed, saves the loadorder
                foreach (Game g in games.gamesList) { if (g.WasChanged && g.LoadOrder != null) { g.WritePlugins(); } }
            }
            else if (dialogResult == MessageBoxResult.No)
            {
                //if a games load order was changed, saves the loadorder
                if (games.gamesList[game.SelectedIndex].WasChanged) { games.gamesList[game.SelectedIndex].WritePlugins(); }
            }
        }

        //WHEN CHECK FOR CONFLICTS IS CHECKED
        private async void ConflictCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!conflictCheckLock)
            {
                foreach (Plugin p in games.gamesList[game.SelectedIndex].LoadOrder)
                    p.Conflicts = null;

                await CheckConflictsAsync();
            }
        }


        private void SetUIEnabled(bool enabled, string progressMsg = "")
        {
            var controls = new System.Windows.Controls.Control[]
            {
        game, saveButton, gameFolderButton, pluginFolderButton,
        editMasters, conflictCheckBox
            };

            foreach (var control in controls)
            {
                control.IsEnabled = enabled;
            }

            loadingBar.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            progressLabel.Content = enabled ? "Created by George S." : progressMsg;
        }




        private bool IsValidGame(int i) =>
            File.Exists(games.gamesList[i].ConfigFolder) &&
            Directory.Exists(games.gamesList[i].GameFolder);

        private void RefreshDataContext(int i)
        {
            DataContext = null;
            DataContext = games.gamesList[i];
        }



        private static bool TrySelectFolder(out string? path, string expectedFile, bool isFile)
        {
            path = null;

            if (isFile)
            {
                // File selection dialog
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    FileName = "Config file",
                    DefaultExt = ".txt",
                    Filter = "Text documents (*.txt;*.ini)|*.txt;*.ini"
                };

                bool? result = dialog.ShowDialog();
                if (result != true)
                    return false;

                if (Path.GetFileName(dialog.FileName).Equals(expectedFile, StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.GetDirectoryName(dialog.FileName) + Path.DirectorySeparatorChar;
                    return true;
                }

                System.Windows.Forms.MessageBox.Show(
                    $"Could not find {expectedFile}, please choose the correct file.",
                    "Error");
                return false;
            }
            else
            {
                // Folder selection dialog
                using var dialog = new FolderBrowserDialog { InitialDirectory = "C:\\" };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return false;

                var fullPath = Path.Combine(dialog.SelectedPath, expectedFile);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    path = dialog.SelectedPath + Path.DirectorySeparatorChar;
                    return true;
                }

                System.Windows.Forms.MessageBox.Show(
                    $"Could not find {expectedFile}, please choose the correct directory.",
                    "Error");
                return false;
            }
        }



    }

}