# Nexus Ops — multiplayer (pre-UGS)

## Org admin checklist (Unity Dashboard)

1. Create / link a **Unity Gaming Services** project to this game.
2. Enable **Authentication** (Anonymous sign-in is enough to start).
3. Enable **Lobby** (room codes + room list).
4. Enable **Relay** (device-to-device without self-hosted servers).
5. Copy project credentials into the Editor (or use automatic linking).

Optional later: **Matchmaker** for “Find match” queues.

## Code map

| File | Role |
|------|------|
| `NexusSession.cs` | Local vs online, seat index, host flag |
| `NexusLobbyService.cs` | Menu flow stubs → replace with UGS Lobby API |
| `NexusGameCommands.cs` | End turn / moves → replace client RPCs |

## Player-hosted model

- No AWS required for v1.
- Room **host** device runs `GameController` authority.
- **Relay** carries traffic between phones.

## Testing without UGS (today)

- **Create room** → share stub code → **Simulate opponent** → **Start match** (host).
- **Join room** on another build with the same stub code (accepts any 4+ char code until UGS validates).
- Online 1v1 uses the standard **1v1** board; only your seat accepts input.
