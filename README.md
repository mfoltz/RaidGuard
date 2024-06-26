## Table of Contents

- [Features](#features)
- [Commands](#commands)
- [Configuration](#configuration)

## Features

- **Raid Guard:** Detects castle breach events and, by default, only permits the owning clan members and raiding clan members in the territory for the duration of the breach unharmed. Interlopers will be afflicted with unholy fire that damages and prevents healing.
- **Alliances:** Create and manage alliances with other players/clans configurably. Alliance members will be allowed unharmed in allied territories during raids and optionally will be unable to harm other alliance members.

## Commands

### Alliance Commands
- `.toggleAllianceInvites`
  - Toggle alliance invites.
  - Shortcut: *.ainvites*
- `.allianceAdd [Player/Clan]`
  - Adds player/clan to alliance.
  - Shortcut: *.aa [Player/Clan]*
- `.allianceRemove [Player/Clan]`
  - Removes player/clan from alliance.
  - Shortcut: *.ar [Player/Clan]*
- `.allianceDisband`
  - Disbands your alliance.
  - Shortcut: *.adisband*
- `.listAllianceMembers [Player/Clan]`
  - Lists alliance members of player entered or self if left blank.
  - Shortcut: *.lam [Player/Clan]*
- `.leaveAlliance`
  - Leaves alliance.
  - Shortcut: *.aleave*
 
## Configuration

### General
- **Raid Guard**: `RaidGuard` (bool, default: false)
  Enable or disable RaidGuard (see features).
- **Alliances**: `Alliances` (bool, default: false)
  Enable or disable Alliances (see features).
- **Clan Alliances**: `ClanAlliances` (bool, default: false)
  Enable or disable ClanAlliances. If enabled, only clan leaders will be able to manage alliances.
- **Prevent Friendly Fire**: `PreventFriendlyFire` (bool, default: false)
  Enable or disable friendly fire prevention between alliance members.
- **Max Alliance Size**: `MaxAllianceSize` (int, default: 4)
  Maximum members allowed in an alliance that are not part of the owning clan.





