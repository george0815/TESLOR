using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;


namespace SimpleLoadOrderOrganizer
{

    [DataContract]
    internal partial class Game
    {
        [DataMember(Name = "Game Folder")]
        public string? GameFolder { get; set; }

        [DataMember(Name = "Config Folder")]
        public string? ConfigFolder { get; set; }

        [DataMember(Name = "Default Config Folder")]
        public string? DefaultConfigFolder { get; set; }

        [DataMember(Name = "Name")]
        public string? Name { get; set; }

        [DataMember(Name = "Registry Key")]
        public string? RegKey { get; set; }

        [DataMember(Name = "ID")]
        public int Id { get; set; }

        [DataMember(Name = "Load Plugins")]
        public bool LoadOnStart { get; set; }


        private bool _conflictCheck;
        [DataMember(Name = "Check for Conflcits")]
        public bool ConflictCheck
        {
            get => _conflictCheck;
            set => SetField(ref _conflictCheck, value);
        }

        private bool _editMaster;
        [DataMember(Name = "Edit Master")]
        public bool EditMaster
        {
            get => _editMaster;
            set => SetField(ref _editMaster, value);
        }

        // INotifyPropertyChanged 
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            this.WasChanged = true;
            return true;
        }

        public ObservableCollection<Plugin> LoadOrder { get; set; } = [];

        [DataMember(Name = "Mandatory Files")]
        public List<string> MandatoryFiles { get; set; } = [];// these are files that will always be loaded regardless of whether they are checked in the launcher/in the config

        [DefaultValue(false)]
        public bool WasChanged { get; set; }


        //LOADS PLUGINS
        public void LoadPlugins(BackgroundWorker? bw = null)
        {

            #region LOAD ALL PLUGINS FROM DATA FOLDER
            this.LoadOrder = [];

            string directory = ""; //hold's plugins directory for a given game


            //Morrowind uses "Data Files" as it's folder
            if (this.Name == "The Elder Scrolls III: Morrowind") { directory = this.GameFolder + "\\Data Files"; }
            //for every other game, just use the Data folder
            else { directory = this.GameFolder + "Data"; }





            DirectoryInfo info = new(directory);
            //gets all plugin files
            var filesList = from fullFilename in info.GetFiles().OrderBy(s => s.LastWriteTime).Where(s => s.Name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                            select fullFilename;



            //reads config file

            IEnumerable<string> lines = [];
            if (!string.IsNullOrEmpty(this.ConfigFolder))
            {
                lines = File.ReadLines(this.ConfigFolder);
            }
            Plugin tempPlugin;

            //add a checkbox for each plugin
            foreach (FileInfo file in filesList)
            {

                //creates plugin
                tempPlugin = new Plugin(file.FullName, this.Id);

                //if invalid plugin, skip
                if (tempPlugin.invalid == true) { continue; }

                tempPlugin.MastersString = String.Join("\n", [.. tempPlugin.Masters!]);
                tempPlugin.DateModified = file.LastWriteTime;
                tempPlugin.FilePath = file.FullName;

                try
                {
                    //checks all files that are required for loading
                    if (this.MandatoryFiles.Contains(file.Name) || CreationClubCheck1().IsMatch(file.Name) || CreationClubCheck2().IsMatch(file.Name)) { tempPlugin.IsActive = true; }

                    //Morrowind use's Morrowind.ini instead of plugins.txt so we have to acocunt for that
                    if (this.Id == 0)
                    {
                        string tempLine = "";
                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (line.Contains("GameFile")) { tempLine = line[(line.LastIndexOf('=') + 1)..]; }

                            //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                            if (file.Name == tempLine) { tempPlugin.IsActive = true; }

                        }
                    }
                    else if (this.Id == 3 || this.Id == 6)
                    {
                        string tempLine = "";
                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (line[0] == '*') { tempLine = line[(line.IndexOf('*') + 1)..]; }

                            //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                            if (file.Name == tempLine && line[0] == '*') { tempPlugin.IsActive = true; }

                        }
                    }
                    else
                    {
                        //sets checkbox to checked if plugin is active (ie: if plugin name was found in plugins.txt/Morrowind.ini)
                        foreach (var line in lines) { if (file.Name == line) { tempPlugin.IsActive = true; } }

                    }
                }
                catch (Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Error opening plugin, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
                }


                App.Current.Dispatcher.Invoke((Action)delegate 
                {
                    this.LoadOrder.Add(tempPlugin);

                });
                bw?.ReportProgress(Convert.ToInt32((double)LoadOrder.Count / filesList.Count() * 100));
                tempPlugin.Dispose();


            }

