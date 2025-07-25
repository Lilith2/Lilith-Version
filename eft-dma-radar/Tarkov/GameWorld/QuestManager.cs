﻿using eft_dma_radar.Tarkov.EFTPlayer;
using eft_dma_radar.UI.Radar;
using eft_dma_radar.UI.Misc;
using System.Collections.Frozen;
using eft_dma_shared.Common.Unity;
using eft_dma_shared.Common.Unity.Collections;
using eft_dma_shared.Common.Maps;
using eft_dma_shared.Common.Players;
using eft_dma_shared.Common.Misc.Data;
using eft_dma_shared.Common.Misc;

namespace eft_dma_radar.Tarkov.GameWorld
{
    public sealed class QuestManager
    {

        private static readonly FrozenDictionary<string, string> _mapToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "factory4_day", "55f2d3fd4bdc2d5f408b4567" },
            { "factory4_night", "59fc81d786f774390775787e" },
            { "bigmap", "56f40101d2720b2a4d8b45d6" },
            { "woods", "5704e3c2d2720bac5b8b4567" },
            { "lighthouse", "5704e4dad2720bb55b8b4567" },
            { "shoreline", "5704e554d2720bac5b8b456e" },
            { "labyrinth", "6733700029c367a3d40b02af" },
            { "rezervbase", "5704e5fad2720bc05b8b4567" },
            { "interchange", "5714dbc024597771384a510d" },
            { "tarkovstreets", "5714dc692459777137212e12" },
            { "laboratory", "5b0fc42d86f7744a585f9105" },
            { "Sandbox", "653e6760052c01c1c805532f" },
            { "Sandbox_high", "65b8d6f5cdde2479cb2a3125" }
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private static readonly FrozenDictionary<string, FrozenDictionary<string, Vector3>> _questZones = EftDataManager.TaskData.Values
            .Where(task => task.Objectives is not null) // Ensure the Objectives are not null
            .SelectMany(task => task.Objectives)   // Flatten the Objectives from each TaskElement
            .Where(objective => objective.Zones is not null) // Ensure the Zones are not null
            .SelectMany(objective => objective.Zones)    // Flatten the Zones from each Objective
            .Where(zone => zone.Position is not null && zone.Map?.Id is not null) // Ensure Position and Map are not null
            .GroupBy(zone => zone.Map.Id, zone => new
            {
                id = zone.Id,
                pos = new Vector3(zone.Position.X, zone.Position.Y, zone.Position.Z)
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key, // Map Id
                group => group
                .DistinctBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    zone => zone.id,
                    zone => zone.pos,
                    StringComparer.OrdinalIgnoreCase
                ).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase
            )
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private static readonly FrozenDictionary<string, FrozenDictionary<string, List<Vector3>>> _questOutlines = EftDataManager.TaskData.Values
            .Where(task => task.Objectives is not null) // Ensure the Objectives are not null
            .SelectMany(task => task.Objectives) // Flatten the Objectives from each TaskElement
            .Where(objective => objective.Zones is not null) // Ensure the Zones are not null
            .SelectMany(objective => objective.Zones) // Flatten the Zones from each Objective
            .Where(zone => zone.Outline is not null && zone.Map?.Id is not null) // Ensure Outline and Map are not null
            .GroupBy(zone => zone.Map.Id, zone => new
            {
                id = zone.Id,
                outline = zone.Outline.Select(pos => new Vector3(pos.X, pos.Y, pos.Z)).ToList()
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key, // Map Id
                group => group
                    .DistinctBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        zone => zone.id,
                        zone => zone.outline,
                        StringComparer.OrdinalIgnoreCase
                    ).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase
            )
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private readonly Stopwatch _rateLimit = new();
        private readonly ulong _profile;

        public QuestManager(ulong profile)
        {
            _profile = profile;
            Refresh();
        }

        /// <summary>
        /// Currently logged quests.
        /// </summary>
        public IReadOnlySet<string> CurrentQuests { get; private set; } = new HashSet<string>();
        /// <summary>
        /// Contains a List of BSG ID's that we need to pickup.
        /// </summary>
        public IReadOnlySet<string> ItemConditions { get; private set; } = new HashSet<string>();

        /// <summary>
        /// Contains a List of locations that we need to visit.
        /// </summary>
        public IReadOnlyList<QuestLocation> LocationConditions { get; private set; } = new List<QuestLocation>();

        public Dictionary<string, ulong[]> QuestConditions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> QuestItems { get; } = new(new Dictionary<string, HashSet<string>>());

        /// <summary>
        /// Map Identifier of Current Map.
        /// </summary>
        private static string MapID
        {
            get
            {
                var id = Memory.MapID;
                id ??= "MAPDEFAULT";
                return id;
            }
        }

