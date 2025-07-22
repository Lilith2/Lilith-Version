﻿using eft_dma_radar.Tarkov.API;
using eft_dma_radar.Tarkov.EFTPlayer.Plugins;
using eft_dma_radar.Tarkov.Features.MemoryWrites.Patches;
using eft_dma_radar.UI.ESP;
using eft_dma_shared.Common.DMA.ScatterAPI;
using eft_dma_shared.Common.Features;

using eft_dma_shared.Common.Misc.Data;
using eft_dma_shared.Common.Players;
using eft_dma_shared.Common.Unity;
using static SDK.Enums;
using eft_dma_shared.Common.Misc;
using eft_dma_radar.Tarkov.Loot;

namespace eft_dma_radar.Tarkov.EFTPlayer
{
    public class ObservedPlayer : Player
    {
        /// <summary>
        /// Player's Profile & Stats (If Human Player).
        /// </summary>
        public PlayerProfile Profile { get; }
        /// <summary>
        /// ObservedPlayerController for non-clientplayer players.
        /// </summary>
        private ulong ObservedPlayerController { get; }
        /// <summary>
        /// ObservedHealthController for non-clientplayer players.
        /// </summary>
        private ulong ObservedHealthController { get; }
        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name { get; set; }
        /// <summary>
        /// Account UUID for Human Controlled Players.
        /// </summary>
        public override string AccountID { get; }
        /// <summary>
        /// Group that the player belongs to.
        /// </summary>
        public override int GroupID { get; } = -1;
        /// <summary>
        /// Player's Faction.
        /// </summary>
        public override Enums.EPlayerSide PlayerSide { get; }
        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }
        /// <summary>
        /// MovementContext / StateContext
        /// </summary>
        public override ulong MovementContext { get; }
        /// <summary>
        /// EFT.PlayerBody
        /// </summary>
        public override ulong Body { get; }
        /// <summary>
        /// Inventory Controller field address.
        /// </summary>
        public override ulong InventoryControllerAddr { get; }
        /// <summary>
        /// Hands Controller field address.
        /// </summary>
        public override ulong HandsControllerAddr { get; }
        /// <summary>
        /// Corpse field address..
        /// </summary>
        public override ulong CorpseAddr { get; }
        /// <summary>
        /// Player Rotation Field Address (view angles).
        /// </summary>
        public override ulong RotationAddress { get; }
        /// <summary>
        /// Player's Skeleton Bones.
        /// </summary>
        public override Skeleton Skeleton { get; }
        /// <summary>
        /// Player's Current Health Status
        /// </summary>
        public Enums.ETagStatus HealthStatus { get; private set; } = Enums.ETagStatus.Healthy;

