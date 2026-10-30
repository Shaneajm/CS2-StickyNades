![GitHub Repo stars](https://img.shields.io/github/stars/Shaneajm/CS2-StickyNades?style=for-the-badge)
![GitHub all releases](https://img.shields.io/github/downloads/Shaneajm/CS2-StickyNades/total?style=for-the-badge)
![GitHub](https://img.shields.io/github/license/Shaneajm/CS2-StickyNades?style=for-the-badge)

<br />
<div align="center">
  <h1 align="center">StickyNades</h1>
  <p align="center">A <a href="https://docs.cssharp.dev">CounterStrikeSharp</a> plugin for CS2 that makes HE grenades sticky.</p>
</div>

## Features

- HE grenades stick to walls, floors, props and players.
- Optional chat announcements for stuck players.
- Allow per player or for the whole server.

## Dependencies

- [**CounterStrikeSharp**](https://github.com/roflmuffin/CounterStrikeSharp) v1.0.378 or newer.

## Installation

1. Install [CounterStrikeSharp](https://docs.cssharp.dev/docs/guides/getting-started.html) on your server.
2. Download or build the plugin zip.
3. Extract it into your server's `game/csgo/` folder.
4. Restart the server, or run `css_plugins load StickyNades`.
5. Edit the config file the plugin creates (see [Configuration](#configuration)).

## Configuration

The config file is created on first load at:

```
counterstrikesharp/configs/plugins/StickyNades/StickyNades.json
```

After editing it, run `css_plugins reload StickyNades` or change the map.

| Option                    | Description                                                                                                                                        | Default                     |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `Enabled`                 | Turns the whole plugin on or off.                                                                                                                  | `true`                      |
| `Mode`                    | `Everyone`: all HE grenades are sticky. `PerPlayer`: only players who turned it on with the toggle command.                                        | `"Everyone"`                |
| `PerPlayerDefaultEnabled` | In `PerPlayer` mode, whether players start with sticky grenades on.                                                                                | `false`                     |
| `ToggleCommands`          | Command names that toggle sticky grenades. `"sticky"` gives `!sticky`, `/sticky` and `css_sticky`.                                                 | `["sticky", "stickynades"]` |
| `TogglePermission`        | Permission needed to use the toggle command, such as `"@css/vip"`. Leave empty to let anyone use it.                                               | `""`                        |
| `ChatMessages`            | Who sees the stuck message: `All` (everyone), `Involved` (thrower and stuck player), `Thrower` (thrower only) or `None`.                            | `"All"`                     |
| `StickToTeammates`        | When `false`, grenades bounce off teammates as usual and stick to the next thing they hit. Has no effect when `mp_teammates_are_enemies` is on.     | `true`                      |

The default config:

```json
{
  "ConfigVersion": 1,
  "Enabled": true,
  "Mode": "Everyone",
  "PerPlayerDefaultEnabled": false,
  "ToggleCommands": ["sticky", "stickynades"],
  "TogglePermission": "",
  "ChatMessages": "All",
  "StickToTeammates": true
}
```

## Commands

| Command        | Description                                                                   |
| -------------- | ----------------------------------------------------------------------------- |
| `!sticky`      | Turns sticky grenades on or off for yourself. Only works in `PerPlayer` mode. |
| `!stickynades` | Same as `!sticky`.                                                            |

Change the command names with the `ToggleCommands` option. The setting resets when the player disconnects.

## Translations

Translation files are located at `counterstrikesharp/plugins/StickyNades/lang/`.

| Language | File      |
| -------- | --------- |
| English  | `en.json` |
| German   | `de.json` |

## Building

Requires the .NET 10 SDK.

```sh
cd src
dotnet build -c Release
```

## License

Distributed under the GPL-3.0 License. See [`LICENSE.md`](LICENSE.md) for more information.