        public void Refresh()
        {
            if (_rateLimit.IsRunning && _rateLimit.Elapsed.TotalSeconds < 2d)
                return;
            var currentQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var masterItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var masterLocations = new List<QuestLocation>();
            var questsData = Memory.ReadPtr(_profile + Offsets.Profile.QuestsData);
            using var questsDataList = MemList<ulong>.Get(questsData);
            foreach (var qDataEntry in questsDataList) // GCLass1BBF
            {
                try
                {
                    var qStatus = Memory.ReadValue<int>(qDataEntry + Offsets.QuestData.Status);
                    if (qStatus != 2) // 2 == Started
                        continue;
                    var completedPtr = Memory.ReadPtr(qDataEntry + Offsets.QuestData.CompletedConditions);
                    using var completedHS = MemHashSet<Types.MongoID>.Get(completedPtr);
                    var completedConditions = new HashSet<string>();
                    foreach (var c in completedHS)
                    {
                        var completedCond = Memory.ReadUnityString(c.Value.StringID);
                        completedConditions.Add(completedCond);
                    }

                    var qIDPtr = Memory.ReadPtr(qDataEntry + Offsets.QuestData.Id);
                    var qID = Memory.ReadUnityString(qIDPtr);
                    currentQuests.Add(qID);
                    if (Program.Config.QuestHelper.BlacklistedQuests.Contains(qID, StringComparer.OrdinalIgnoreCase))
                        continue;
                    var qTemplate = Memory.ReadPtr(qDataEntry + Offsets.QuestData.Template); // GClass1BF4
                    var qConditions =
                        Memory.ReadPtr(qTemplate + Offsets.QuestTemplate.Conditions); // EFT.Quests.QuestConditionsList
                    using var qCondDict = MemDictionary<int, ulong>.Get(qConditions);
                    foreach (var qDicCondEntry in qCondDict)
                    {
                        var condListPtr = Memory.ReadPtr(qDicCondEntry.Value + Offsets.QuestConditionsContainer.ConditionsList);
                        using var condList = MemList<ulong>.Get(condListPtr);
                        foreach (var condition in condList)
                            GetQuestConditions(qID, condition, completedConditions, masterItems, masterLocations);
                    }
                }
                catch (Exception ex)
                {
                    LoneLogging.WriteLine($"[QuestManager] ERROR parsing Quest at 0x{qDataEntry.ToString("X")}: {ex}");
                }
            }
            CurrentQuests = currentQuests;
            ItemConditions = masterItems;
            LocationConditions = masterLocations;
            _rateLimit.Restart();
        }

