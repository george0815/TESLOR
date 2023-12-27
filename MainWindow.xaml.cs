using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Shapes;


namespace SimpleLoadOrderOrganizer
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
        public partial class MainWindow : System.Windows.Window
    {


        private Games games; //holds data of all games supported by the program
        private DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Games)); // serilizer used for creating/serilizing conifg
        private FileStream fs; //Filestream used for creating/serilizing config 
        BackgroundWorker backgroundWorker = new BackgroundWorker();//backgroundworker used for loading plugins
        BackgroundWorker backgroundWorkerConflict = new BackgroundWorker(); //background worker used for checking for plugin conflicts
        int index; //holds current index of the combobox
        bool hasRan = false; //used for deciding whether or not to add the event handler for the check for conflicts checkbox





        public MainWindow() 
        {
            InitializeComponent();
       


            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.DoWork += loadPluginsBackground;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_OnCompleted;

            backgroundWorkerConflict.WorkerReportsProgress = true;
            backgroundWorkerConflict.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorkerConflict.DoWork += conflictChecksBackground;
            backgroundWorkerConflict.RunWorkerCompleted += backgroundWorker_OnCompletedConflict;



        }

        public void loadPluginsBackground(object sender, DoWorkEventArgs e){games.gamesList[index].loadPlugins(backgroundWorker); }

        public void conflictChecksBackground(object sender, DoWorkEventArgs e){ games.gamesList[index].overlapCheck(backgroundWorkerConflict);}

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) { loadingBar.Value = e.ProgressPercentage; }




        //WHEN LOADING PLUGINS FINISHES
        void backgroundWorker_OnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {


            conflictCheckBox.Checked -= conflictCheckBoxChanged;


            //sets up datacontext
            DataContext = null;
            DataContext = games.gamesList[game.SelectedIndex];



            //sets up loading bar
            loadingBar.Value = 0;
           
            loadingBar.Visibility = Visibility.Hidden;

            //is conflict check is checked, make loading bar visible and check for conflicts
            if (games.gamesList[game.SelectedIndex].conflictCheck == true) {
                progressLabel.Content = "Checking for conflicts...";
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;
                backgroundWorkerConflict.RunWorkerAsync();
            }
            //enables buttons
            else
            {
                game.IsEnabled = true;
                gameFolderButton.IsEnabled = true;
                pluginFolderButton.IsEnabled = true;
                editMasters.IsEnabled = true;
                conflictCheckBox.IsEnabled = true;
                progressLabel.Content = "Created by George S.";

                conflictCheckBox.Checked += conflictCheckBoxChanged;



            }

        }
        



        //WHEN CHECKING FOR PLUGIN CONFLICTS FINISHES
        void backgroundWorker_OnCompletedConflict(object sender, RunWorkerCompletedEventArgs e)
        {
            //sets up loading bar
            loadingBar.Value = 0;
            warningLabel.Visibility = Visibility.Hidden;
            loadingBar.Visibility = Visibility.Hidden;

            conflictCheckBox.Checked -= conflictCheckBoxChanged;

            //refreshes datacontext
            DataContext = null;
            DataContext = games.gamesList[game.SelectedIndex];
            conflictCheckBox.Checked += conflictCheckBoxChanged;


            //enables buttons
            game.IsEnabled = true;
            gameFolderButton.IsEnabled = true;
            pluginFolderButton.IsEnabled = true;
            editMasters.IsEnabled = true;
            conflictCheckBox.IsEnabled = true;
            progressLabel.Content = "Created by George S.";


          

        }



        //WHEN COMBOBOX INDEX CHANGES
        public void game_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            conflictCheckBox.Checked -= conflictCheckBoxChanged;

            games.gameID = game.SelectedIndex;
            

            //if game doesn't already have a load order in memory, load plugins
            if (games.gamesList[game.SelectedIndex].loadOrder == null && (File.Exists(games.gamesList[game.SelectedIndex].configFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder))) {

                //loads plugins and sets index
                index = game.SelectedIndex;
                loadPlugins();
            }
            //if there is already a load order, just update DataContext
            else if((File.Exists(games.gamesList[game.SelectedIndex].configFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder))) {DataContext = games.gamesList[game.SelectedIndex]; index = game.SelectedIndex;}
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
            conflictCheckBox.Checked += conflictCheckBoxChanged;

        }



        //SAVES CONFIG TO WORKING DIRECTORY
        public void saveConfig()
        {
            //Tries to save config by serializing the class into JSON, displays messagebox is there is an exception
            try
            {
                fs = new FileStream("cfg.json", FileMode.CreateNew);
                serializer.WriteObject(fs, games);
                fs.Close(); ;
            }
            catch (Exception e){ System.Windows.Forms.MessageBox.Show("Error saving config file, this may be because the file is open in another program, or that this program wasn't ran as administrator...", "Error"); }

        }



        //GAME FOLDER BUTTON
        private void gameFolderButton_Click(object sender, RoutedEventArgs e)
        {


            //loop control
            bool loopControl = false;

            while (!loopControl)
            {

                //creates new instance of FolderBrowserDialog and sets initial directory
                FolderBrowserDialog openFolderDialog = new FolderBrowserDialog();
                openFolderDialog.InitialDirectory = "c:\\";


                // Show open file dialog box
                DialogResult result = openFolderDialog.ShowDialog();





                // Process open file dialog box results
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    //decides which folder to search for depending on game
                    string cFolder = "";
                    if (game.SelectedIndex == 0) { cFolder = "Data Files"; }
                    else { cFolder = "Data"; }

                    //if config file and game folder are both valid
                    if (File.Exists(games.gamesList[game.SelectedIndex].configFolder) && File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].mandatoryFiles[0]))
                    {

                        //Get the path of specified folder
                        gameFolderBox.Text = openFolderDialog.SelectedPath + "\\";
                        games.gamesList[game.SelectedIndex].gameFolder = openFolderDialog.SelectedPath + "\\";
                        loopControl = true;
                        loadPlugins();
                    }
                    //if only config file is valid
                    else if (File.Exists(games.gamesList[game.SelectedIndex].configFolder) && !(File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].mandatoryFiles[0])))
                    {
                        //Shows error message
                        System.Windows.Forms.MessageBox.Show("Could not find " + games.gamesList[game.SelectedIndex].mandatoryFiles[0] + ", please choose the correct directory.", "Error");
                    }
                    //if only game folder is valid
                    else if (!(File.Exists(games.gamesList[game.SelectedIndex].configFolder)) && File.Exists(openFolderDialog.SelectedPath + "\\" + cFolder + "\\" + games.gamesList[game.SelectedIndex].mandatoryFiles[0]))
                    {
                        //Get the path of specified folder
                        gameFolderBox.Text = openFolderDialog.SelectedPath + "\\";
                        games.gamesList[game.SelectedIndex].gameFolder = openFolderDialog.SelectedPath + "\\";
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


        //ON WINDOW CLOSE
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.IO.File.Delete("cfg.json");
            saveConfig();


            //if a games load order was changed, saves the loadorder
            foreach (Game g in games.gamesList) {if (g.wasChanged) {g.writePlugins(); } }
        }


        //PLUGIN CONFIG FOLDER BUTTTON
        private void pluginFolderButton_Click(object sender, RoutedEventArgs e)
        {

            //loop control
            bool loopControl = false;

            while (!loopControl)
            {


                // Configure open file dialog box
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.FileName = "Config file"; // Default file name
                dialog.DefaultExt = ".txt"; // Default file extension
                dialog.Filter = "Text documents (*.txt;*.ini)|*.txt;*.ini"; // Filter files by extension

                // Show open file dialog box
                bool? result = dialog.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    //if config file and game folder are both valid
                    if (File.Exists(dialog.FileName) && ((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.gameID != 0) || 
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.gameID == 0)) && Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder)) { 
                        //Get the path of specified file
                        pluginsTextBox.Text = dialog.FileName;
                        games.gamesList[game.SelectedIndex].configFolder = dialog.FileName;
                        loopControl = true;
                        loadPlugins();
                    }
                    //if only game folder is valid
                    else if ((!File.Exists(dialog.FileName) || !((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.gameID != 0) ||
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.gameID == 0))) && Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder))
                    {
                        //Shows error message
                        if (games.gameID != 0) { System.Windows.Forms.MessageBox.Show("Could not find plugins.txt, please choose the correct file.", "Error"); }
                        else { System.Windows.Forms.MessageBox.Show("Could not find Morrowind.ini, please choose the correct file.", "Error"); }
                    }
                    //if only config file is valid
                    else if (File.Exists(dialog.FileName) && ((string.Equals(dialog.SafeFileName, "plugins.txt", StringComparison.OrdinalIgnoreCase) && games.gameID != 0) ||
                        (string.Equals(dialog.SafeFileName, "morrowind.ini", StringComparison.OrdinalIgnoreCase) && games.gameID == 0)) && !Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder))
                    {
                        //Get the path of specified file
                        pluginsTextBox.Text = dialog.FileName;
                        games.gamesList[game.SelectedIndex].configFolder = dialog.FileName;
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
        


        

        //EDIT MASTERS CHECKBOX
        private void editMasters_Checked(object sender, RoutedEventArgs e){ DataContext = games.gamesList[game.SelectedIndex]; }
        private void editMasters_Unchecked(object sender, RoutedEventArgs e){ DataContext = games.gamesList[game.SelectedIndex]; }


        //ON WINDOW LOADED
        private async void Window_Loaded(object sender, RoutedEventArgs e)
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
                    games = (Games)serializer.ReadObject(fs);
                    index = games.gameID;
                    fs.Close();
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Error reading config file, creating new one...", "Error");
                    if (fs != null) { fs.Close(); }
                    System.IO.File.Delete("cfg.json");
                    games = new Games();
                    index = games.gameID;
                    saveConfig();
                }
            }
            //if config doesn't exist, create one
            else
            {
                games = new Games();
                index = games.gameID;     
                saveConfig();
            }

            game.SelectedIndex = index;

            #endregion


            //adds selectionChanged handler this is becuase selectionChanged is called before the above code when set in XAML, and the handler will then try to access objects that are null 
            game.SelectionChanged += game_SelectionChanged;


            //searches for folder/plugin.txt paths for each game 
            #region FOLDER SEARCH

            foreach (Game game in games.gamesList)
            {
                //searches for game directories
                #region GAME FOLDER

                //if game directory is unknown, interegate the registry entry and find the directory 
                if (game.gameFolder == "")
                {

                    //opens registry key for given game 
                    var key = Registry.LocalMachine.OpenSubKey(game.regKey, false);
                    string path;

                    //checks if key is null so program doesn't crash if there isn't a registry entry
                    if (key != null)
                    {

                        path = key?.GetValue("installed path") as string;

                        //if directory is found, set directory to key value 
                        using (key) { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ) { game.gameFolder = path; } }

                    }
                }

                #endregion


                //searches for plugin config directories
                #region CONFIG FILE

                //if game directory is unknown, search for it in the default directory
                if (game.configFolder == "")
                {

                    //morrowind uses Morrowind.ini in it's game folder to store active plugins so we have to look in the game folder rather than appdata
                    if (game.name == "The Elder Scrolls III: Morrowind" && System.IO.File.Exists(game.gameFolder + game.defaultConfigFolder))
                    {
                        game.configFolder = game.gameFolder + game.defaultConfigFolder;
                        File.Copy(game.configFolder, game.configFolder.Remove(game.configFolder.Length - 4) + "_backup.txt", true);
                    }
                    else if ((game.name != "The Elder Scrolls III: Morrowind" && System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.defaultConfigFolder)))
                    {
                        game.configFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.defaultConfigFolder;
                        File.Copy(game.configFolder, game.configFolder.Remove(game.configFolder.Length - 4) + "_backup.txt", true);
                    }


                }
                else if(System.IO.File.Exists(game.configFolder))
                {
                    File.Copy(game.configFolder, game.configFolder.Remove(game.configFolder.Length - 4) + "_backup.txt", true);
                }

                #endregion

            }

            #endregion





 
            //if both folders exist,remove notice text and start loading plugins
            if (File.Exists(games.gamesList[game.SelectedIndex].configFolder) && Directory.Exists(games.gamesList[game.SelectedIndex].gameFolder))
            {
                //loads plugins and sets DataContext
                loadPlugins();
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
        void loadPlugins()
        {
            DataContext = null;
            warningLabel.Visibility = Visibility.Hidden;
            progressLabel.Visibility = Visibility.Visible;
            loadingBar.Visibility = Visibility.Visible;
            progressLabel.Content = "Loading " + games.gamesList[game.SelectedIndex].name + " plugins...";
            game.IsEnabled = false;
            gameFolderButton.IsEnabled = false;
            pluginFolderButton.IsEnabled = false;
            editMasters.IsEnabled = false;
            conflictCheckBox.IsEnabled = false;
            backgroundWorker.RunWorkerAsync();
        }



        //WHEN LOAD ORDER CHANGED
        private void pluginsBox_Drop(object sender, System.Windows.DragEventArgs e){ games.gamesList[game.SelectedIndex].wasChanged = true; }// when order is changed

        private void CheckBox_Checked(object sender, RoutedEventArgs e){ games.gamesList[game.SelectedIndex].wasChanged = true; }// when plugin is enabled

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e){ games.gamesList[game.SelectedIndex].wasChanged = true; } // when plugin is disabled



        //WHEN CHECK FOR CONFLICT"S CHECKBOX STATE CHANGES
        private void conflictCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            game.IsEnabled = false;
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