        internal ObservedPlayer(ulong playerBase) : base(playerBase)
        {
            var localPlayer = Memory.LocalPlayer;
            ArgumentNullException.ThrowIfNull(localPlayer, nameof(localPlayer));
            ObservedPlayerController = Memory.ReadPtr(this + Offsets.ObservedPlayerView.ObservedPlayerController);
            ArgumentOutOfRangeException.ThrowIfNotEqual(this,
                Memory.ReadValue<ulong>(ObservedPlayerController + Offsets.ObservedPlayerController.Player),
                nameof(ObservedPlayerController));
            ObservedHealthController = Memory.ReadPtr(ObservedPlayerController + Offsets.ObservedPlayerController.HealthController);
            ArgumentOutOfRangeException.ThrowIfNotEqual(this,
                Memory.ReadValue<ulong>(ObservedHealthController + Offsets.ObservedHealthController.Player),
                nameof(ObservedHealthController));
            Body = Memory.ReadPtr(this + Offsets.ObservedPlayerView.PlayerBody);
            InventoryControllerAddr = ObservedPlayerController + Offsets.ObservedPlayerController.InventoryController;
            HandsControllerAddr = ObservedPlayerController + Offsets.ObservedPlayerController.HandsController;
            CorpseAddr = ObservedHealthController + Offsets.ObservedHealthController.PlayerCorpse;

            GroupID = GetGroupID();
            MovementContext = GetMovementContext();
            RotationAddress = ValidateRotationAddr(MovementContext + Offsets.ObservedMovementController.Rotation);
            /// Setup Transforms
            this.Skeleton = new Skeleton(this, GetTransformInternalChain);
            /// Determine Player Type
            PlayerSide = (Enums.EPlayerSide)Memory.ReadValue<int>(this + Offsets.ObservedPlayerView.Side); // Usec,Bear,Scav,etc.
            if (!Enum.IsDefined(PlayerSide)) // Make sure PlayerSide is valid
                throw new Exception("Invalid Player Side/Faction!");

            AccountID = GetAccountID();
            bool isAI = Memory.ReadValue<bool>(this + Offsets.ObservedPlayerView.IsAI);
            IsHuman = !isAI;
            if (IsScav)
            {
                if (isAI)
                {
                    if (MemPatchFeature<FixWildSpawnType>.Instance.IsApplied)
                    {
                        ulong infoContainer = Memory.ReadPtr(ObservedPlayerController + Offsets.ObservedPlayerController.InfoContainer);
                        var wildSpawnType = (Enums.WildSpawnType)Memory.ReadValueEnsure<int>(infoContainer + Offsets.InfoContainer.Side);
                        var role = Player.GetAIRoleInfo(wildSpawnType);
                        Name = role.Name;
                        Type = role.Type;
                    }
                    else // Check voice lines!
                    {
                        var voicePtr = Memory.ReadPtr(this + Offsets.ObservedPlayerView.Voice);
                        string voice = Memory.ReadUnityString(voicePtr);
                        var role = Player.GetAIRoleInfo(voice);
                        Name = role.Name;
                        Type = role.Type;
                    }
                }
                else
                {
                    int pscavNumber = Interlocked.Increment(ref _playerScavNumber);
                    Name = $"PScav{pscavNumber}";
                    Type = GroupID != -1 && GroupID == localPlayer.GroupID ?
                        PlayerType.Teammate : PlayerType.PScav;
                }
            }
            else if (IsPmc)
            {
                Name = "PMC";
                Type = GroupID != -1 && GroupID == localPlayer.GroupID ?
                    PlayerType.Teammate : PlayerType.PMC;
            }
            else
                throw new NotImplementedException(nameof(PlayerSide));
            if (IsHuman)
            {
                Profile = new PlayerProfile(this);
                if (string.IsNullOrEmpty(AccountID) || !ulong.TryParse(AccountID, out _))
                    throw new ArgumentOutOfRangeException(nameof(AccountID));
            }
            else
                AccountID = "AI";
            if (IsHumanHostile) /// Special Players Check on Hostiles Only
            {
                if (PlayerWatchlist.Entries.TryGetValue(AccountID, out var watchlistEntry)) // player is on watchlist
                {
                    Type = PlayerType.SpecialPlayer; // Flag watchlist player
                    UpdateAlerts($"[Watchlist] {watchlistEntry.Reason} @ {watchlistEntry.Timestamp}");
                }
            }
            PlayerHistory.AddOrUpdate(this); /// Log To Player History
        }


        /// <summary>
        /// System to Identify Guards.
        /// </summary>
        public class GuardIdentifier
        {
            private readonly string mapId;
            private bool[] hasGun;
            private Dictionary<int, List<bool[]>> gunHasMods;

            private static readonly Dictionary<string, List<string>> GuardBackpacks = new();
            private static readonly Dictionary<string, List<string>> GuardHelmets = new();
            private static readonly Dictionary<string, List<string>> GuardAmmo = new();
            private static readonly Dictionary<string, Dictionary<string, int>> GuardWeapons = new();
            private static readonly Dictionary<string, Dictionary<int, List<List<string>>>> GuardWeaponMods = new();
            private static readonly Dictionary<string, Func<bool[], Dictionary<int, List<bool[]>>, bool>> GuardWeaponChecks = new();

