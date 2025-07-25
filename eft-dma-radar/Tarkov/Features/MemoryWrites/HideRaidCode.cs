using eft_dma_shared.Common.Features;

using eft_dma_shared.Common.Unity.LowLevel.Hooks;
using eft_dma_shared.Common.DMA.ScatterAPI;
using eft_dma_shared.Common.Unity;
using eft_dma_radar;
using eft_dma_radar.Tarkov.Features;
using eft_dma_shared.Common.Misc;

namespace eft_dma_shared.Common.Features.MemoryWrites
{
    public sealed class HideRaidCode : MemPatchFeature<HideRaidCode>
    {
        private bool _set;

        public override bool Enabled
        {
            get => MemWrites.Config.HideRaidCode;
            set => MemWrites.Config.HideRaidCode = value;
        }

        public override bool TryApply()
        {
            try
            {
                ulong alphaLabel = NativeMethods.FindGameObjectS("Preloader UI/Preloader UI/BottomPanel/Content/UpperPart/AlphaLabel");
                if (alphaLabel == 0x0)
                {
                    "HideRaidCode: Could not find AlphaLabel GameObject".printf();
                    return false;
                }

                if (Enabled)
                {
                    if (!_set)
                    {
                        NativeMethods.GameObjectSetActive(alphaLabel, false);
                        "HideRaidCode [ON]".printf();
                        _set = true;
                    }
                }
                else if (_set)
                {
                    NativeMethods.GameObjectSetActive(alphaLabel, true);
                    "HideRaidCode [OFF]".printf();
                    _set = false;
                }
            }
            catch (Exception ex)
            {
                $"ERROR configuring HideRaidCode: {ex}".printf();
                return false;
            }
            return true;
        }

        public override void OnRaidStart()
        {
            _set = default;
        }

        public override void OnGameStop()
        {
            _set = false;
        }
    }
}
