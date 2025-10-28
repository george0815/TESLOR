using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace SimpleLoadOrderOrganizer
{
    //declares rust FFI functions used to parse plugins
    internal static partial class Native
    {
        private const string DllName = "esplugin";

        // GetPluginInfo: returns an allocated C string we must free manually
        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        public static partial IntPtr GetPluginInfo(string path, int game);

        // FreeString: frees memory allocated by Rust
        [LibraryImport(DllName)]
        public static partial void FreeString(IntPtr ptr);

        // DoesOverlap: returns bool
        [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static partial bool DoesOverlap(int game, string pluginOne, string pluginTwo);
    }


    internal class PluginHandle : SafeHandle
    {
        // Default constructor for ownership
        public PluginHandle() : base(IntPtr.Zero, true) { }

        // New constructor to wrap an existing pointer
        public PluginHandle(IntPtr handle) : base(IntPtr.Zero, true)
        {
            SetHandle(handle); // protected call is allowed inside derived class
        }

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
            if (!this.IsInvalid) { Native.FreeString(handle); }

            return true;
        }
    }


    [DataContract]
    public class Plugin : IDisposable
    {

        [DataMember(Name = "overriderecords")]
        public int OverrideRecords { get; set; }


        [DataMember(Name = "ismaster")]
        public bool IsMaster { get; set; }


        [DataMember(Name = "islightmaster")]
        public bool IsLight { get; set; }


        [DataMember(Name = "masters")]
        public List<string>? Masters { get; set; }


        [DataMember(Name = "filename")]
        public string? PluginFilename { get; set; }

        public string? FilePath { get; set; }


        public bool IsActive { get; set; }

        private readonly PluginHandle PluginJson;


        public string? MastersString { get; set; }

        public DateTime DateModified { get; set; }
            

        public string? Conflicts { get; set; }
        

        public bool invalid = false;

        public Plugin(string path, Int32 game) {


            PluginJson = new PluginHandle(Native.GetPluginInfo(path, game));


            using var memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(PluginJson.AsString()));
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Plugin));

                var obj = serializer.ReadObject(memoryStream);
                if (obj is Plugin temp &&
                    temp.Masters != null &&
                    temp.PluginFilename != null){
                    this.Masters = temp.Masters;
                    this.IsMaster = temp.IsMaster;
                    this.IsLight = temp.IsLight;
                    this.OverrideRecords = temp.OverrideRecords;
                    this.PluginFilename = temp.PluginFilename;
                }
                else{
                    throw new InvalidOperationException("Deserialized Plugin is missing required properties.");
                }


            }
            catch (Exception ex) {

                if (ex is InvalidOperationException) {
                    this.invalid = true;
                }
                else { MessageBox.Show(ex.Message); }
                    
            
            
            }

        }

        

        public void Dispose() { PluginJson.Dispose(); GC.SuppressFinalize(this); }


      


    }
}
