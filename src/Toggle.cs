using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace StickyNades;

public partial class StickyNades
{
    private readonly Dictionary<int, bool> _playerEnabled = new();

    private void RegisterToggle()
    {
        foreach (var command in Config.ToggleCommands)
            AddCommand($"css_{command}", "Toggle sticky HE grenades for yourself", OnToggleCommand);

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } player)
                _playerEnabled.Remove(player.Slot);
            return HookResult.Continue;
        });
    }

    private bool IsStickyFor(CCSPlayerController? player)
    {
        if (Config.Mode == StickyMode.Everyone)
            return true;

        if (player is null || !player.IsValid)
            return Config.PerPlayerDefaultEnabled;

        return _playerEnabled.TryGetValue(player.Slot, out var enabled) ? enabled : Config.PerPlayerDefaultEnabled;
    }

    private void OnToggleCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player is null || !player.IsValid)
        {
            info.ReplyToCommand(Translate(null, "stickynades.command.client-only"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(Config.TogglePermission)
            && !AdminManager.PlayerHasPermissions(player, Config.TogglePermission))
        {
            Reply(player, "stickynades.command.no-permission");
            return;
        }

        if (!Config.Enabled)
        {
            Reply(player, "stickynades.toggle.disabled");
            return;
        }

        if (Config.Mode == StickyMode.Everyone)
        {
            Reply(player, "stickynades.toggle.everyone");
            return;
        }

        var enabled = !IsStickyFor(player);
        _playerEnabled[player.Slot] = enabled;
        Reply(player, enabled ? "stickynades.toggle.on" : "stickynades.toggle.off");
    }
}
