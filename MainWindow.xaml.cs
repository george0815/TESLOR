using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
        BackgroundWorker backgroundWorker = new BackgroundWorker();
        BackgroundWorker backgroundWorkerConflict = new BackgroundWorker();

        int index; 





        public MainWindow() 
        {
            InitializeComponent();
            index = game.SelectedIndex;
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.DoWork += loadPluginsBackground;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_OnCompleted;

            backgroundWorkerConflict.WorkerReportsProgress = true;
            backgroundWorkerConflict.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorkerConflict.DoWork += conflictChecksBackground;
            backgroundWorkerConflict.RunWorkerCompleted += backgroundWorker_OnCompletedConflict;



        }

        public void loadPluginsBackground(object sender, DoWorkEventArgs e){ games.gamesList[index].loadPlugins(backgroundWorker);}

        public void conflictChecksBackground(object sender, DoWorkEventArgs e){ games.gamesList[index].overlapCheck(backgroundWorkerConflict);}

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) { loadingBar.Value = e.ProgressPercentage; }





        void backgroundWorker_OnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DataContext = games.gamesList[game.SelectedIndex];
            loadingBar.Value = 0;
            progressLabel.Visibility = Visibility.Hidden;
            loadingBar.Visibility = Visibility.Hidden;


            if (games.gamesList[game.SelectedIndex].conflictCheck == true) {
                progressLabel.Content = "Checking for conflicts...";
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;

                backgroundWorkerConflict.RunWorkerAsync();
            }

        }

        void backgroundWorker_OnCompletedConflict(object sender, RunWorkerCompletedEventArgs e)
        {
           
            loadingBar.Value = 0;
            progressLabel.Visibility = Visibility.Hidden;
            loadingBar.Visibility = Visibility.Hidden;
            DataContext = null;
            DataContext = games.gamesList[game.SelectedIndex];

        }






        //WHEN COMBOBOX INDEX CHANGES
        public void game_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            
            
           
            if (games.gamesList[game.SelectedIndex].loadOrder == null) {

                //loads plugins and sets DataContext
                DataContext = null;
                index = game.SelectedIndex;
                progressLabel.Visibility = Visibility.Visible;
                loadingBar.Visibility = Visibility.Visible;
                progressLabel.Content = "Loading " + games.gamesList[game.SelectedIndex].name + " plugins...";
                backgroundWorker.RunWorkerAsync();

            }
            else
            {
                DataContext = games.gamesList[game.SelectedIndex];
            }

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
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show("Error saving config file, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
            }

        }



        //GAME FOLDER BUTTON
        private void gameFolderButton_Click(object sender, RoutedEventArgs e)
        {

            //creates new instance of FolderBrowserDialog and sets initial directory
            FolderBrowserDialog openFolderDialog = new FolderBrowserDialog();
            openFolderDialog.InitialDirectory = "c:\\";
           

            // Show open file dialog box
            DialogResult result = openFolderDialog.ShowDialog();

            // Process open file dialog box results
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                //Get the path of specified file
                gameFolderBox.Text = openFolderDialog.SelectedPath;
                games.gamesList[game.SelectedIndex].gameFolder = openFolderDialog.SelectedPath;

            }



        }


        //ON WINDOW CLOSE
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.IO.File.Delete("cfg.json");
            saveConfig();
        }


        //PLUGIN CONFIG FOLDER BUTTTON
        private void pluginFolderButton_Click(object sender, RoutedEventArgs e)
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
                //Get the path of specified file
                pluginsTextBox.Text = dialog.FileName;
                games.gamesList[game.SelectedIndex].configFolder = dialog.FileName;
            }
        }


        

        //EDIT MASTERS CHECKBOX
        private void editMasters_Checked(object sender, RoutedEventArgs e){ DataContext = games.gamesList[game.SelectedIndex]; }
        private void editMasters_Unchecked(object sender, RoutedEventArgs e){ DataContext = games.gamesList[game.SelectedIndex]; }

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
                    fs.Close();
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Error reading config file, creating new one...", "Error");
                    if (fs != null) { fs.Close(); }
                    System.IO.File.Delete("cfg.json");
                    games = new Games();
                    saveConfig();
                }
            }
            //if config doesn't exist, create one
            else
            {
                games = new Games();
                saveConfig();
            }

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
                        using (key) { if (!string.IsNullOrWhiteSpace(path)) { game.gameFolder = path; } }

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
                    }
                    else if ((game.name != "The Elder Scrolls III: Morrowind" && System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.defaultConfigFolder)))
                    {
                        game.configFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + game.defaultConfigFolder;
                    }

                }

                #endregion

            }

            #endregion



            

            //loads plugins and sets DataContext
            progressLabel.Content = "Loading " + games.gamesList[game.SelectedIndex].name + " plugins...";
            
            backgroundWorker.RunWorkerAsync();

            






        }
    }


}
