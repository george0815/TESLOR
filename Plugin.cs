using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleLoadOrderOrganizer
{
    //declares rust FFI functions used to parse plugins
    internal class Native
    {
        [DllImport("esplugin.dll")]
        internal static extern pluginHandle getPluginInfo(string path, Int32 game);

        [DllImport("esplugin.dll")]
        internal static extern bool doesOverlap(Int32 game, string pluginOne, string pluginTwo);

        [DllImport("esplugin.dll")]
        internal static extern void freeString(IntPtr path);
    }


    internal class pluginHandle : SafeHandle
    {
        public pluginHandle() : base(IntPtr.Zero, true) { }

        public override bool IsInvalid { get { return this.handle == IntPtr.Zero; } }

        public string AsString()
        {
            int len = 0;
            while (Marshal.ReadByte(handle, len) != 0) { ++len; }
            byte[] buffer = new byte[len];
            Marshal.Copy(handle, buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        protected override bool ReleaseHandle()
        {
            if (!this.IsInvalid) { Native.freeString(handle); }

            return true;
        }
    }


    [DataContract]
    public class Plugin : IDisposable
    {

        [DataMember(Name = "overriderecords")]
        public int overrideRecords { get; set; }


        [DataMember(Name = "ismaster")]
        public bool isMaster { get; set; }


        [DataMember(Name = "islightmaster")]
        public bool isLight { get; set; }


        [DataMember(Name = "masters")]
        public List<string> masters { get; set; }


        [DataMember(Name = "filename")]
        public string pluginFilename { get; set; }

        public string filePath { get; set; }


        public bool isActive { get; set; }

        private pluginHandle pluginJson;


        public string mastersString { get; set; }

        public DateTime dateModified { get; set; }
            

        public string conflicts { get; set; }
        

        public Plugin(string path, Int32 game) { pluginJson = Native.getPluginInfo(path, game); deserialize(); }

        public void deserialize()
        {


            using (var memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(pluginJson.AsString())))
            {
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(Plugin));
                    Plugin temp = (Plugin)serializer.ReadObject(memoryStream);
                    this.masters = temp.masters;
                    this.isMaster = temp.isMaster;
                    this.isLight = temp.isLight;
                    this.overrideRecords = temp.overrideRecords;
                    this.pluginFilename = temp.pluginFilename;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }


        }

        public void Dispose() { pluginJson.Dispose(); }


      


    }
}