            //reorders
            if (this.Id == 3 || this.Id == 6 || this.Id == 2)
            {
                //for every line
                foreach (var line in lines)
                {

                    string tempLine = "";
                    foreach (FileInfo file in filesList)
                    {

                        //reinserts plugins into loadorder lists
                        tempLine = line[(line.IndexOf('*') + 1)..];
                        if (((file.Name == tempLine) && line[0] == '*') || (line == file.Name))
                        {


                            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                            {
                                tempPlugin = LoadOrder.FirstOrDefault(x => x.PluginFilename == file.Name)!;
                                if (tempPlugin != null)
                                {
                                    LoadOrder.Remove(tempPlugin);
                                    LoadOrder.Insert(0, tempPlugin);
                                }

                            });

                        }



                    }

                }
            }
            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
            {
                //if skyrim
                if (Id == 2) { this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderBy(s => s.IsMaster).ThenBy(s => s.PluginFilename == "Skyrim.esm").ThenBy(s => s.PluginFilename != "Update.esm").Reverse()); }
                //if sse or fallout 4
                else if (Id == 3)
                {
                    this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderBy(s => s.IsMaster).ThenBy(s => s.PluginFilename == "Skyrim.esm").ThenBy(s => s.PluginFilename == "Update.esm").ThenBy(s => s.PluginFilename == "Dawnguard.esm").ThenBy(s => s.PluginFilename == "HearthFires.esm").ThenBy(s => s.PluginFilename == "Dragonborn.esm").Reverse());
                }
                else if (Id == 6)
                {
                    this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderBy(s => s.IsMaster).ThenBy(s => s.PluginFilename == "Fallout4.esm").ThenBy(s => s.PluginFilename == "DLCRobot.esm").ThenBy(s => s.PluginFilename == "DLCworkshop01.esm").ThenBy(s => s.PluginFilename == "DLCCoast.esm").ThenBy(s => s.PluginFilename == "DLCworkshop02.esm").ThenBy(s => s.PluginFilename == "DLCworkshop03.esm").ThenBy(s => s.PluginFilename == "DLCNukaWorld.esm").Reverse());
                }
                //if fallout 3
                else if (Id == 4) { this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderByDescending(s => s.PluginFilename == "Fallout3.esm").ThenBy(s => s.DateModified).ThenBy(s => s.IsMaster)); }

                //if fallout new vegas
                else if (Id == 5) { this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderByDescending(s => s.PluginFilename == "FalloutNV.esm").ThenBy(s => s.DateModified).ThenBy(s => s.IsMaster)); }

                //if Morrowind
                else if (Id == 0) { this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderByDescending(s => s.PluginFilename == "Morrowind.esm").ThenBy(s => s.DateModified).ThenBy(s => s.IsMaster)); }

                //if Oblivion
                else if (Id == 1) { this.LoadOrder = new ObservableCollection<Plugin>(LoadOrder.OrderByDescending(s => s.PluginFilename == "Oblivion.esm").ThenBy(s => s.DateModified).ThenBy(s => s.IsMaster)); }
            });

            #endregion



        }

        //WRITES PLUGINS

        public void WritePlugins()
        {



            //sets release date
            var dateModified = this.Id switch
            {
                0 => new DateOnly(2002, 5, 1),
                1 => new DateOnly(2006, 3, 20),
                4 => new DateOnly(2008, 10, 28),
                5 => new DateOnly(2010, 10, 19),
                _ => new DateOnly(2011, 11, 11),
            };

            //declared string that will hold config text
            string fileString = "";


            if (this.Id == 0)
            {
                //Opens morrowind config file
                try
                {
                    {
                        //copies every line except game files
                        IEnumerable<string> lines = [];
                        if (!string.IsNullOrEmpty(this.ConfigFolder))
                        {
                            lines = File.ReadLines(this.ConfigFolder);
                        }

                        //for every line
                        foreach (var line in lines)
                        {
                            //gets rid of plugin prefix
                            if (!line.Contains("GameFile") && !line.Contains("[Game Files]")) { fileString += line + Environment.NewLine; }

                        }
                        fileString += "[Game Files]" + Environment.NewLine;
                    }
                }
                catch (Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Error opening Morrowind.ini, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
                }
            }

            //iterates through loadorder
            int counter = 0; //used for morrowind
            foreach (Plugin p in this.LoadOrder)
            {


                if (this.Id == 0 || this.Id == 1 || this.Id == 4 || this.Id == 5)
                {


                    //3. Sets date modified (olde game' loadorder is determined by the plugins last write time)
                    File.SetLastWriteTime(p.FilePath!, dateModified.ToDateTime(TimeOnly.MinValue));
                    dateModified = dateModified.AddDays(1);



                    //4. write name if active, nothing inactive (handle morrowind)
                    if (p.IsActive && this.Id != 0) { fileString += p.PluginFilename + Environment.NewLine; }
                    else if (p.IsActive && this.Id == 0)
                    {
                        fileString += "GameFile" + counter + "=" + p.PluginFilename + Environment.NewLine;
                        counter++;
                    }

                }
                else if (this.Id == 3 || this.Id == 6)
                {

                    //3. write down * then name if active, just name if inactive
                    if (p.IsActive) { fileString += "*" + p.PluginFilename + Environment.NewLine; }
                    else { fileString += p.PluginFilename + Environment.NewLine; }

                }
                else
                {

                    //3. write name if active, nothing inactive
                    if (p.IsActive) { fileString += p.PluginFilename + Environment.NewLine; }

                }


            }


            //5. Saves loadorder
            try { File.WriteAllText(this.ConfigFolder!, fileString); }
            catch (Exception)
            {
                System.Windows.Forms.MessageBox.Show("Error saving plugins.txt or Morrowind.ini, this may be because the file is open in another program, or that his program wasn't ran as administrator...", "Error");
            }

        }

        //CHECKS FOR MOD CONFLICTS
        public void OverlapCheck(BackgroundWorker? bw = null)
        {
            List<string> checkedPlugins = [];
            int counter = 0;

            foreach (Plugin p1 in this.LoadOrder)
            {
                

                foreach (Plugin p2 in this.LoadOrder)
                {
                    if (p1.PluginFilename != p2.PluginFilename && !checkedPlugins.Contains(p2.PluginFilename!) &&
                        (p2.PluginFilename != "Skyrim.esm" && p2.PluginFilename != "Update.esm" && p1.PluginFilename != "Skyrim.esm" && p1.PluginFilename != "Update.esm") &&
                        (p2.PluginFilename != "Morrowind.esm" && p1.PluginFilename != "Morrowind.esm") &&
                        (p2.PluginFilename != "Oblivion.esm" && p1.PluginFilename != "Oblivion.esm") &&
                        (p2.PluginFilename != "FalloutNV.esm" && p1.PluginFilename != "FalloutNV.esm") &&
                        (p2.PluginFilename != "Fallout3.esm" && p1.PluginFilename != "Fallout3.esm") &&
                        (p2.PluginFilename != "Fallout4.esm" && p1.PluginFilename != "Fallout4.esm")
                        )
                    {
                        if (Native.DoesOverlap(Id, p1.FilePath!, p2.FilePath!))
                        {



                            App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
                            {
                                if (p1.Conflicts != null) { p1.Conflicts += "\n"; }
                                if (p2.Conflicts != null) { p2.Conflicts += "\n"; }


                                p1.Conflicts += p2.PluginFilename;
                                p2.Conflicts += p1.PluginFilename;



                            });

                        }

                    }
                    counter++;
                    bw?.ReportProgress(Convert.ToInt32((double)counter / (LoadOrder.Count * LoadOrder.Count) * 100));
                }

                checkedPlugins.Add(p1.PluginFilename!);

            }
        }

        //regexes for checking for anniversary edition plugins

        [GeneratedRegex(@"^cc[a-zA-Z]{6}\d{3}")]
        private static partial Regex CreationClubCheck1();
        [GeneratedRegex(@"^cc[a-zA-Z]{5}\d{4}")]
        private static partial Regex CreationClubCheck2();
    }
    [DataContract]
    internal class Games
    {
        [DataMember(Name = "Games")]
        public ObservableCollection<Game> gamesList = [];

        [DataMember(Name = "Last Active Game")]
        public int GameID { get; set; }

        public Games()
        {
            gamesList.Add(new Game { Name = "The Elder Scrolls III: Morrowind", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Morrowind", DefaultConfigFolder = "\\Morrowind.ini", Id = 0, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Morrowind.esm"] });
            gamesList.Add(new Game { Name = "The Elder Scrolls IV: Oblivion", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Oblivion", DefaultConfigFolder = "\\AppData\\Local\\Oblivion\\Plugins.txt", Id = 1, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Oblivion.esm"] });
            gamesList.Add(new Game { Name = "The Elder Scrolls V: Skyrim", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Skyrim", DefaultConfigFolder = "\\AppData\\Local\\Skyrim\\plugins.txt", Id = 2, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Skyrim.esm", "Update.esm"] });
            gamesList.Add(new Game { Name = "The Elder Scrolls V: Skyrim – Special Edition", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Skyrim Special Edition", DefaultConfigFolder = "\\AppData\\Local\\Skyrim Special Edition\\Plugins.txt", Id = 3, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Skyrim.esm", "Update.esm", "Dawnguard.esm", "Dragonborn.esm", "HearthFires.esm"] });
            gamesList.Add(new Game { Name = "Fallout 3", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Fallout3", DefaultConfigFolder = "\\AppData\\Local\\Fallout3\\plugins.txt", Id = 4, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Fallout3.esm"] });
            gamesList.Add(new Game { Name = "Fallout: New Vegas", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\falloutnv", DefaultConfigFolder = "\\AppData\\Local\\FalloutNV\\plugins.txt", Id = 5, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["FalloutNV.esm"] });
            gamesList.Add(new Game { Name = "Fallout 4", ConfigFolder = "", GameFolder = "", RegKey = "SOFTWARE\\WOW6432Node\\Bethesda Softworks\\Fallout4", DefaultConfigFolder = "\\AppData\\Local\\Fallout4\\Plugins.txt", Id = 6, EditMaster = false, ConflictCheck = false, MandatoryFiles = ["Fallout4.esm", "DLCRobot.esm", "DLCworkshop01.esm", "DLCCoast.esm", "DLCworkshop02.esm", "DLCworkshop03.esm", "DLCNukaWorld.esm"] });
            GameID = 3;

        }
    }
}