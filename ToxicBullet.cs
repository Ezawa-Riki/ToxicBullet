using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using MassivesoftCore;
using System.Reflection;
using SPTarkov.Server.Core.Utils;
using System.Text.Json;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace ToxicBullet
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string Name { get; init; } = "ToxicBullet";
        public override string Author { get; init; } = "Massivesoft";
        public override List<string>? Contributors { get; init; }
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");


        public override List<string>? Incompatibilities { get; init; }
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new Dictionary<string, SemanticVersioning.Range>{
            { "com.massivesoft.massivesoftcore", new SemanticVersioning.Range(">=1.0.0") }
        };
        public override string? Url { get; init; }
        public override bool? IsBundleMod { get; init; } = true;
        public override string? License { get; init; } = "MIT";
        public override string ModGuid { get; init; } = "com.massivesoft.toxicbullet";
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
    public class MassivesoftBaseItemsClass(
        ISptLogger<MassivesoftBaseItemsClass> logger,
        MassivesoftCoreClass massivesoftCoreClass,
        ModHelper modHelper,
        FileUtil fileUtil,
        BundleHashCacheService bundleHashCacheService,
        JsonUtil jsonUtil,
        BundleLoader bundleLoader
        ) : IOnLoad
    {
        public Task OnLoad()
        {
            string modName = new ModMetadata().Name;
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            //Load Item Jsons
            DirectoryInfo directoryInfoDB = new DirectoryInfo(Path.Combine(pathToMod, "db"));
            foreach (FileInfo file in directoryInfoDB.GetFiles())
            {
                if (file.Extension == ".json")
                {
                    string json = fileUtil.ReadFile(file.FullName);
                    try
                    {
                        Dictionary<string, AdvancedNewItemFromCloneDetails>? details = JsonSerializer.Deserialize<Dictionary<string, AdvancedNewItemFromCloneDetails>>(json, JsonUtil.JsonSerializerOptionsNoIndent);
                        if (details == null || details.Count == 0)
                        {
                            logger.Error($"Json file {file.Name} of {modName} is invalid (null)!");
                            continue;
                        }
                        foreach (KeyValuePair<string, AdvancedNewItemFromCloneDetails> pair in details)
                        {
                            massivesoftCoreClass.AdvancedCreateItemFromClone(pair.Value);
                        }
                    }
                    catch (JsonException exception)
                    {
                        logger.Error($"Json file {file.Name} of {modName} is invalid! {exception.Message}");
                    }
                }
            }


            //Load Custom Buffs
            DirectoryInfo customBuffInfoDB = new(Path.Combine(pathToMod, "buffs"));
            Dictionary<string, Buff[]> Buffs = [];
            foreach (FileInfo file in customBuffInfoDB.GetFiles())
            {
                if (file.Extension == ".json")
                {
                    string json = fileUtil.ReadFile(file.FullName);
                    string buffName = Path.GetFileNameWithoutExtension(file.Name);
                    try
                    {
                        Buff[]? buff = JsonSerializer.Deserialize<Buff[]>(json, JsonUtil.JsonSerializerOptionsNoIndent);
                        if (buff == null || buff.Length == 0)
                        {
                            logger.Error($"Json file {file.Name} of {modName} is invalid (null)!");
                            continue;
                        }
                        Buffs.Add(buffName, buff);
                    }
                    catch (JsonException exception)
                    {
                        logger.Error($"Json file {file.Name} of {modName} is invalid! {exception.Message}");
                    }
                }
            }
            massivesoftCoreClass.AddBuffs(Buffs);

            return Task.CompletedTask;
        }
        public async Task LoadBundlesAsync(string pathBundle)
        {
            await bundleHashCacheService.HydrateCache();

            var modPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), pathBundle);

            var modBundles = await jsonUtil.DeserializeFromFileAsync<BundleManifest>(
                Path.Join(pathBundle, "bundles.json")
            );

            var bundleManifests = modBundles?.Manifest ?? [];

            foreach (var bundleManifest in bundleManifests)
            {
                var relativeModPath = modPath.Replace('\\', '/');

                var bundleLocalPath = Path.Join(relativeModPath, "bundles", bundleManifest.Key).Replace('\\', '/');

                if (!File.Exists(bundleLocalPath))
                {
                    logger.Warning($"Could not find bundle {bundleManifest.Key}");
                    continue;
                }

                var bundleHash = await bundleHashCacheService.CalculateMatchAndStoreHash(bundleLocalPath);

                bundleLoader.AddBundle(bundleManifest.Key, new BundleInfo(relativeModPath, bundleManifest, bundleHash));
            }
        }
    }
}

