﻿using eft_dma_shared.Common.Misc.Data.TarkovMarket;
using eft_dma_shared.Common.UI;
using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eft_dma_shared.Common.Misc.Data
{
    public static class EftDataManager
    {
        private const string _dataFileName = "data.json";
        private static readonly string _dataFile = Path.Combine(SharedProgram.ConfigPath.FullName, _dataFileName);

        /// <summary>
        /// Master items dictionary - mapped via BSGID String.
        /// </summary>
        public static FrozenDictionary<string, TarkovMarketItem> AllItems { get; private set; }

        /// <summary>
        /// Master containers dictionary - mapped via BSGID String.
        /// </summary>
        public static FrozenDictionary<string, TarkovMarketItem> AllContainers { get; private set; }

        /// <summary>
        /// Quest Data for Tarkov.
        /// </summary>
        public static FrozenDictionary<string, TaskElement> TaskData { get; private set; }

        #region Startup

        /// <summary>
        /// Call to start LoneDataManager Module. ONLY CALL ONCE.
        /// </summary>
        /// <param name="loading">Loading UI Form.</param>
        /// <param name="defaultOnly">True if you want to load cached/default data only.</param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public static async Task ModuleInitAsync(LoadingForm loading, bool defaultOnly = false)
        {
            try
            {
                var data = await GetDataAsync(loading, defaultOnly);
                AllItems = data.Items.Where(x => !x.Tags?.Contains("Static Container") ?? false)
                    .DistinctBy(x => x.BsgId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(k => k.BsgId, v => v, StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                AllContainers = data.Items.Where(x => x.Tags?.Contains("Static Container") ?? false)
                    .DistinctBy(x => x.BsgId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(k => k.BsgId, v => v, StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                TaskData = data.Tasks
                    .DistinctBy(x => x.Id)
                    .ToDictionary(k => k.Id, v => v, StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"ERROR loading {_dataFileName}", ex);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads Market data via several possible methods (cached,web,embedded resource).
        /// </summary>
        /// <returns>Collection of TarkovMarketItems.</returns>
        private static async Task<TarkovMarketData> GetDataAsync(LoadingForm loading, bool defaultOnly)
        {
            TarkovMarketData data;
            string json = null;
            if (!defaultOnly &&
                (!File.Exists(_dataFile) ||
            File.GetLastWriteTime(_dataFile).AddHours(.5) < DateTime.Now)) // // only update every .5h. Originally set to every 4h by Lone
            {
                loading.UpdateStatus("Getting Updated Tarkov.Dev Data...", loading.PercentComplete);
                json = await GetUpdatedDataJsonAsync();
                if (json is not null)
                {
                    await File.WriteAllTextAsync(_dataFile, json);
                }
            }
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            if (json is null && File.Exists(_dataFile))
            {
                json = await File.ReadAllTextAsync(_dataFile);
            }
            json ??= await GetDefaultDataAsync();
            try
            {
                data = JsonSerializer.Deserialize<TarkovMarketData>(json, jsonOptions);
            }
            catch (JsonException)
            {
                File.Delete(_dataFile); // Delete data if json is corrupt.
                throw;
            }
            ArgumentNullException.ThrowIfNull(data, nameof(data));
            return data;
        }

        private static async Task<string> GetDefaultDataAsync()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("eft-dma-shared.DEFAULT_DATA.json"))
            {
                var data = new byte[stream!.Length];
                await stream.ReadExactlyAsync(data);
                return Encoding.UTF8.GetString(data);
            }
        }

        /// <summary>
        /// Contacts the Loot Server for an updated Loot List.
        /// </summary>
        /// <returns>Json string of Loot List.</returns>
        private static async Task<string> GetUpdatedDataJsonAsync()
        {
            try
            {
                return await TarkovMarketJob.GetUpdatedMarketDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WARNING: Failed to retrieve updated Tarkov Market Data. Will use backup source(s).\n\n{ex}",
                    nameof(EftDataManager),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly);
                return null;
            }
        }

        #endregion

        #region Types

        public sealed class TarkovMarketData
        {
            [JsonPropertyName("items")]
            public List<TarkovMarketItem> Items { get; set; }
            [JsonPropertyName("tasks")]
            public List<TaskElement> Tasks { get; set; }
        }

        public partial class TaskElement
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("kappaRequired")]
            public bool KappaRequired { get; set; }

            [JsonPropertyName("objectives")]
            public List<ObjectiveElement> Objectives { get; set; }

            public partial class ObjectiveElement
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }

                [JsonPropertyName("type")]
                public string Type { get; set; }

                [JsonPropertyName("optional")]
                public bool Optional { get; set; }

                [JsonPropertyName("description")]
                public string Description { get; set; }

                [JsonPropertyName("requiredKeys")]
                public List<List<MarkerItemClass>> RequiredKeys { get; set; }

                [JsonPropertyName("maps")]
                public List<BasicDataElement> Maps { get; set; }

                [JsonPropertyName("zones")]
                public List<ZoneElement> Zones { get; set; }

                [JsonPropertyName("count")]
                public int Count { get; set; }

                [JsonPropertyName("foundInRaid")]
                public bool FoundInRaid { get; set; }

                [JsonPropertyName("item")]
                public MarkerItemClass Item { get; set; }

                [JsonPropertyName("questItem")]
                public ObjectiveQuestItem QuestItem { get; set; }

                [JsonPropertyName("markerItem")]
                public MarkerItemClass MarkerItem { get; set; }

                public partial class MarkerItemClass
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }

                    [JsonPropertyName("shortName")]
                    public string ShortName { get; set; }
                }

                public partial class ObjectiveQuestItem
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }

                    [JsonPropertyName("shortName")]
                    public string ShortName { get; set; }

                    [JsonPropertyName("normalizedName")]
                    public string NormalizedName { get; set; }

                    [JsonPropertyName("description")]
                    public string Description { get; set; }
                }

                public partial class ZoneElement
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("outline")]
                    public List<PositionElement> Outline { get; set; }

                    [JsonPropertyName("position")]
                    public PositionElement Position { get; set; }

                    [JsonPropertyName("map")]
                    public BasicDataElement Map { get; set; }
                }

                public partial class BasicDataElement
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("normalizedName")]
                    public string NormalizedName { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }
                }

                public partial class PositionElement
                {
                    [JsonPropertyName("y")]
                    public float Y { get; set; }

                    [JsonPropertyName("x")]
                    public float X { get; set; }

                    [JsonPropertyName("z")]
                    public float Z { get; set; }
                }
            }
        }

        #endregion
    }
}