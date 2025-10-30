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
        public override string ModGuid { get; init; } = "com.mconie.weaponattatchmentmodifier";
        public override string Name { get; init; } = "WeaponattAtchmentModifier";
        public override string Author { get; init; } = "McOnie";
        public override List<string>? Contributors { get; init; } = null;
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.2");
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

    public class ModConfig
    {
        public float ForegripErgonomicsMultiplier { get; set; } = 1.0f;
        public float StockErgonomicsMultiplier { get; set; } = 1.0f;
        public float PistolGripErgonomicsMultiplier { get; set; } = 1.0f;
        public float MuzzleDeviceErgonomicsMultiplier { get; set; } = 1.0f;
        public float ForegripRecoilMultiplier { get; set; } = 1.0f;
        public float StockRecoilMultiplier { get; set; } = 1.0f;
        public float MuzzleDeviceRecoilMultiplier { get; set; } = 1.0f;
        public float MuzzleDurabilityBurnMultiplier { get; set; } = 1.0f;

        public List<AttachmentOverride> SpecificAttachmentOverrides { get; set; } = new List<AttachmentOverride>();
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1000)]
    public class WeaponAttachmentModifier(
        ISptLogger<WeaponAttachmentModifier> _logger,
        ModHelper _modHelper,
        DatabaseServer _databaseServer) : IOnLoad
    {
        private static readonly string ForegripID       = "55818af64bdc2d5b648b4570";
        private static readonly string StockID          = "55818a594bdc2db9688b456a";
        private static readonly string PistolGripID     = "55818a684bdc2ddd698b456d";
        private static readonly string MuzzleBrakeID    = "5448fe394bdc2d0d028b456c";
        private static readonly string SuppressorID     = "550aa4cd4bdc2dd8348b456c";
        private static readonly string MuzzleAdapterID  = "550aa4dd4bdc2dc9348b4569";

        private static readonly List<string> AllTargetAttachmentIDs = new List<string>
        {
            ForegripID, StockID, PistolGripID, MuzzleBrakeID, SuppressorID, MuzzleAdapterID
        };

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

                if (itemProps == null || item.Parent == null)
                    continue;

                bool modified = false;
                float recoilMultiplier = 1.0f;
                float ergoMultiplier = 1.0f;
                bool isMuzzleDevice = false;

                if (item.Parent == ForegripID)
                {
                    recoilMultiplier = modConfig.ForegripRecoilMultiplier;
                    ergoMultiplier = modConfig.ForegripErgonomicsMultiplier;
                }
                else if (item.Parent == StockID)
                {
                    recoilMultiplier = modConfig.StockRecoilMultiplier;
                    ergoMultiplier = modConfig.StockErgonomicsMultiplier;
                }
                else if (item.Parent == PistolGripID)
                {
                    ergoMultiplier = modConfig.PistolGripErgonomicsMultiplier;
                }
                else if (item.Parent == MuzzleBrakeID || item.Parent == SuppressorID || item.Parent == MuzzleAdapterID)
                {
                    recoilMultiplier = modConfig.MuzzleDeviceRecoilMultiplier;
                    ergoMultiplier = modConfig.MuzzleDeviceErgonomicsMultiplier;
                    isMuzzleDevice = true;
                }

                if (ergoMultiplier != 1.0f && itemProps.Ergonomics.HasValue)
                {
                    float calculatedValue;
                    float baseErgo = (float)itemProps.Ergonomics.Value;
                    if (baseErgo < 0)
                    {
                        if (Math.Abs(ergoMultiplier) > 0.01f)
                        {
                            calculatedValue = baseErgo / ergoMultiplier;
                        }
                        else { calculatedValue = baseErgo; }
                    }
                    else
                    {
                        calculatedValue = baseErgo * ergoMultiplier;
                    }

                    SetNumber(itemProps, "Ergonomics", new[] { "Ergonomics" }, (int)Math.Round(calculatedValue));
                    modified = true;
                }

                if (recoilMultiplier != 1.0f && itemProps.Recoil.HasValue)
                {
                    float calculatedValue = (float)(itemProps.Recoil.Value * recoilMultiplier);
                    SetNumber(itemProps, "Recoil", new[] { "Recoil" }, (int)Math.Round(calculatedValue));
                    modified = true;
                }

                if (isMuzzleDevice && modConfig.MuzzleDurabilityBurnMultiplier != 1.0f && itemProps.DurabilityBurnModificator.HasValue)
                {
                    float calculatedBurn = (float)(itemProps.DurabilityBurnModificator.Value * modConfig.MuzzleDurabilityBurnMultiplier + 1);

                    SetFloat(itemProps, "DurabilityBurnModificator", new[] { "DurabilityBurnModificator" }, calculatedBurn);
                    modified = true;
                }

                var overrideData = modConfig.SpecificAttachmentOverrides.Find(o => o.ItemId == item.Id);

                if (overrideData != null)
                {
                    ApplyOverride(itemProps, overrideData, _logger, item.Name);
                    modified = true;
                }

                if (modified)
                {
                    itemsModified++;
                }
            }

            _logger.Success($"[Weapon Attachment Modifier] Mod Loaded: Stats changed for {itemsModified} attachments.");

            return Task.CompletedTask;
        }

        private void ApplyOverride(TemplateItemProperties itemProps, AttachmentOverride overrideData, ISptLogger<WeaponAttachmentModifier> _logger, string itemName)
        {
            string name = string.IsNullOrEmpty(overrideData.Name) ? itemName : overrideData.Name;

            if (overrideData.ErgonomicsOverride.HasValue)
            {
                SetNumber(itemProps, "Ergonomics", new[] { "Ergonomics" }, (int)Math.Round(overrideData.ErgonomicsOverride.Value));
                _logger.Success($"[Weapon Attachment Modifier] Overriding {name} Ergo to {itemProps.Ergonomics}");
            }

            if (overrideData.RecoilPercentageOverride.HasValue)
            {
                SetNumber(itemProps, "Recoil", new[] { "Recoil" }, (int)Math.Round(overrideData.RecoilPercentageOverride.Value));
                _logger.Success($"[Weapon Attachment Modifier] Overriding {name} Recoil % to {itemProps.Recoil}");
            }

            if (overrideData.DurabilityBurnOverride.HasValue)
            {
                SetFloat(itemProps, "DurabilityBurnModificator", new[] { "DurabilityBurnModificator" }, overrideData.DurabilityBurnOverride.Value);
                _logger.Success($"[Weapon Attachment Modifier] Overriding {name} Durability Burn Rate to {itemProps.DurabilityBurnModificator}");
            }
        }

        private static (PropertyInfo? pi, object? owner) FindProp(object obj, string jsonName, string[] candidates)
        {
            foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
                if (Array.Exists(candidates, c => string.Equals(c, p.Name, StringComparison.OrdinalIgnoreCase))) return (p, obj);
                var j = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                if (!string.IsNullOrEmpty(j) && string.Equals(j, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
            }
            return (null, null);
        }

        private static void SetNumber(object obj, string jsonName, string[] candidates, int value)
        {
            var (pi, owner) = FindProp(obj, jsonName, candidates);
            if (pi is null || owner is null) return;
            try
            {
                var tgt = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                if (tgt == typeof(int) || tgt == typeof(long) || tgt == typeof(short)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
                if (tgt == typeof(float) || tgt == typeof(double) || tgt == typeof(decimal)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
            }
            catch { }
        }

        private static void SetFloat(object obj, string jsonName, string[] candidates, float value)
        {
            var (pi, owner) = FindProp(obj, jsonName, candidates);
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