# Nexus Ops — multiplayer (UGS)

## Unity Dashboard checklist

1. Create / link a **Unity Gaming Services** project to this game (`com.clankergames.nexus`).
2. In **Edit → Project Settings → Services**, link the cloud project.
3. Enable **Authentication** + ID providers (see **[AUTH_SETUP.md](AUTH_SETUP.md)** for Game Center / Play Games).
4. Enable **Lobby** and **Relay** (via **Multiplayer Services**).

Optional later: **Matchmaker** queues for ranked “Find match”.

## Platform sign-in (production)

Full step-by-step (Apple, Google, Unity dashboard, plugins, defines): **[AUTH_SETUP.md](AUTH_SETUP.md)**

| File | Role |
|------|------|
| `NexusPlatformSignIn.cs` | Routes to platform sign-in |
| `NexusAppleGameCenterSignIn.cs` | Game Kit → `SignInWithAppleGameCenterAsync` (needs `NEXUS_APPLE_GAMEKIT`) |
| `NexusGooglePlayGamesSignIn.cs` | GPGS → `SignInWithGooglePlayGamesAsync` (needs `NEXUS_GOOGLE_PLAY_GAMES`) |

**Editor:** anonymous sign-in for dev. **Devices:** Game Center / Play Games after plugins + defines are added.

## Packages (manifest)

| Package | Role |
|---------|------|
| `com.unity.services.multiplayer` | Sessions (lobby codes), Relay at match start |
| `com.unity.netcode.gameobjects` | Host/client RPCs over Relay |

## Code map

| File | Role |
|------|------|
| `NexusUgsAuth.cs` | UGS initialize + delegates to platform sign-in |
| `NexusPlatformSignIn.cs` | Routes to Game Center / Play Games / Editor anonymous |
| `NexusAppleGameCenterSignIn.cs` | iOS Game Kit integration |
| `NexusGooglePlayGamesSignIn.cs` | Android GPGS integration |
| `NexusUgsRunner.cs` | Runs async UGS tasks off the IMGUI menu thread |
| `NexusLobbyService.cs` | Create / join / find session; Relay when host starts match |
| `NexusNetworkSetup.cs` | Creates `NetworkManager` + `UnityTransport` for MPS + NGO |
| `NexusOnlineBridge.cs` | `BeginMatchClientRpc`, `RequestEndTurnServerRpc` |
| `NexusSession.cs` | Local seat, host flag, online vs hotseat |
| `NexusGameCommands.cs` | End turn → host RPC when online client |

## Flow

1. **Main menu → Multiplayer** — initializes UGS; signs in on first room action.
2. **Create room** — `MultiplayerService.CreateSessionAsync` (lobby only; Relay deferred).
3. **Join room** — `JoinSessionByCodeAsync`.
4. **Find match** — `MatchmakeSessionAsync` (quick join).
5. **Start match (host)** — `StartRelayNetworkAsync`, spawn bridge, client enters via RPC.
6. **End turn (client)** — `RequestEndTurnServerRpc` → host runs `GameController.EndTurn()`.

If sign-in or UGS init fails, the menu falls back to **stub rooms** with dev simulate buttons.

## Player-hosted model

- Host device runs simulation authority for end-turn (first RPC wired).
- **Relay** carries NGO traffic between phones (no dedicated game server).
- Full move/battle state sync is the next wiring phase.

## Testing

### Editor + dashboard linked

1. Link UGS project → anonymous dev sign-in works automatically.
2. Create / join room with two Editor or Editor + device builds.

### Device (before platform plugins)

- Stub rooms only until `NexusPlatformSignIn` is completed.
- Multiplayer menu explains which provider is expected.

### Device (after Game Center / Play Games wired)

1. Player signs in with platform account on launch or first multiplayer tap.
2. Create / join / start match as above.
