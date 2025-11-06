using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using System.Reflection;
using System.Text.Json.Serialization;
using Path = System.IO.Path;

namespace WeaponAttachmentModifier
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.mconie.weaponattachmentmodifier";
        public override string Name { get; init; } = "WeaponAttachmentModifier";
        public override string Author { get; init; } = "McOnie";
        public override List<string>? Contributors { get; init; } = null;
        public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.3");
        public override List<string>? Incompatibilities { get; init; } = null;
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
        public override string? Url { get; init; } = null;
        public override bool? IsBundleMod { get; init; } = false;
        public override string? License { get; init; } = "MIT";
    }

    public class AttachmentOverride
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public float? ErgonomicsOverride { get; set; }
        public float? RecoilPercentageOverride { get; set; }
        public float? DurabilityBurnOverride { get; set; }
    }

    public class StatSettings
    {
        public bool EnableHardSetOverride { get; set; } = false;
        public float HardSetOverrideValue { get; set; } = 0f;
        public bool EnableAdditiveOverride { get; set; } = false;
        public float AdditiveOverrideValue { get; set; } = 0f;
        public float Multiplier { get; set; } = 1.0f;
    }

    public class ModConfig
    {
        public StatSettings ForegripErgonomics { get; set; } = new();
        public StatSettings ForegripRecoil { get; set; } = new();
        public StatSettings StockErgonomics { get; set; } = new();
        public StatSettings StockRecoil { get; set; } = new();
        public StatSettings PistolGripErgonomics { get; set; } = new();
        public StatSettings MuzzleDeviceErgonomics { get; set; } = new();
        public StatSettings MuzzleDeviceRecoil { get; set; } = new();
        public StatSettings MuzzleDeviceDurability { get; set; } = new();

        public List<AttachmentOverride> SpecificAttachmentOverrides { get; set; } = new List<AttachmentOverride>();
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1000)]
    public class WeaponAttachmentModifier(
        ISptLogger<WeaponAttachmentModifier> _logger,
        ModHelper _modHelper,
        DatabaseServer _databaseServer) : IOnLoad
    {
        private static readonly string ForegripID = "55818af64bdc2d5b648b4570";
        private static readonly string StockID = "55818a594bdc2db9688b456a";
        private static readonly string PistolGripID = "55818a684bdc2ddd698b456d";
        private static readonly string MuzzleBrakeID = "5448fe394bdc2d0d028b456c";
        private static readonly string SuppressorID = "550aa4cd4bdc2dd8348b456c";
        private static readonly string MuzzleAdapterID = "550aa4dd4bdc2dc9348b4569";

        public Task OnLoad()
        {
            var modFolder = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var configDir = Path.Combine(modFolder, "config");
            var modConfig = _modHelper.GetJsonDataFromFile<ModConfig>(configDir, "config.jsonc");
            var itemDB = _databaseServer.GetTables().Templates.Items;

            int itemsModified = 0;

            foreach (var item in itemDB.Values)
            {
                var itemProps = item.Properties;
                if (itemProps == null || item.Parent == null) continue;

                var overrideData = modConfig.SpecificAttachmentOverrides.Find(o => o.ItemId == item.Id);
                if (overrideData != null)
                {
                    ApplySpecificOverride(itemProps, overrideData, _logger, item.Name);
                    itemsModified++;
                    continue;
                }

                bool modified = false;

                if (item.Parent == ForegripID)
                {
                    modified |= ApplyCategoryLogic(itemProps, "Ergonomics", modConfig.ForegripErgonomics);
                    modified |= ApplyCategoryLogic(itemProps, "Recoil", modConfig.ForegripRecoil);
                }
                else if (item.Parent == StockID)
                {
                    modified |= ApplyCategoryLogic(itemProps, "Ergonomics", modConfig.StockErgonomics);
                    modified |= ApplyCategoryLogic(itemProps, "Recoil", modConfig.StockRecoil);
                }
                else if (item.Parent == PistolGripID)
                {
                    modified |= ApplyCategoryLogic(itemProps, "Ergonomics", modConfig.PistolGripErgonomics);
                }
                else if (item.Parent == MuzzleBrakeID || item.Parent == SuppressorID || item.Parent == MuzzleAdapterID)
                {
                    modified |= ApplyCategoryLogic(itemProps, "Ergonomics", modConfig.MuzzleDeviceErgonomics);
                    modified |= ApplyCategoryLogic(itemProps, "Recoil", modConfig.MuzzleDeviceRecoil);
                    modified |= ApplyCategoryLogic(itemProps, "DurabilityBurnModificator", modConfig.MuzzleDeviceDurability);
                }

                if (modified)
                {
                    itemsModified++;
                }
            }

            _logger.Success($"[Weapon Attachment Modifier] Mod Loaded: Stats changed for {itemsModified} attachments.");
            return Task.CompletedTask;
        }

        private bool ApplyCategoryLogic(TemplateItemProperties props, string propName, StatSettings settings)
        {
            float? baseValue = GetFloatValue(props, propName);
            
            if (baseValue == null) return false; 

            float finalValue = baseValue.Value;
            bool valueSet = false;

            if (settings.EnableHardSetOverride)
            {
                finalValue = settings.HardSetOverrideValue;
                valueSet = true;
            }
            else if (settings.EnableAdditiveOverride)
            {
                finalValue = baseValue.Value + settings.AdditiveOverrideValue;
                valueSet = true;
            }
            else if (settings.Multiplier != 1.0f)
            {
                if (propName == "Ergonomics" && baseValue.Value < 0)
                {
                    if (Math.Abs(settings.Multiplier) > 0.01f)
                        finalValue = baseValue.Value / settings.Multiplier;
                }
                else
                {
                    finalValue = baseValue.Value * settings.Multiplier;
                }
                valueSet = true;
            }

            if (valueSet)
            {
                if (propName == "DurabilityBurnModificator")
                    SetFloat(props, propName, finalValue);
                else
                    SetNumber(props, propName, (int)Math.Round(finalValue));
                
                return true;
            }
            return false;
        }

        private float? GetFloatValue(TemplateItemProperties props, string propName)
        {
            switch (propName)
            {
                case "Ergonomics":
                    return (float?)props.Ergonomics;
                case "Recoil":
                    return (float?)props.Recoil;
                case "DurabilityBurnModificator":
                    return (float?)props.DurabilityBurnModificator;
                default:
                    return null;
            }
        }

        private void ApplySpecificOverride(TemplateItemProperties itemProps, AttachmentOverride overrideData, ISptLogger<WeaponAttachmentModifier> _logger, string itemName)
        {
            string name = string.IsNullOrEmpty(overrideData.Name) ? itemName : overrideData.Name;

            if (overrideData.ErgonomicsOverride.HasValue && itemProps.Ergonomics.HasValue)
            {
                SetNumber(itemProps, "Ergonomics", (int)Math.Round(overrideData.ErgonomicsOverride.Value));
                _logger.Info($"[Weapon Attachment Modifier] Overriding {name} Ergo to {itemProps.Ergonomics}");
            }

            if (overrideData.RecoilPercentageOverride.HasValue && itemProps.Recoil.HasValue)
            {
                SetNumber(itemProps, "Recoil", (int)Math.Round(overrideData.RecoilPercentageOverride.Value));
                _logger.Info($"[Weapon Attachment Modifier] Overriding {name} Recoil % to {itemProps.Recoil}");
            }

            if (overrideData.DurabilityBurnOverride.HasValue && itemProps.DurabilityBurnModificator.HasValue)
            {
                SetFloat(itemProps, "DurabilityBurnModificator", overrideData.DurabilityBurnOverride.Value);
                _logger.Info($"[Weapon Attachment Modifier] Overriding {name} Durability Burn Rate to {itemProps.DurabilityBurnModificator}");
            }
        }

        private static (PropertyInfo? pi, object? owner) FindProp(object obj, string jsonName)
        {
            foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
                var j = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                if (!string.IsNullOrEmpty(j) && string.Equals(j, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
            }
            return (null, null);
        }

        private static void SetNumber(object obj, string jsonName, int value)
        {
            var (pi, owner) = FindProp(obj, jsonName);
            if (pi is null || owner is null) return;
            try
            {
                var tgt = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                if (tgt == typeof(int) || tgt == typeof(long) || tgt == typeof(short)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
                if (tgt == typeof(float) || tgt == typeof(double) || tgt == typeof(decimal)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
            }
            catch { }
        }

        private static void SetFloat(object obj, string jsonName, float value)
        {
            var (pi, owner) = FindProp(obj, jsonName);
            if (pi is null || owner is null) return;
            try
            {
                var tgt = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                if (tgt == typeof(float) || tgt == typeof(double) || tgt == typeof(decimal))
                {
                    pi.SetValue(owner, Convert.ChangeType(value, tgt));
                    return;
                }
            }
            catch { }
        }
    }
}