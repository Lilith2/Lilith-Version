global using SkiaSharp;
global using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
global using SkiaSharp.Views.Desktop;
global using System.ComponentModel;
global using System.Data;
global using System.Reflection;
global using System.Diagnostics;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Numerics;
global using System.Collections.Concurrent;
global using System.Net;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Collections;
global using System.Net.Http.Headers;
global using System.Buffers;
global using SDK;
global using eft_dma_shared.Common;
using System.Runtime.Versioning;
using eft_dma_radar;
using eft_dma_radar.UI.Misc;
using eft_dma_radar.UI.Radar;
using eft_dma_radar.Tarkov;
using eft_dma_shared.Common.Features;
using eft_dma_radar.UI.ESP;
using eft_dma_shared.Common.Maps;
using eft_dma_radar.Tarkov.Features;
using eft_dma_radar.Tarkov.Features.MemoryWrites.Patches;
using eft_dma_shared.Common.Misc.Data;
using eft_dma_shared.Common.UI;

[assembly: AssemblyTitle(Program.Name)]
[assembly: AssemblyProduct(Program.Name)]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: SupportedOSPlatform("Windows")]

namespace eft_dma_radar
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        internal const string Name = "EFT DMA Radar";


        /// <summary>
        /// Global Program Configuration.
        /// </summary>
        public static Config Config { get; }

        /// <summary>
        /// Path to the Configuration Folder in %AppData%
        /// </summary>
        public static DirectoryInfo ConfigPath { get; } =
            new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "eft-dma-radar"));

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[Obfuscation(Feature = "Virtualization", Exclude = false)]
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                EnsureConsole();
                ConfigureProgram();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        #region Private Members

        /// <summary>
        /// Ensures a console window is attached for logging.
        /// </summary>
        private static void EnsureConsole()
        {
            // Only allocate a console if one does not already exist
            if (!ConsoleAttached())
            {
                AllocConsole();
            }
        }

        /// <summary>
        /// Checks if a console window is already attached.
        /// </summary>
        private static bool ConsoleAttached()
        {
            try
            {
                int windowHeight = Console.WindowHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static Program()
        {
            try
            {
                try
                {
                    string loneCfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lones-Client");
                    if (Directory.Exists(loneCfgPath))
                    {
                        if (ConfigPath.Exists)
                            ConfigPath.Delete(true);
                        Directory.Move(loneCfgPath, ConfigPath.FullName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("ERROR Importing Lone Config(s). Close down the radar, and try copy your config files manually from %AppData%\\LonesClient TO %AppData%\\eft-dma-radar\n\n" +
                        "Be sure to delete the Lones-Client folder when done.\n\n" +
                        $"ERROR: {ex}",
                        Program.Name,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                ConfigPath.Create();
                var config = Config.Load();
                eft_dma_shared.SharedProgram.Initialize(ConfigPath, config);
                Config = config;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Configure Program Startup.
        /// </summary>
        //[Obfuscation(Feature = "Virtualization", Exclude = false)]
        private static void ConfigureProgram()
        {
            ApplicationConfiguration.Initialize();
            using var loading = LoadingForm.Create();
            loading.UpdateStatus("Loading Tarkov.Dev Data...", 15);
            EftDataManager.ModuleInitAsync(loading).GetAwaiter().GetResult();
            loading.UpdateStatus("Loading Map Assets...", 35);
            LoneMapManager.ModuleInit();
            loading.UpdateStatus("Starting DMA Connection...", 50);
            MemoryInterface.ModuleInit();
            loading.UpdateStatus("Loading Remaining Modules...", 75);
            FeatureManager.ModuleInit();
            ResourceJanitor.ModuleInit(new Action(CleanupWindowResources));
            RuntimeHelpers.RunClassConstructor(typeof(MemPatchFeature<FixWildSpawnType>).TypeHandle);
            loading.UpdateStatus("Loading Completed!", 100);
        }

        private static void CleanupWindowResources()
        {
            MainForm.Window?.PurgeSKResources();
            EspForm.Window?.PurgeSKResources();
        }

        #endregion
    }
}