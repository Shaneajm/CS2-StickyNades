using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;

namespace StickyNades;

public partial class StickyNades
{
    private void AnnounceStick(CCSPlayerController? thrower, CCSPlayerController? victim)
    {
        if (Config.ChatMessages == ChatMessageMode.None || victim is null || !victim.IsValid)
            return;

        var throwerName = thrower is { IsValid: true } ? thrower.PlayerName : "?";
        var victimName = victim.PlayerName;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot)
                continue;

            var isThrower = thrower is not null && player.Slot == thrower.Slot;
            var isVictim = player.Slot == victim.Slot;

            var key = isThrower ? "stickynades.stuck.thrower"
                : isVictim ? "stickynades.stuck.victim"
                : "stickynades.stuck.all";

            var shouldSee = Config.ChatMessages switch
            {
                ChatMessageMode.All => true,
                ChatMessageMode.Involved => isThrower || isVictim,
                ChatMessageMode.Thrower => isThrower,
                _ => false,
            };

            if (!shouldSee)
                continue;

            object[] args = key switch
            {
                "stickynades.stuck.thrower" => [victimName],
                "stickynades.stuck.victim" => [throwerName],
                _ => [throwerName, victimName],
            };

            player.PrintToChat(Translate(player, key, args));
        }
    }

    private void Reply(CCSPlayerController player, string key) => player.PrintToChat(Translate(player, key));

    private string Translate(CCSPlayerController? player, string key, params object[] args)
    {
        var prefix = Localizer.ForPlayer(player, "stickynades.prefix");
        var message = Localizer.ForPlayer(player, key, args);

        return $" {prefix} {message}";
    }
}