            static GuardIdentifier()
            {
                AddMap("shoreline",
                    new List<string> { "SFMP", "Beta 2", "Attack 2" },
                    new List<string> { "Altyn", "LShZ-2DTM" },
                    new List<string> { "m62", "m993", "pp", "bp", "ap-20", "ppbs" },
                    new Dictionary<string, List<List<string>>>
                    {
                { "VPO-101 Vepr-Hunter", new List<List<string>> { new List<string> { "USP-1", "USP-1 cup" } } },
                { "Saiga-12K", new List<List<string>> { new List<string> { "EKP-8-02 DT", "Powermag", "Sb.5" } } },
                { "VPO-136 Vepr-KM", new List<List<string>> { new List<string> { "B10M+B19" } } },
                { "AKM", new List<List<string>> { new List<string> { "B-10", "RK-6" } } },
                { "AKS-74UB", new List<List<string>> { new List<string> { "PBS-4", "EKP-8-02 DT", "B-11" } } }
                    }
                );
                AddMap("bigmap",
                    null,
                    new List<string> { "Altyn" },
                    new List<string> { "bp", "pp", "ppbs", "ap-m", "m856a1" },
                    new Dictionary<string, List<List<string>>>
                    {
                { "AK-103", new List<List<string>> { new List<string> { "B10M+B19", "SAW", "B-33" } } },
                { "AKS-74N", new List<List<string>> { new List<string> { "TRAX 1", "PK-06" } } },
                { "VPO-209", new List<List<string>>
                        {
                            new List<string> { "VS Combo", "SAW", "R43 .366TKM" },
                            new List<string> { "VS Combo" }
                        }
                },
                { "AK-74M", new List<List<string>>
                    {
                        new List<string> { "B10M+B19", "OKP-7 DT", "RK-3" },
                        new List<string> { "B10M+B19", "OKP-7", "RK-3" }
                    }
                },
                { "ADAR 2-15", new List<List<string>> { new List<string> { "GL-SHOCK", "Compact 2x32", "Stark AR" } } }
                    }
                );
                AddMap("rezervbase",
                    new List<string> { "Attack 2" },
                    new List<string> { "Altyn", "LShZ-2DTM", "Maska-1SCh", "Vulkan-5", "ZSh-1-2M" },
                    new List<string> { "m62", "m80", "zvezda", "shrap-10", "pp" },
                    new Dictionary<string, List<List<string>>>
                    {
                { "RPDN", new List<List<string>> { new List<string> { "USP-1" } } },
                { "M1A", new List<List<string>> { new List<string> { "Archangel M1A", "M14" } } },
                { "AS VAL", new List<List<string>> { new List<string> { "B10M+B19" } } },
                { "AK-74M", new List<List<string>>
                    {
                        new List<string> { "B-10", "RK-6" },
                        new List<string> { "AK 100", "RK-4" },
                        new List<string> { "AK 100" },
                        new List<string> { "VS Combo", "USP-1" }
                    }
                },
                { "AK-104", new List<List<string>>
                    {
                        new List<string> { "Kobra" },
                        new List<string> { "USP-1" },
                        new List<string> { "AKM-L" },
                        new List<string> { "Zhukov-U" },
                        new List<string> { "Molot" }
                    }
                },
                { "AK-12", new List<List<string>> { new List<string> { "Krechet" } } },
                { "M4A1", new List<List<string>>
                    {
                        new List<string> { "553" },
                        new List<string> { "M7A1PDW", "MK12", "MOE SL" },
                        new List<string> { "MOE SL" }
                    }
                },
                { "MP-133", new List<List<string>> { new List<string> { "MP-133x8" } } },
                { "MP-153", new List<List<string>> { new List<string> { "MP-153x8" } } },
                { "KS-23M Drozd", new List<List<string>> { new List<string> { "" } } },
                { "AKMS", new List<List<string>> { new List<string> { "VS Combo", "GEN M3" } } },
                { "AKM", new List<List<string>> { new List<string> { "VS Combo", "GEN M3" } } },
                { "AKMN", new List<List<string>> { new List<string> { "VS Combo", "GEN M3" } } },
                { "Saiga-12K", new List<List<string>>
                    {
                        new List<string> { "P1x42", "Powermag" },
                        new List<string> { "P1x42", "GL-SHOCK" },
                        new List<string> { "Powermag", "GL-SHOCK" }
                    }
                },
                { "MP5", new List<List<string>> { new List<string> { "MP5 Tri-Rail" } } },
                { "RPK-16", new List<List<string>> { new List<string> { "EKP-8-18" } } },
                { "PP-19-01", new List<List<string>>
                    {
                        new List<string> { "EKP-8-18" },
                        new List<string> { "Vityaz-SN" }
                    }
                },
                { "MP5K-N", new List<List<string>>
                    {
                        new List<string> { "EKP-8-18" },
                        new List<string> { "SRS-02" },
                        new List<string> { "X-5 MP5" }
                    }
                } }
                );
                AddMap("streets",
                    new List<string> { "Attack 2" },
                    new List<string> { "Altyn", "LShZ-2DTM", "Maska-1SCh", "Vulkan-5", "ZSh-1-2M" },
                    new List<string> { "m62", "m80", "zvezda", "shrap-10", "pp" },
                    new Dictionary<string, List<List<string>>>
                    {
                        { "RPDN", new List<List<string>> { new List<string> { "USP-1" } } },
                        { "PP-19-01", new List<List<string>>
                        {
                            new List<string> { "EKP-8-18" },
                            new List<string> { "Vityaz-SN" }
                        }
                },
                    });
            }

