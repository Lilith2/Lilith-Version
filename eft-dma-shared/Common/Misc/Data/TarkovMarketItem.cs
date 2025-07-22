﻿using System.Text.Json.Serialization;

namespace eft_dma_shared.Common.Misc.Data
{
    /// <summary>
    /// Class JSON Representation of Tarkov Market Data.
    /// </summary>
    public class TarkovMarketItem
    {
        /// <summary>
        /// Item ID.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("bsgID")]
        public string BsgId { get; init; } = "NULL";
        /// <summary>
        /// Item Full Name.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("name")]
        public string Name { get; init; } = "NULL";
        /// <summary>
        /// Item Short Name.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("shortName")]
        public string ShortName { get; init; } = "NULL";
        /// <summary>
        /// Highest Vendor Price.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("price")]
        public long TraderPrice { get; init; } = 0;
        /// <summary>
        /// Optimal Flea Market Price.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("fleaPrice")]
        public long FleaPrice { get; init; } = 0;
        /// <summary>
        /// Number of slots taken up in the inventory.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("slots")]
        public int Slots { get; init; } = 1;
        [JsonInclude]
        [JsonPropertyName("categories")]
        public IReadOnlyList<string> Tags { get; init; } = new List<string>();
        /// <summary>
        /// True if this item is Important via the Filters.
        /// </summary>
        [JsonIgnore]
        public bool Important => CustomFilter?.Important ?? false;
        /// <summary>
        /// True if this item is Blacklisted via the Filters.
        /// </summary>
        [JsonIgnore]
        public bool Blacklisted => CustomFilter?.Blacklisted ?? false;
        /// <summary>
        /// Is a Medical Item.
        /// </summary>
        [JsonIgnore]
        public bool IsMed => Tags.Contains("Meds");
        /// <summary>
        /// Is a Food Item.
        /// </summary>
        [JsonIgnore]
        public bool IsFood => Tags.Contains("Food and drink");
        /// <summary>
        /// Is a backpack.
        /// </summary>
        [JsonIgnore]
        public bool IsBackpack => Tags.Contains("Backpack");
        /// <summary>
        /// Is a Weapon Item.
        /// </summary>
        [JsonIgnore]
        public bool IsWeapon => Tags.Contains("Weapon");
        /// <summary>
        /// Is Currency (Roubles,etc.)
        /// </summary>
        [JsonIgnore]
        public bool IsCurrency => Tags.Contains("Money");

        [JsonIgnore]
        public bool IsGear => Tags.Contains("Equipment") || IsPlateCarrier;
        /// <summary>
        /// Is a Weapon Mod.
        /// </summary>
        [JsonIgnore]
        public bool IsWeaponMod => Tags.Contains("Weapon mod");
        /// <summary>
        /// Is Currency (Roubles,etc.)
        /// </summary>

        [JsonIgnore]
        public bool IsUBGL => Tags.Contains("UBGL");

        [JsonIgnore]
        public bool IsT7 => Tags.Contains("Thermal Vision");

        [JsonIgnore]
        public bool IsNVG => Tags.Contains("Night Vision");

        [JsonIgnore]
        public bool IsWelding => Name.Contains("welding mask", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsAltyn => Name.Contains("altyn", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsRysT => Name.Contains("rys-t", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsMaska => Name.Contains("Maska-1SCh", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsVulkan => Name.Contains("vulkan", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsJuggernaught => Name.Contains("altyn", StringComparison.OrdinalIgnoreCase) || Name.Contains("rys-t", StringComparison.OrdinalIgnoreCase) || Name.Contains("Maska-1SCh", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public bool IsThermalScope => Tags.Contains("Special scope") && !IsNVG && !Name.ToLower().Contains("night vision scope") && !IsT7;

        [JsonIgnore]
        public bool IsBullet => Tags.Contains("Ammo");
        [JsonIgnore]
        public bool IsAmmo => Tags.Contains("Ammo container");
        [JsonIgnore]
        public bool IsContainer => Tags.Contains("Common container");
        [JsonIgnore]
        public bool IsThrowable => Tags.Contains("Throwable weapon");
        [JsonIgnore]
        public bool IsKey => Tags.Contains("Mechanical Key");
        [JsonIgnore]
        public bool IsKeycard => Tags.Contains("Keycard");
        [JsonIgnore]
        public bool IsHeadset => Tags.Contains("Headphones");
        [JsonIgnore]
        public bool IsHelmet => Tags.Contains("Helmet");
        [JsonIgnore]
        public bool IsRig => Tags.Contains("Chest rig");
        [JsonIgnore]
        public bool IsArmband => Tags.Contains("Arm Band");
        [JsonIgnore]
        public bool IsFaceCover => Tags.Contains("Face Cover");
        [JsonIgnore]
        public bool IsGlasses => Tags.Contains("Vis. observ. device");
        [JsonIgnore]
        public bool IsMelee => Tags.Contains("Knife");
        [JsonIgnore]
        public bool IsArmorPlate => Tags.Contains("Armor Plate");
        [JsonIgnore]
        public bool CouldHavePlates => (IsArmoredEquipment &&
            !Name.ToLower().Contains("soft") && !Name.ToLower().Contains("module-3M") && !Name.ToLower().Contains("visor") && !Name.ToLower().Contains("shield") &&
            !Name.ToLower().Contains("ops-core") || !Name.ToLower().Contains("mf-untar") && !ShortName.ToLower().Contains("af ") && !Name.ToLower().Contains("lshz-2dtm") &&
            !Name.ToLower().Contains("covers") && !Name.ToLower().Contains("") && !Name.ToLower().Contains("helmet") &&
            !IsHelmet &&
            !IsFaceCover &&
            !IsGlasses) || IsPlateCarrier;
        [JsonIgnore]

        public bool IsBodyArmor => Name.ToLower().Contains("body armor");
        [JsonIgnore]
        public bool IsArmoredEquipment => Tags.Contains("Armored equipment");
        [JsonIgnore]
        public bool IsPlateCarrier => Name.ToLower().Contains("plate carrier");
        [JsonIgnore]
        public bool IsArmoredRig => (IsRig && IsPlateCarrier);
        [JsonIgnore]
        public bool IsSpecialItem => Tags.Contains("Special item");
        [JsonIgnore]
        public bool IsRocket => Tags.Contains("Rocket") && Tags.Contains("Ammo");
        [JsonIgnore]
        public bool IsRocketLauncher => Tags.Contains("Rocket Launcher") && Tags.Contains("Weapon");

        [JsonIgnore]
        public bool IsPoster => Tags.Contains("Flyer");

        /// <summary>
        /// This field is set if this item has a special filter.
        /// </summary>
        [JsonIgnore]
        public LootFilterEntry CustomFilter { get; private set; }

        /// <summary>
        /// Set the Custom Filter for this item.
        /// </summary>
        public void SetFilter(LootFilterEntry filter)
        {
            if (filter?.Enabled ?? false)
                CustomFilter = filter;
            else
                CustomFilter = null;
        }

        public override string ToString() => Name;

        /// <summary>
        /// Format price numeral as a string.
        /// </summary>
        /// <param name="price">Price to convert to string format.</param>
        public static string FormatPrice(int price)
        {
            if (price >= 1000000)
                return (price / 1000000D).ToString("0.##") + "M";
            if (price >= 1000)
                return (price / 1000D).ToString("0") + "K";

            return price.ToString();
        }
    }
}
