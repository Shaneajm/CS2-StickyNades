using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace StickyNades;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StickyMode
{
    Everyone,
    PerPlayer,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatMessageMode
{
    All,
    Involved,
    Thrower,
    None,
}

public sealed class StickyNadesConfig : BasePluginConfig
{
    [JsonPropertyName("ConfigVersion")]
    public override int Version { get; set; } = 1;

    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("Mode")]
    public StickyMode Mode { get; set; } = StickyMode.Everyone;

    [JsonPropertyName("PerPlayerDefaultEnabled")]
    public bool PerPlayerDefaultEnabled { get; set; } = false;

    [JsonPropertyName("ToggleCommands")]
    public List<string> ToggleCommands { get; set; } = ["sticky", "stickynades"];

    [JsonPropertyName("TogglePermission")]
    public string TogglePermission { get; set; } = "";

    [JsonPropertyName("ChatMessages")]
    public ChatMessageMode ChatMessages { get; set; } = ChatMessageMode.All;

    [JsonPropertyName("StickToTeammates")]
    public bool StickToTeammates { get; set; } = true;
}