            public GuardIdentifier(string mapId)
            {
                this.mapId = mapId?.ToLower() ?? string.Empty;
            }

            public static void AddMap(
                string newMapId,
                List<string> backpacks,
                List<string> helmets,
                List<string> ammo,
                Dictionary<string, List<List<string>>> weaponsAndModVariants)
            {
                newMapId = newMapId?.ToLower() ?? string.Empty;

                if (!GuardBackpacks.ContainsKey(newMapId))
                    GuardBackpacks[newMapId] = backpacks ?? new List<string>();

                if (!GuardHelmets.ContainsKey(newMapId))
                    GuardHelmets[newMapId] = helmets ?? new List<string>();

                if (!GuardAmmo.ContainsKey(newMapId))
                    GuardAmmo[newMapId] = ammo ?? new List<string>();

                if (!GuardWeapons.ContainsKey(newMapId))
                {
                    var weaponDict = new Dictionary<string, int>();
                    var modsDict = new Dictionary<int, List<List<string>>>();
                    int weaponIndex = 0;

                    if (weaponsAndModVariants != null)
                    {
                        foreach (var weaponEntry in weaponsAndModVariants)
                        {
                            weaponDict[weaponEntry.Key] = weaponIndex;
                            modsDict[weaponIndex] = weaponEntry.Value ?? new List<List<string>>();
                            weaponIndex++;
                        }
                    }

                    GuardWeapons[newMapId] = weaponDict;
                    GuardWeaponMods[newMapId] = modsDict;

                    GuardWeaponChecks[newMapId] = (hasGun, gunMods) =>
                    {
                        for (int i = 0; i < hasGun.Length; i++)
                        {
                            if (hasGun[i] && gunMods.TryGetValue(i, out var loadoutFlagsList))
                            {
                                foreach (var modFlags in loadoutFlagsList)
                                {
                                    if (modFlags.All(m => m))
                                        return true;
                                }
                            }
                        }
                        return false;
                    };
                }
            }

            public bool TryIdentifyGuard(GearManager gear, HandsManager hands, ref string name, ref PlayerType type)
            {
                //                                                                                                                                                                                                    < 21 GroundZero
                if (Memory.Players.Count(x => x.Type is PlayerType.AIBoss) == 0 || (mapId.Contains("factory4") || mapId.Equals("interchange") || mapId.Equals("laboratory") || mapId.Equals("lighthouse") || mapId.Equals("Sandbox")))
                    // no boss or map without guards = no guards
                    return false;
                if (type.ToString().ToLower().Contains("scav"))
                {
                    if (this.mapId == "woods") // camper + 12ga shotgun secondary 100% on guards
                    {
                        if (gear?.Equipment != null &&
                            gear.Equipment.TryGetValue("Scabbard", out var knife) &&
                            knife != null && knife.Short.ToLower() == "camper")
                        {
                            if (gear.Equipment.TryGetValue("SecondPrimaryWeapon", out var shotgun) &&
                                shotgun.Long.ToLower().Contains("12ga"))
                            {
                                name = "Guard";
                                type = PlayerType.AIRaider;
                                Reset();
                                return true;
                            }
                        }
                    }

                    if (IsGuardByBackpack(gear) || IsGuardByHelmet(gear) || IsGuardByAmmo(hands) || HasGuardWeapon(gear))
                    {
                        name = "Guard";
                        type = PlayerType.AIRaider;
                        Reset();
                        return true;
                    }
                }
                return false;
            }

