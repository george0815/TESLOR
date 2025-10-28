using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;


namespace SimpleLoadOrderOrganizer
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : System.Windows.Window
    {


        private Games games = new(); //holds data of all games supported by the program
        private readonly DataContractJsonSerializer serializer = new(typeof(Games)); // serilizer used for creating/serilizing conifg
        private FileStream? fs; //Filestream used for creating/serilizing config 
        readonly BackgroundWorker backgroundWorker = new();//backgroundworker used for loading plugins
        readonly BackgroundWorker backgroundWorkerConflict = new(); //background worker used for checking for plugin conflicts
        int index; //holds current index of the combobox
        bool conflictCheckLock = false; //used for deciding whether or not to add the event handler for the check for conflicts checkbox





        public MainWindow()
        {
            InitializeComponent();



            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.DoWork += LoadPluginsBackground;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_OnCompleted;

            backgroundWorkerConflict.WorkerReportsProgress = true;
            backgroundWorkerConflict.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorkerConflict.DoWork += ConflictChecksBackground;
            backgroundWorkerConflict.RunWorkerCompleted += BackgroundWorker_OnCompletedConflict;



        }

        public void LoadPluginsBackground(object? sender, DoWorkEventArgs e) { games.gamesList[index].LoadPlugins(backgroundWorker); }

        public void ConflictChecksBackground(object? sender, DoWorkEventArgs e) { games.gamesList[index].OverlapCheck(backgroundWorkerConflict); }

        void BackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e) { loadingBar.Value = e.ProgressPercentage; }




        //WHEN LOADING PLUGINS FINISHES
        void BackgroundWorker_OnCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {


            conflictCheckLock = true;


            //sets up datacontext
            DataContext = null;
            DataContext = games.gamesList[game.SelectedIndex];



            //sets up loading bar
            loadingBar.Value = 0;

            loadingBar.Visibility = Visibility.Hidden;

            //is conflict check is checked, make loading bar visible and check for conflicts
            if (games.gamesList[game.SelectedIndex].ConflictCheck == true)
            {
                progressLabel.Content = "Checking for conflicts...";
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;
                backgroundWorkerConflict.RunWorkerAsync();
            }
            //enables buttons
            else
            {
                game.IsEnabled = true;
                saveButton.IsEnabled = true;
                gameFolderButton.IsEnabled = true;
                pluginFolderButton.IsEnabled = true;
                editMasters.IsEnabled = true;
                conflictCheckBox.IsEnabled = true;
                progressLabel.Content = "Created by George S.";

                conflictCheckLock = false;



            }

        }




        //WHEN CHECKING FOR PLUGIN CONFLICTS FINISHES
        void BackgroundWorker_OnCompletedConflict(object? sender, RunWorkerCompletedEventArgs e)
        {
            //sets up loading bar
            loadingBar.Value = 0;
            warningLabel.Visibility = Visibility.Hidden;
            loadingBar.Visibility = Visibility.Hidden;
            conflictCheckLock = true;

            //refreshes datacontext
            DataContext = null;
            DataContext = games.gamesList[game.SelectedIndex];
            conflictCheckLock = false;

            //enables buttons
            game.IsEnabled = true;
            saveButton.IsEnabled = true;
            gameFolderButton.IsEnabled = true;
            pluginFolderButton.IsEnabled = true;
            editMasters.IsEnabled = true;
            conflictCheckBox.IsEnabled = true;
            progressLabel.Content = "Created by George S.";




        }



        //WHEN COMBOBOX INDEX CHANGES
        public void Game_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            conflictCheckLock = true;

            games.GameID = game.SelectedIndex;


            //if game doesn't already have a load order in memory, load plugins
            if (games.gamesList[game.SelectedIndex].LoadOrder == null && (File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder)))
            {

                //loads plugins and sets index
                index = game.SelectedIndex;
                LoadPlugins();
            }
            //if there is already a load order, just update DataContext
            else if ((File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder)))
            {
                DataContext = games.gamesList[game.SelectedIndex];
                index = game.SelectedIndex;
                warningLabel.Visibility = Visibility.Hidden;

            }
            //if one path isn't valid, show notice
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
                fs = new FileStream("cfg.json", FileMode.CreateNew);
                serializer.WriteObject(fs, games);
                fs.Close(); ;
            }
            catch (Exception) { System.Windows.Forms.MessageBox.Show("Error saving config file, this may be because the file is open in another program, or that this program wasn't ran as administrator...", "Error"); }

        }



        //GAME FOLDER BUTTON
        private void GameFolderButton_Click(object sender, RoutedEventArgs e)
        {


            //loop control
            bool loopControl = false;

            while (!loopControl)
            {

                //creates new instance of FolderBrowserDialog and sets initial directory
                FolderBrowserDialog openFolderDialog = new()
                {
                    InitialDirectory = "c:\\"
                };


                // Show open file dialog box
                DialogResult result = openFolderDialog.ShowDialog();





                // Process open file dialog box results
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    //decides which folder to search for depending on game
                    string cFolder;
                    if (game.SelectedIndex == 0) { cFolder = "Data Files"; }
                    else { cFolder = "Data"; }

                    //if config file and game folder are both valid
                    if (File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder) && File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].MandatoryFiles[0]))
                    {

                        //Get the path of specified folder
                        gameFolderBox.Text = openFolderDialog.SelectedPath + "\\";
                        games.gamesList[game.SelectedIndex].GameFolder = openFolderDialog.SelectedPath + "\\";
                        loopControl = true;
                        LoadPlugins();
                    }
                    //if only config file is valid
                    else if (File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder) && !(File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].MandatoryFiles[0])))
                    {
                        //Shows error message
                        System.Windows.Forms.MessageBox.Show("Could not find " + games.gamesList[game.SelectedIndex].MandatoryFiles[0] + ", please choose the correct directory.", "Error");
                    }
                    //if only game folder is valid
                    else if (!(File.Exists(games.gamesList[game.SelectedIndex].ConfigFolder)) && File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].MandatoryFiles[0]))
                    {
                        //Get the path of specified folder
                        gameFolderBox.Text = openFolderDialog.SelectedPath + "\\";
                        games.gamesList[game.SelectedIndex].GameFolder = openFolderDialog.SelectedPath + "\\";
                        loopControl = true;
                        //Shows error message
                        System.Windows.Forms.MessageBox.Show("Please enter a valid path for the plugin config file", "Error");
                    }
                }


                else if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    loopControl = true;
                }

            }




        }





        //PLUGIN CONFIG FOLDER BUTTTON
        private void PluginFolderButton_Click(object sender, RoutedEventArgs e)
        {

            //loop control
            bool loopControl = false;

            while (!loopControl)
            {


                // Configure open file dialog box
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    FileName = "Config file", // Default file name
                    DefaultExt = ".txt", // Default file extension
                    Filter = "Text documents (*.txt;*.ini)|*.txt;*.ini" // Filter files by extension
                };

                // Show open file dialog box
                bool? result = dialog.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    //if config file and game folder are both valid
                    if (File.Exists(dialog.FileName) && ((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.GameID != 0) ||
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.GameID == 0)) && Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder))
                    {
                        //Get the path of specified file
                        pluginsTextBox.Text = dialog.FileName;
                        games.gamesList[game.SelectedIndex].ConfigFolder = dialog.FileName;
                        loopControl = true;
                        LoadPlugins();
                    }
                    //if only game folder is valid
                    else if ((!File.Exists(dialog.FileName) || !((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.GameID != 0) ||
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.GameID == 0))) && Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder))
                    {
                        //Shows error message
                        if (games.GameID != 0) { System.Windows.Forms.MessageBox.Show("Could not find plugins.txt, please choose the correct file.", "Error"); }
                        else { System.Windows.Forms.MessageBox.Show("Could not find Morrowind.ini, please choose the correct file.", "Error"); }
                    }
                    //if only config file is valid
                    else if (File.Exists(dialog.FileName) && ((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.GameID != 0) ||
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.GameID == 0)) && !Directory.Exists(games.gamesList[game.SelectedIndex].GameFolder))
                    {
                        //Get the path of specified file
                        pluginsTextBox.Text = dialog.FileName;
                        games.gamesList[game.SelectedIndex].ConfigFolder = dialog.FileName;
                        loopControl = true;
                        //Shows error message
                        System.Windows.Forms.MessageBox.Show("Please enter a valid path for the game directory.", "Error");
                    }

                }

                else if (result == false)
                {
                    loopControl = true;
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
                    fs = new FileStream("cfg.json", FileMode.Open);
                    games = (Games?)serializer.ReadObject(fs) ?? new Games();
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
                if (game.GameFolder == "")
                {

                    //opens registry key for given game 
                    var key = Registry.LocalMachine.OpenSubKey(game.RegKey!, false);
                    string? path;

                    //checks if key is null so program doesn't crash if there isn't a registry entry
                    if (key != null)
                    {

                        path = key?.GetValue("installed path") as string;

                        //if directory is found, set directory to key value 
                        using (key) { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) { game.GameFolder = path; } }

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
            progressLabel.Visibility = Visibility.Visible;
            loadingBar.Visibility = Visibility.Visible;
            progressLabel.Content = "Loading " + games.gamesList[game.SelectedIndex].Name + " plugins...";
            game.IsEnabled = false;
            saveButton.IsEnabled = false;
            gameFolderButton.IsEnabled = false;
            pluginFolderButton.IsEnabled = false;
            editMasters.IsEnabled = false;
            conflictCheckBox.IsEnabled = false;
            backgroundWorker.RunWorkerAsync();
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

        private void ConflictCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!conflictCheckLock)
            {
                foreach (Plugin p in games.gamesList[game.SelectedIndex].LoadOrder) { p.Conflicts = null; }

                game.IsEnabled = false;
                saveButton.IsEnabled = false;
                gameFolderButton.IsEnabled = false;
                pluginFolderButton.IsEnabled = false;
                editMasters.IsEnabled = false;
                conflictCheckBox.IsEnabled = false;
                progressLabel.Content = "Checking for conflicts...";
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;
                backgroundWorkerConflict.RunWorkerAsync();
            }


        }
    }


}