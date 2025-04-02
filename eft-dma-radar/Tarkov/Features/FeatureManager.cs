using eft_dma_radar.Tarkov.Features.MemoryWrites;
using eft_dma_radar.Tarkov.GameWorld;
using eft_dma_radar.UI.Misc;
using eft_dma_shared.Common.DMA;
using eft_dma_shared.Common.DMA.ScatterAPI;
using eft_dma_shared.Common.Features;

using eft_dma_shared.Common.Unity.LowLevel;
using eft_dma_shared.Common.Unity.LowLevel.Hooks;
using eft_dma_shared.Common.Misc;

namespace eft_dma_radar.Tarkov.Features
{
    /// <summary>
    /// Feature Manager Thread.
    /// </summary>
    internal static class FeatureManager
    {
        internal static void ModuleInit()
        {
            new Thread(Worker)
            {
                IsBackground = true
            }.Start();
        }

        static FeatureManager()
        {
            MemDMABase.GameStarted += Memory_GameStarted;
            MemDMABase.GameStopped += Memory_GameStopped;
            MemDMABase.RaidStarted += Memory_RaidStarted;
            MemDMABase.RaidStopped += Memory_RaidStopped;
        }

        private static void Worker()
        {
            "Features Thread Starting...".printf();
            while (true)
            {
                try
                {
                    if (MemDMABase.WaitForProcess() && MemWrites.Enabled && Memory.Ready)
                    {
                        while (MemWrites.Enabled && Memory.Ready)
                        {
                            if (MemWrites.Config.AdvancedMemWrites && !NativeHook.Initialized)
                            {
                                NativeHook.Initialize();
                            }
                            var memWrites = IFeature.AllFeatures
                                .OfType<IMemWriteFeature>()
                                .Where(feature => feature.CanRun);
                            if (memWrites.Any())
                            {
                                ExecuteMemWrites(memWrites);
                            }
                            var patches = IFeature.AllFeatures
                                .OfType<IMemPatchFeature>()
                                .Where(feature => feature.CanRun);
                            if (patches.Any())
                            {
                                ExecuteMemPatches(patches);
                            }
                            Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    $"[Features Thread] CRITICAL ERROR: {ex}".printf();
                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Executes MemWrite Features.
        /// </summary>
        private static void ExecuteMemWrites(IEnumerable<IMemWriteFeature> memWrites)
        {
            try
            {
                using var hScatter = new ScatterWriteHandle();
                foreach (var feature in memWrites)
                {
                    feature.TryApply(hScatter);
                    feature.OnApply();
                }
                if (Memory.Game is LocalGameWorld game)
                {
                    hScatter.Execute(DoWrite);
                    bool DoWrite() =>
                        MemWrites.Enabled && game.IsSafeToWriteMem;
                }
            }
            catch (Exception ex)
            {
                $"MemWrites [FAIL] {ex}".printf();
            }
        }

        /// <summary>
        /// Executes MemWrite Features.
        /// </summary>
        private static void ExecuteMemPatches(IEnumerable<IMemPatchFeature> patches)
        {
            try
            {
                foreach (var feature in patches)
                {
                    feature.TryApply();
                    feature.OnApply();
                }
            }
            catch (Exception ex)
            {
                $"MemPatches [FAIL] {ex}".printf();
            }
        }

        private static void Memory_GameStarted(object sender, EventArgs e)
        {
            foreach (var feature in IFeature.AllFeatures)
            {
                feature.OnGameStart();
            }
        }

        private static void Memory_GameStopped(object sender, EventArgs e)
        {
            foreach (var feature in IFeature.AllFeatures)
            {
                feature.OnGameStop();
            }
        }

        private static void Memory_RaidStarted(object sender, EventArgs e)
        {
            foreach (var feature in IFeature.AllFeatures)
            {
                feature.OnRaidStart();
            }
        }

        private static void Memory_RaidStopped(object sender, EventArgs e)
        {
            foreach (var feature in IFeature.AllFeatures)
            {
                feature.OnRaidEnd();
            }
        }
    }

    /// <summary>
    /// Helper Class.
    /// </summary>
    internal static class MemWrites
    {
        /// <summary>
        /// DMAToolkit/MemWrites Config.
        /// </summary>
        public static MemWritesConfig Config { get; } = Program.Config.MemWrites;

        /// <summary>
        /// True if Memory Writes are enabled, otherwise False.
        /// </summary>
        public static bool Enabled
        {
            get => Config.MemWritesEnabled;
            set => Config.MemWritesEnabled = value;
        }
    }
}