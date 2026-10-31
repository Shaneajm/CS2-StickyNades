using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace StickyNades;

public partial class StickyNades : BasePlugin, IPluginConfig<StickyNadesConfig>
{
    public override string ModuleName => "StickyNades";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "shaneajm";
    public override string ModuleDescription => "HE grenades stick to the first surface or player they hit.";

    public StickyNadesConfig Config { get; set; } = new();

    private readonly Dictionary<uint, TrackedNade> _nades = new();

    private ConVar? _teammatesAreEnemiesCvar;

    private bool _teammatesAreEnemies;

    public void OnConfigParsed(StickyNadesConfig config)
    {
        if (config.Version < Config.Version)
        {
            Logger.LogWarning(
                "Config is out of date (expected version {Expected}, found {Found}). " +
                "Delete it to regenerate it with the new settings.", Config.Version, config.Version);
        }

        config.ToggleCommands = config.ToggleCommands
            .Select(c => c.Trim().TrimStart('!', '/'))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _teammatesAreEnemiesCvar = ConVar.Find("mp_teammates_are_enemies");

        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnTick>(UpdateCenterMessages);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _nades.Clear();
            _centerMessages.Clear();
        });
        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            _nades.Clear();
            return HookResult.Continue;
        });

        RegisterToggle();
    }

    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (!Config.Enabled || entity.DesignerName != "hegrenade_projectile")
            return;

        var index = entity.Index;
        Server.NextFrame(() =>
        {
            var nade = Utilities.GetEntityFromIndex<CHEGrenadeProjectile>((int)index);
            if (nade is null || !nade.IsValid || nade.AbsOrigin is null || nade.AbsVelocity is null)
                return;

            if (!IsStickyFor(nade.Thrower.Value?.OriginalController.Value))
                return;

            _nades[index] = new TrackedNade
            {
                Projectile = nade,
                LastPosition = Copy(nade.AbsOrigin),
                LastVelocity = Copy(nade.AbsVelocity),
            };
        });
    }

    private void OnTick()
    {
        if (_nades.Count == 0)
            return;

        _teammatesAreEnemies = _teammatesAreEnemiesCvar?.GetPrimitiveValue<bool>() ?? false;
        var pawns = GetAlivePawns();

        foreach (var (index, tracked) in _nades.ToList())
        {
            var nade = tracked.Projectile;
            if (!nade.IsValid || nade.AbsOrigin is null || nade.AbsVelocity is null)
            {
                _nades.Remove(index);
                continue;
            }

            if (ProcessNade(tracked, pawns))
                _nades.Remove(index);
        }
    }
}