            private bool IsGuardByBackpack(GearManager gear)
            {
                return GuardBackpacks.TryGetValue(mapId, out var allowedBackpacks) &&
                    gear?.Equipment != null &&
                    gear.Equipment.TryGetValue("Backpack", out var backpack) &&
                    backpack != null &&
                    allowedBackpacks.Contains(backpack.Short);
            }

            private bool IsGuardByHelmet(GearManager gear)
            {
                return GuardHelmets.TryGetValue(mapId, out var allowedHelmets) &&
                    gear?.Equipment != null &&
                    gear.Equipment.TryGetValue("Headwear", out var headwear) &&
                    headwear != null &&
                    allowedHelmets.Contains(headwear.Short);
            }

            private bool IsGuardByAmmo(HandsManager hands)
            {
                return GuardAmmo.TryGetValue(mapId, out var allowedAmmo) &&
                    hands?.CurrentItem != null &&
                    allowedAmmo.Any(ammo => hands.CurrentItem.ToLower().Contains(ammo));
            }

            private bool HasGuardWeapon(GearManager gear)
            {
                if (gear == null || !GuardWeapons.TryGetValue(mapId, out var allowedWeapons))
                    return false;

                hasGun = new bool[allowedWeapons.Count];
                gunHasMods = new Dictionary<int, List<bool[]>>();

                if (!GuardWeaponMods.TryGetValue(mapId, out var weaponMods))
                    return false;

                foreach (var weapon in allowedWeapons)
                {
                    if (weaponMods.TryGetValue(weapon.Value, out var loadouts))
                    {
                        var flagsList = new List<bool[]>();
                        foreach (var loadout in loadouts)
                        {
                            flagsList.Add(new bool[loadout.Count]);
                        }
                        gunHasMods[weapon.Value] = flagsList;
                    }
                }

                foreach (var loot in gear.Loot ?? Enumerable.Empty<LootItem>())
                {
                    if (loot.IsWeapon && allowedWeapons.TryGetValue(loot.ShortName, out var weaponIndex))
                    {
                        hasGun[weaponIndex] = true;
                    }
                }

                foreach (var loot in gear.Loot ?? Enumerable.Empty<LootItem>())
                {
                    if (loot.IsWeaponMod)
                    {
                        foreach (var weaponEntry in weaponMods)
                        {
                            int weaponIndex = weaponEntry.Key;
                            var loadouts = weaponEntry.Value;

                            for (int loadoutIndex = 0; loadoutIndex < loadouts.Count; loadoutIndex++)
                            {
                                var loadout = loadouts[loadoutIndex];

                                for (int modIndex = 0; modIndex < loadout.Count; modIndex++)
                                {
                                    if (string.Equals(loot.ShortName, loadout[modIndex], StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (gunHasMods.TryGetValue(weaponIndex, out var flagsList) &&
                                            loadoutIndex < flagsList.Count)
                                        {
                                            flagsList[loadoutIndex][modIndex] = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (GuardWeaponChecks.TryGetValue(mapId, out var checkFunc))
                {
                    return checkFunc(hasGun, gunHasMods);
                }

                return false;
            }

            private void Reset()
            {
                hasGun = null;
                gunHasMods = null;
            }
        }


        /// <summary>
        /// Get Player's Account ID.
        /// </summary>
        /// <returns>Account ID Numeric String.</returns>
        private string GetAccountID()
        {
            var idPTR = Memory.ReadPtr(this + Offsets.ObservedPlayerView.AccountId);
            return Memory.ReadUnityString(idPTR);
        }

        /// <summary>
        /// Gets player's Group Number.
        /// </summary>
        private int GetGroupID()
        {
            try
            {
                var grpIdPtr = Memory.ReadPtr(this + Offsets.ObservedPlayerView.GroupID);
                var grp = Memory.ReadUnityString(grpIdPtr);
                return _groups.GetGroup(grp);
            }
            catch { return -1; } // will return null if Solo / Don't have a team
        }

        /// <summary>
        /// Get Movement Context Instance.
        /// </summary>
        private ulong GetMovementContext()
        {
            var movementController = Memory.ReadPtrChain(ObservedPlayerController, Offsets.ObservedPlayerController.MovementController);
            return movementController;
        }

        /// <summary>
        /// Refresh Player Information.
        /// </summary>
        public override void OnRegRefresh(ScatterReadIndex index, IReadOnlySet<ulong> registered, bool? isActiveParam = null)
        {
            if (isActiveParam is not bool isActive)
                isActive = registered.Contains(this);
            if (isActive)
            {
                if (IsHuman)
                {
                    UpdateMemberCategory();
                    UpdatePlayerName();
                }
                UpdateHealthStatus();
            }
            base.OnRegRefresh(index, registered, isActive);
        }

        private void UpdatePlayerName()
        {
            try
            {
                string nickname = Profile?.Nickname;
                if (nickname is not null && this.Name != nickname)
                {
                    this.Name = nickname;
                    //_ = RunTwitchLookupAsync(nickname);
                }
            }
            catch (Exception ex)
            {
                $"ERROR updating Name for Player '{Name}': {ex}".printf();
            }
        }

        private bool _mcSet = false;
        private void UpdateMemberCategory()
        {
            try
            {
                if (!_mcSet)
                {
                    var mcObj = Profile?.MemberCategory;
                    if (mcObj is Enums.EMemberCategory memberCategory)
                    {
                        string alert = null;
                        if ((memberCategory & Enums.EMemberCategory.Developer) == Enums.EMemberCategory.Developer)
                        {
                            alert = "Developer Account";
                            Type = PlayerType.SpecialPlayer;
                        }
                        else if ((memberCategory & Enums.EMemberCategory.Sherpa) == Enums.EMemberCategory.Sherpa)
                        {
                            alert = "Sherpa Account";
                            Type = PlayerType.SpecialPlayer;
                        }
                        else if ((memberCategory & Enums.EMemberCategory.Emissary) == Enums.EMemberCategory.Emissary)
                        {
                            alert = "Emissary Account";
                            Type = PlayerType.SpecialPlayer;
                        }
                        this.UpdateAlerts(alert);
                        _mcSet = true;
                    }
                }
            }
            catch (Exception ex)
            {
                $"ERROR updating Member Category for '{Name}': {ex}".printf();
            }
        }

        /// <summary>
        /// Get Player's Updated Health Condition
        /// Only works in Online Mode.
        /// </summary>
        private void UpdateHealthStatus()
        {
            try
            {
                var tag = (Enums.ETagStatus)Memory.ReadValue<int>(ObservedHealthController + Offsets.ObservedHealthController.HealthStatus);
                if ((tag & Enums.ETagStatus.Dying) == Enums.ETagStatus.Dying)
                    HealthStatus = Enums.ETagStatus.Dying;
                else if ((tag & Enums.ETagStatus.BadlyInjured) == Enums.ETagStatus.BadlyInjured)
                    HealthStatus = Enums.ETagStatus.BadlyInjured;
                else if ((tag & Enums.ETagStatus.Injured) == Enums.ETagStatus.Injured)
                    HealthStatus = Enums.ETagStatus.Injured;
                else
                    HealthStatus = Enums.ETagStatus.Healthy;
            }
            catch (Exception ex)
            {
                $"ERROR updating Health Status for '{Name}': {ex}".printf();
            }
        }

        /// <summary>
        /// Get the Transform Internal Chain for this Player.
        /// </summary>
        /// <param name="bone">Bone to lookup.</param>
        /// <returns>Array of offsets for transform internal chain.</returns>
        public override uint[] GetTransformInternalChain(Bones bone) =>
            Offsets.ObservedPlayerView.GetTransformChain(bone);
    }
}
