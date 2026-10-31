using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Net;
using System.Text;
using CounterStrikeSharp.API.Core.Translations;

namespace StickyNades;

public partial class StickyNades
{
    private sealed record CenterMessage(string Html, float ExpiresAt);

    private readonly Dictionary<int, CenterMessage> _centerMessages = new();

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

            if (Config.MessageLocation is MessageLocation.Chat or MessageLocation.Both)
                player.PrintToChat(Translate(player, key, args));

            if (Config.MessageLocation is MessageLocation.Center or MessageLocation.Both)
            {
                var htmlArgs = args.Select(a => (object)WebUtility.HtmlEncode(a.ToString() ?? "")).ToArray();
                ShowCenterMessage(player, ToCenterHtml(Localizer.ForPlayer(player, key, htmlArgs)));
            }
        }
    }

    private void ShowCenterMessage(CCSPlayerController player, string html)
    {
        player.PrintToCenterHtml(html);

        if (Config.CenterMessageDuration > 0)
            _centerMessages[player.Slot] = new CenterMessage(html, Server.CurrentTime + Config.CenterMessageDuration);
    }

    private void UpdateCenterMessages()
    {
        if (_centerMessages.Count == 0)
            return;

        var now = Server.CurrentTime;
        foreach (var (slot, message) in _centerMessages.ToList())
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (now >= message.ExpiresAt || player is not { IsValid: true })
            {
                _centerMessages.Remove(slot);
                continue;
            }

            player.PrintToCenterHtml(message.Html);
        }
    }

    private static readonly Dictionary<char, string?> CenterHtmlColors = new()
    {
        ['\x01'] = null,      // default / white
        ['\x02'] = "#FF0000", // dark red
        ['\x03'] = "#BA81F0", // light purple
        ['\x04'] = "#40FF40", // green
        ['\x05'] = "#BFFF90", // olive
        ['\x06'] = "#A2FF47", // lime
        ['\x07'] = "#FF4040", // red
        ['\x08'] = "#C5CAD0", // grey
        ['\x09'] = "#EDE47A", // yellow
        ['\x0A'] = "#B0C3D9", // silver / blue grey
        ['\x0B'] = "#5E98D9", // blue
        ['\x0C'] = "#4B69FF", // dark blue
        ['\x0E'] = "#D32CE6", // purple / magenta
        ['\x0F'] = "#EB4B4B", // light red
        ['\x10'] = "#E4AE39", // gold / orange
    };

    private static string ToCenterHtml(string message)
    {
        message = message.ReplaceColorTags().Trim();

        var html = new StringBuilder();
        var open = false;

        foreach (var c in message)
        {
            if (c >= ' ')
            {
                html.Append(c);
                continue;
            }

            if (open)
            {
                html.Append("</font>");
                open = false;
            }

            if (CenterHtmlColors.TryGetValue(c, out var color) && color is not null)
            {
                html.Append($"<font color='{color}'>");
                open = true;
            }
        }

        if (open)
            html.Append("</font>");

        return html.ToString();
    }

    private void Reply(CCSPlayerController player, string key) => player.PrintToChat(Translate(player, key));

    private string Translate(CCSPlayerController? player, string key, params object[] args)
    {
        var prefix = Localizer.ForPlayer(player, "stickynades.prefix");
        var message = Localizer.ForPlayer(player, key, args);

        return $" {prefix} {message}";
    }
}
