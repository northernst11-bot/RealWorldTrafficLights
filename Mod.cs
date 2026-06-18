using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using RealWorldTrafficLights.Systems;

namespace RealWorldTrafficLights
{
    public sealed class Mod : IMod
    {
        public const string kModName = "RealWorldTrafficLights";

        public static readonly ILog log = LogManager.GetLogger($"{kModName}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            updateSystem.UpdateAfter<RealWorldTrafficLightSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            log.Info("RealWorldTrafficLightSystem registered");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