        public static void GetQuestConditions(string questID, ulong condition, HashSet<string> completedConditions,
            HashSet<string> items, List<QuestLocation> locations)
        {
            try
            {
                var condIDPtr = Memory.ReadValue<Types.MongoID>(condition + Offsets.QuestCondition.id);
                var condID = Memory.ReadUnityString(condIDPtr.StringID);
                if (completedConditions.Contains(condID))
                    return;
                var condName = ObjectClass.ReadName(condition);
                // ConditionWeaponAssembly = gunsmith
                if (condName == "ConditionFindItem" || condName == "ConditionHandoverItem")
                {
                    var targetArray =
                        Memory.ReadPtr(condition + Offsets.QuestConditionFindItem.target); // this is a typical unity array[] at 0x48
                    using var targets = MemArray<ulong>.Get(targetArray);
                    foreach (var targetPtr in targets)
                    {
                        var target = Memory.ReadUnityString(targetPtr);
                        items.Add(target);
                    }
                }
                else if (condName == "ConditionPlaceBeacon" || condName == "ConditionLeaveItemAtLocation")
                {
                    var zoneIDPtr = Memory.ReadPtr(condition + Offsets.QuestConditionPlaceBeacon.zoneId);
                    var target = Memory.ReadUnityString(zoneIDPtr); // Zone ID
                    if (_mapToId.TryGetValue(MapID, out var id) &&
                        _questZones.TryGetValue(id, out var zones) &&
                        zones.TryGetValue(target, out var loc))
                    {
                        locations.Add(new QuestLocation(questID, target, loc));
                    }
                }
                else if (condName == "ConditionVisitPlace")
                {
                    var targetPtr = Memory.ReadPtr(condition + Offsets.QuestConditionVisitPlace.target);
                    var target = Memory.ReadUnityString(targetPtr);
                    if (_mapToId.TryGetValue(MapID, out var id) &&
                        _questZones.TryGetValue(id, out var zones) &&
                        zones.TryGetValue(target, out var loc))
                    {
                        locations.Add(new QuestLocation(questID, target, loc));
                    }
                }
                else if (condName == "ConditionCounterCreator") // Check for children
                {
                    var conditionsPtr = Memory.ReadPtr(condition + Offsets.QuestConditionCounterCreator.Conditions);
                    var conditionsListPtr = Memory.ReadPtr(conditionsPtr + Offsets.QuestConditionsContainer.ConditionsList);
                    using var counterList = MemList<ulong>.Get(conditionsListPtr);
                    foreach (var childCond in counterList)
                        GetQuestConditions(questID, childCond, completedConditions, items, locations);
                }
                else if (condName == "ConditionLaunchFlare")
                {
                    var zonePtr = Memory.ReadPtr(condition + Offsets.QuestConditionLaunchFlare.zoneId);
                    var target = Memory.ReadUnityString(zonePtr);
                    if (_mapToId.TryGetValue(MapID, out var id) &&
                        _questZones.TryGetValue(id, out var zones) &&
                        zones.TryGetValue(target, out var loc))
                    {
                        locations.Add(new QuestLocation(questID, target, loc));
                    }
                }
                else if (condName == "ConditionZone")
                {
                    var zonePtr = Memory.ReadPtr(condition + Offsets.QuestConditionZone.zoneId);
                    var targetPtr = Memory.ReadPtr(condition + Offsets.QuestConditionZone.target);
                    var zone = Memory.ReadUnityString(zonePtr);
                    using var targets = MemArray<ulong>.Get(targetPtr);
                    foreach (var targetPtr2 in targets)
                        items.Add(Memory.ReadUnityString(targetPtr2));
                    if (_mapToId.TryGetValue(MapID, out var id) &&
                        _questZones.TryGetValue(id, out var zones) &&
                        zones.TryGetValue(zone, out var loc))
                    {
                        locations.Add(new QuestLocation(questID, zone, loc));
                    }
                }
                else if (condName == "ConditionInZone")
                {
                    var zonePtr2 = Memory.ReadPtr(condition + 0x70);
                    using var zones = MemArray<ulong>.Get(zonePtr2);
                    foreach (var zone in zones)
                    {
                        var id = Memory.ReadUnityString(zone);
                        if (_mapToId.TryGetValue(MapID, out var mapId) &&
                            _questOutlines.TryGetValue(mapId, out var outzone) &&
                            outzone.TryGetValue(id, out var outlines) &&
                            _questZones.TryGetValue(mapId, out var outpos) &&
                            outpos.TryGetValue(id, out var locc))
                        {
                            locations.Add(new QuestLocation(questID, id, locc, outlines));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoneLogging.WriteLine($"[QuestManager] ERROR parsing Condition(s): {ex}");
            }
        }
    }

    /// <summary>
    /// Wraps a Mouseoverable Quest Location marker onto the Map GUI.
    /// </summary>
    public sealed class QuestLocation : IWorldEntity, IMapEntity, IMouseoverEntity
    {
        /// <summary>
        /// Main UI/Application Config.
        /// </summary>
        public static Config Config { get; } = Program.Config;
        /// <summary>
        /// Name of this quest.
        /// </summary>
        public string Name { get; }

        public QuestLocation(string questID, string target, Vector3 position, List<Vector3> outline = null)
        {
            if (EftDataManager.TaskData.TryGetValue(questID, out var q))
                Name = q.Name;
            else
                Name = target;
            Position = position;
            Outline = outline;
        }

        public static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            int count = polygon.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public void Draw(SKCanvas canvas, LoneMapParams mapParams, ILocalPlayer localPlayer)
        {
            if ((this is QuestLocation) && !Memory.LocalPlayer.IsPmc)
                return;
            if (Outline is not null && Config.ShowZone)
            {
                var mapPoints = Outline.Select(p => p.ToMapPos(mapParams.Map).ToZoomedPos(mapParams)).ToList();

                using var path = new SKPath();
                bool first = true;

                foreach (var p in mapPoints)
                {
                    if (first)
                    {
                        path.MoveTo(p.X, p.Y);
                        first = false;
                    }
                    else
                    {
                        path.LineTo(p.X, p.Y);
                    }
                }
                path.Close(); // Close the shape

                canvas.DrawPath(path, SKPaints.PaintConnectorGroup);
            }
            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);
            var heightDiff = Position.Y - localPlayer.Position.Y;
            SKPaints.ShapeOutline.StrokeWidth = 2f;
            if (heightDiff > 1.45) // marker is above player
            {
                using var path = point.GetArrow();
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.QuestHelperPaint);
            }
            else if (heightDiff < -1.45) // marker is below player
            {
                using var path = point.GetArrow(6, false);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.QuestHelperPaint);
            }
            else // marker is level with player
            {
                var squareSize = 8 * MainForm.UIScale;
                canvas.DrawRect(point.X, point.Y,
                    squareSize, squareSize, SKPaints.ShapeOutline);
                canvas.DrawRect(point.X, point.Y,
                    squareSize, squareSize, SKPaints.QuestHelperPaint);
            }
        }

        public Vector2 MouseoverPosition { get; set; }

        public void DrawMouseover(SKCanvas canvas, LoneMapParams mapParams, LocalPlayer localPlayer)
        {
            string[] lines = new string[] { Name };
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, lines);
            foreach (var vector in Outline.Select(p => p.ToMapPos(mapParams.Map).ToZoomedPos(mapParams)))
            {
                vector.DrawMouseoverText(canvas, lines);
            }
        }

        private Vector3 _position;
        private List<Vector3> _outline;
        public ref Vector3 Position => ref _position;
        public ref List<Vector3> Outline => ref _outline;
    }
}