# Platform authentication setup (Game Center + Play Games)

Nexus Ops uses **Unity Gaming Services (UGS) Authentication** for player IDs. Game Center and Play Games prove identity; UGS still powers lobby, relay, and netcode.

**Hook-up point in code:** `NexusAppleGameCenterSignIn.cs`, `NexusGooglePlayGamesSignIn.cs`, called from `NexusPlatformSignIn.TrySignInAsync()` when you open **Multiplayer**.

---

## Shared (both platforms)

1. **Unity Dashboard** → your project → enable **Authentication**, **Lobby**, **Relay**.
2. **Unity Editor** → **Edit → Project Settings → Services** → link the cloud project.
3. **Player Settings** → set bundle IDs (must match dashboard + store consoles):
   - Android / iOS: `com.clankergames.nexus` (Player Settings → Application Identifier)
4. **Edit → Project Settings → Services → Authentication** → add ID providers (steps below).

---

## Apple Game Center (iOS)

### A. Apple Developer + App Store Connect

1. [Apple Developer](https://developer.apple.com) → **Identifiers** → App ID for your bundle.
2. Enable capability **Game Center** on that App ID.
3. **App Store Connect** → your app → **Game Center** → configure (leaderboards optional for auth-only).
4. On device: Settings → Game Center → signed into an Apple ID.

### B. Unity Dashboard / Editor

1. **Authentication** → ID providers → **Apple Game Center**.
2. Enter **Bundle ID** exactly matching iOS Player Settings: `com.clankergames.nexus`.

### C. Unity project — Apple Game Kit plugin

1. Install **Apple Game Kit** from [Apple Unity Plug-ins](https://github.com/apple/unityplugins) (GameKit package).
2. **iOS Player Settings** → enable **Game Center** capability (or add in exported Xcode project).
3. Add scripting define for iOS builds:
   - **Edit → Project Settings → Player → iOS → Scripting Define Symbols**
   - Add: `NEXUS_APPLE_GAMEKIT`
4. If you see `MissingMethodException` / stripped code: set **Managed Stripping Level** to **Minimal** for iOS, or add a link.xml preserving `Apple.GameKit`.

### D. Code (already in repo)

`NexusAppleGameCenterSignIn.cs` (when `NEXUS_APPLE_GAMEKIT` is defined):

1. `GKLocalPlayer.Authenticate()`
2. `FetchItems()` → signature, teamPlayerId, publicKeyURL, salt, timestamp
3. `AuthenticationService.Instance.SignInWithAppleGameCenterAsync(...)`

**Important:** FetchItems must be called shortly before UGS sign-in (timestamp valid ~10 minutes).

Docs: [Apple Game Center + UGS](https://docs.unity.com/en-us/authentication/platform-signin/apple-game-center)

---

## Google Play Games (Android)

### A. Google Play Console + Cloud

1. [Google Play Console](https://play.google.com/console) → create/link game → **Play Games Services** → configure.
2. [Google Cloud Console](https://console.cloud.google.com) → APIs → enable **Google Play Games Services** / related APIs for your project.
3. Create **OAuth 2.0 Client IDs**:
   - **Android** client (package name `com.clankergames.nexus` + SHA-1 from your keystore).
   - **Web application** client — this is what UGS Authentication needs (**Client ID + Secret**).
4. Link Play Console app to the Cloud project (Play Console → Setup → API access).

### B. Unity Dashboard / Editor

1. **Authentication** → ID providers → **Google Play Games** (or Google Play Services per Unity UI version).
2. Paste **Web App Client ID** and **Client Secret** from Cloud Console.

### C. Unity project — GPGS plugin

1. Import [Google Play Games plugin for Unity](https://github.com/playgameservices/play-games-plugin-for-unity) **v11.01+**.
2. Run **Window → Google Play Games → Setup** (or similar):
   - Link Android app ID from Play Console.
   - Set **Web Client ID** (same Web App client as UGS dashboard).
3. Open setup (GPGS hides **Window → Google Play Games** unless Android is the active build target):
   - **Nexus → Multiplayer → Switch Build Target to Android** (if needed)
   - **Nexus → Multiplayer → Google Play Games Setup...**
   - Or: **Window → Google Play Games → Setup → Android setup...** (visible when build target is Android)
4. Test on a **real device** with Play Store / Play Games app; emulators often fail auth.

### D. Code (already in repo)

`NexusGooglePlayGamesSignIn.cs` (after GPGS plugin is imported):

1. `PlayGamesPlatform.Activate()`
2. `Authenticate` → `RequestServerSideAccess` → one-time **auth code**
3. `AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode)`

Docs: [Google Play Games + UGS](https://docs.unity.com/en-us/authentication/platform-signin/google-play-games)

---

## iOS define (Apple only)

| Platform | Plugin | Scripting define |
|----------|--------|------------------|
| iOS | Apple Game Kit | `NEXUS_APPLE_GAMEKIT` |

Android GPGS does **not** need a scripting define once the plugin is under `Assets/GooglePlayGames`.

---

## Verify sign-in

1. Build to device (not Editor for platform auth).
2. Open **Multiplayer** — `NexusUgsAuth.EnsureReadyAsync()` runs platform sign-in.
3. Check log for `[UGS] UGS signed in with Game Center` or `Play Games`.
4. **Create Room** — if `UseLiveServices` is true (not stub), live room codes work.

---

## Optional: Editor testing without Game Center / GPGS

- Enable **Anonymous** in UGS Authentication dashboard.
- Editor already signs in anonymously via `NexusPlatformSignIn` (dev only).

---

## Troubleshooting

| Symptom | Likely fix |
|---------|------------|
| Stub rooms only on device | Missing plugin, missing define, or dashboard provider not saved |
| Game Center “not authenticated” | Device not signed into Game Center; capability not enabled |
| Play Games auth code empty | Wrong SHA-1 on Android OAuth client; Web client ID mismatch in GPGS setup |
| `SessionNotFound` / rate limit | Lobby/Relay not enabled; wait and retry |
| iOS strip / MissingMethodException | Minimal stripping or link.xml for `Apple.GameKit` |
