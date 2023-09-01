using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace SimpleLoadOrderOrganizer
{

    [DataContract]
    internal class Game
    {
        [DataMember(Name = "Game Folder")]
        public string gameFolder { get; set; }
        [DataMember(Name = "Config Folder")]
        public string configFolder { get; set; }

        [DataMember(Name = "Default Config Folder")]
        public string defaultConfigFolder { get; set; }

        [DataMember(Name = "Name")]
        public string name { get; set; }

        [DataMember(Name = "Registry Key")]
        public string regKey { get; set; }

        [DataMember(Name = "ID")]
        public int id { get; set; }

        [DataMember(Name = "Edit Master")]
        public bool editMaster { get; set; }

        [DataMember(Name = "Load Plugins")]
        public bool loadOnStart { get; set; }

        [DataMember(Name = "Check for Conflcits")]
        public bool conflictCheck { get; set; }

        public ObservableCollection<Plugin> loadOrder { get; set; }

        [DataMember(Name = "Mandatory Files")]
        public List<string> mandatoryFiles { get; set; } // these are files that will always be loaded regardless of whether they are checked in the launcher/in the config

        [DefaultValue(false)]
        public bool wasChanged {get; set;}





        //LOADS PLUGINS
        public void loadPlugins(BackgroundWorker bw = null)
        {

            #region LOAD ALL PLUGINS FROM DATA FOLDER
            this.loadOrder = new ObservableCollection<Plugin>();

            string directory = ""; //hold's plugins directory for a given game


            //Morrowind uses "Data Files" as it's folder
            if (this.name == "The Elder Scrolls III: Morrowind") { directory = this.gameFolder + "\\Data Files"; }
            //for every other game, just use the Data folder
            else { directory = this.gameFolder + "Data"; }





            DirectoryInfo info = new DirectoryInfo(directory);
            //gets all plugin files
            var filesList = from fullFilename in info.GetFiles().OrderBy(s => s.LastWriteTime).Where(s => s.Name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                            select fullFilename;



            //reads config file
            var lines = System.IO.File.ReadLines(this.configFolder);
            Plugin tempPlugin;

            //add a checkbox for each plugin
            foreach (FileInfo file in filesList)
            {

                //creates plugin
                tempPlugin = new Plugin(file.FullName, this.id);
                tempPlugin.mastersString = String.Join("\n", tempPlugin.masters.ToArray());
                tempPlugin.dateModified = file.LastWriteTime;
                tempPlugin.filePath = file.FullName;

                try
                {
                    //checks all files that are required for loading
                    if (this.mandatoryFiles.Contains(file.Name) || Regex.IsMatch(file.Name, @"^cc[a-zA-Z]{6}\d{3}") || Regex.IsMatch(file.Name, @"^cc[a-zA-Z]{5}\d{4}")) { tempPlugin.isActive = true; }

                    //Morrowind use's Morrowind.ini instead of plugins.txt so we have to acocunt for that
                    if (this.id == 0)
                    {
                        string tempLine = "";
                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (line.Contains("GameFile")) { tempLine = line.Substring(line.LastIndexOf('=') + 1); }

                            //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                            if (file.Name == tempLine ) { tempPlugin.isActive = true; }

                        }
                    }
                    else if (this.id == 3 || this.id == 6)
                    {
                        string tempLine = "";
                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (line[0] == '*') { tempLine = line.Substring(line.IndexOf('*') + 1); }

                            //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                            if (file.Name == tempLine && line[0] == '*') { tempPlugin.isActive = true; }

                        }
                    }
                    else
                    {
                        //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                        foreach (var line in lines){if (file.Name == line) { tempPlugin.isActive = true; }}

                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Error opening plugin, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
                }


                App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                {
                    this.loadOrder.Add(tempPlugin);
                    
                });
                if (bw != null) { bw.ReportProgress(Convert.ToInt32((double)loadOrder.Count / filesList.Count() * 100)); }
                tempPlugin.Dispose();


            }

            //reorders
            if (this.id == 3 || this.id == 6 || this.id == 2)
            {
                //for every line
                foreach (var line in lines)
                {

                    string tempLine = "";
                    foreach (FileInfo file in filesList)
                    {

                        //reinserts plugins into loadorder lists
                        tempLine = line.Substring(line.IndexOf('*') + 1);
                        if (((file.Name == tempLine) && line[0] == '*') || (line == file.Name)){


                            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                            {
                                tempPlugin = loadOrder.Where(X => X.pluginFilename == file.Name).FirstOrDefault();

                           
                                loadOrder.Remove(loadOrder.Where(X => X.pluginFilename == file.Name).FirstOrDefault());
                                loadOrder.Insert(0, tempPlugin);

                            });
                            
                        }
                     
                       

                    }

                }
            }
            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
            {
                //if skyrim
                if (id == 2) { this.loadOrder = new ObservableCollection<Plugin>(loadOrder.OrderBy(s => s.isMaster).ThenBy(s => s.pluginFilename == "Skyrim.esm").ThenBy(s => s.pluginFilename != "Update.esm").Reverse()); }
                //if sse or fallout 4
                else if (id == 3)
                {
                    this.loadOrder = new ObservableCollection<Plugin>(loadOrder.OrderBy(s => s.isMaster).ThenBy(s => s.pluginFilename == "Skyrim.esm").ThenBy(s => s.pluginFilename == "Update.esm").ThenBy(s => s.pluginFilename == "Dawnguard.esm").ThenBy(s => s.pluginFilename == "HearthFires.esm").ThenBy(s => s.pluginFilename == "Dragonborn.esm").Reverse());
                }
                else if (id == 6) 
                {
                    this.loadOrder = new ObservableCollection<Plugin>(loadOrder.OrderBy(s => s.isMaster).ThenBy(s => s.pluginFilename == "Fallout4.esm").ThenBy(s => s.pluginFilename == "DLCRobot.esm").ThenBy(s => s.pluginFilename == "DLCworkshop01.esm").ThenBy(s => s.pluginFilename == "DLCCoast.esm").ThenBy(s => s.pluginFilename == "DLCworkshop02.esm").ThenBy(s => s.pluginFilename == "DLCworkshop03.esm").ThenBy(s => s.pluginFilename == "DLCNukaWorld.esm").Reverse());
                }
                //sort based on date modified (games older than skyrim)
                else { this.loadOrder = new ObservableCollection<Plugin>(loadOrder.OrderByDescending(s => s.isMaster).ThenBy(s => s.dateModified)); }
            });

            #endregion
          

            #region LOAD ACTIVE PLUGINS FROM CONFIG FILE


            #endregion
        }

        //WRITES PLUGINS

        public void writePlugins()
        {



            //sets release date
            DateOnly dateModified;
            switch (this.id)
            {
                case 0:
                    dateModified = new DateOnly(2002, 5, 1);
                    break;
                case 1:
                    dateModified = new DateOnly(2006, 3, 20);
                    break;
                case 4:
                    dateModified = new DateOnly(2008, 10, 28);
                    break;
                case 5:
                    dateModified = new DateOnly(2010, 10, 19);
                    break;
                default:
                    dateModified = new DateOnly(2011, 11, 11);
                    break;
            }

            //declared string that will hold config text
            string fileString = "";


            if (this.id == 0)
            {
                //Opens morrowind config file
                try
                {
                    {
                        //copies every line except game files
                        var lines = System.IO.File.ReadLines(this.configFolder);
                        string tempLine = "";
                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (!line.Contains("GameFile") && !line.Contains("[Game Files]")) { fileString += line + Environment.NewLine; }

                        }
                        fileString += "[Game Files]" + Environment.NewLine;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Error opening Morrowind.ini, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
                }
            }

            //iterates through loadorder
            int counter = 0; //used for morrowind
            foreach (Plugin p in this.loadOrder) {


                if (this.id == 0 || this.id == 1 || this.id == 4 || this.id == 5)
                {


                    //3. Sets date modified (olde game' loadorder is determined by the plugins last write time)
                    File.SetLastWriteTime(p.filePath, dateModified.ToDateTime(TimeOnly.MinValue));
                    dateModified = dateModified.AddDays(1);



                    //4. write name if active, nothing inactive (handle morrowind)
                    if (p.isActive && this.id != 0) { fileString += p.pluginFilename + Environment.NewLine; }
                    else if (p.isActive && this.id == 0) {
                        fileString += "GameFile" + counter + "=" + p.pluginFilename + Environment.NewLine;
                        counter++;   
                    }

                }
                else if (this.id == 3 || this.id == 6)
                {

                    //3. write down * then name if active, just name if inactive
                    if (p.isActive){ fileString += "*" + p.pluginFilename + Environment.NewLine; }
                    else{ fileString += p.pluginFilename + Environment.NewLine; }

                }
                else
                {

                    //3. write name if active, nothing inactive
                    if (p.isActive) { fileString += p.pluginFilename + Environment.NewLine; }

                }


            }


            //5. Saves loadorder
            try {File.WriteAllText(this.configFolder, fileString); }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error saving plugins.txt or Morrowind.ini, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
            }

        }

        //CHECKS FOR MOD CONFLICTS
        public void overlapCheck(BackgroundWorker bw = null)
        {
            List<string> checkedPlugins = new List<string>();
            int counter = 0;

            foreach (Plugin p1 in this.loadOrder)
            {
                foreach (Plugin p2 in this.loadOrder)
                {
                    if (p1.pluginFilename != p2.pluginFilename && !checkedPlugins.Contains(p2.pluginFilename) && 
                        (p2.pluginFilename != "Skyrim.esm" && p2.pluginFilename != "Update.esm" && p1.pluginFilename != "Skyrim.esm" && p1.pluginFilename != "Update.esm") &&
                        (p2.pluginFilename != "Morrowind.esm" && p1.pluginFilename != "Morrowind.esm") &&
                        (p2.pluginFilename != "Oblivion.esm" && p1.pluginFilename != "Oblivion.esm" ) &&
                        (p2.pluginFilename != "FalloutNV.esm" && p1.pluginFilename != "FalloutNV.esm") &&
                        (p2.pluginFilename != "Fallout3.esm" && p1.pluginFilename != "Fallout3.esm" ) &&
                        (p2.pluginFilename != "Fallout4.esm" && p1.pluginFilename != "Fallout4.esm" ) 
                        ) { 
                        if (Native.doesOverlap(id, p1.filePath, p2.filePath)) {


                       
                            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                            {
                                if (p1.conflicts != null) {p1.conflicts += "\n"; }
                                if (p2.conflicts != null) { p2.conflicts += "\n"; }


                                p1.conflicts += p2.pluginFilename;
                                p2.conflicts += p1.pluginFilename;



                            });

                        }

                    }
                    counter++;
                    if (bw != null) { bw.ReportProgress(Convert.ToInt32((double)counter / (loadOrder.Count * loadOrder.Count) * 100)); }
                }

                checkedPlugins.Add(p1.pluginFilename);

            }
        }



    }
    [DataContract]
    internal class Games
    {
        [DataMember(Name = "Games")]
        public ObservableCollection<Game> gamesList = new ObservableCollection<Game>();

        [DataMember(Name = "Last Active Game")]
        public int gameID { get; set; }

        public Games()
        {
            gamesList.Add(new Game { name = "The Elder Scrolls III: Morrowind", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Morrowind", defaultConfigFolder = "\\Morrowind.ini", id = 0, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Morrowind.esm" } });
            gamesList.Add(new Game { name = "The Elder Scrolls IV: Oblivion", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Oblivion", defaultConfigFolder = "\\AppData\\Local\\Oblivion\\Plugins.txt", id = 1, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Oblivion.esm" } });
            gamesList.Add(new Game { name = "The Elder Scrolls V: Skyrim", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Skyrim", defaultConfigFolder = "\\AppData\\Local\\Skyrim\\plugins.txt", id = 2, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Skyrim.esm", "Update.esm" } });
            gamesList.Add(new Game { name = "The Elder Scrolls V: Skyrim – Special Edition", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Skyrim Special Edition", defaultConfigFolder = "\\AppData\\Local\\Skyrim Special Edition\\Plugins.txt", id = 3, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Skyrim.esm", "Update.esm", "Dawnguard.esm", "Dragonborn.esm", "HearthFires.esm" } });
            gamesList.Add(new Game { name = "Fallout 3", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Fallout3", defaultConfigFolder = "\\AppData\\Local\\Fallout3\\plugins.txt", id = 4, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Fallout3.esm" } });
            gamesList.Add(new Game { name = "Fallout: New Vegas", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\falloutnv", defaultConfigFolder = "\\AppData\\Local\\FalloutNV\\plugins.txt", id = 5, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "FalloutNV.esm" } });
            gamesList.Add(new Game { name = "Fallout 4", configFolder = "", gameFolder = "", regKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Fallout4", defaultConfigFolder = "\\AppData\\Local\\Fallout4\\Plugins.txt", id = 6, editMaster = false, conflictCheck = false, mandatoryFiles = new List<string> { "Fallout4.esm", "DLCRobot.esm", "DLCworkshop01.esm", "DLCCoast.esm", "DLCworkshop02.esm", "DLCworkshop03.esm", "DLCNukaWorld.esm" } });
            gameID = 3;

        }
    }
}
