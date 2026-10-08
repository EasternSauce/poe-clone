# Minimal combat arena (Unity Editor only)

Public diagnostic URLs are disabled. Published builds ignore `minimal=1` and
all of its arena feature switches and start the normal game. The page also uses
normal update polling and startup hints regardless of those old parameters.
Rebuild and publish the web player for this change to take effect online.

The arena and its diagnostic switches are retained for future Editor debugging.
Start it from **PoeClone > Test > Minimal Combat Arena**, or through Unity MCP:

```csharp
return PoeClone.EditorTools.DevTest.QuickStartMinimal();
```

For mobile controls and a chosen diagnostic configuration:

```csharp
return PoeClone.EditorTools.DevTest.QuickStartMinimal(
    touch: true, options: "features=0&skills=1");
```

Other local options include `weapon=short_bow`, `enemy=Raider`, `drops=0`,
`quests=0`, `menus=0`, `inventory=1`, `character=1`, `passives=1`, `preview=0`,
`audio=0`, and `guaranteedGear=0`. With no options, all arena additions are enabled.
These options are Editor preferences configured by DevTest, not web URL features.

Follow the DevTest session instructions in AGENTS.md: verify the local session
server on port 8099 before starting, wait for QuickStatus, and call Ready again
before inspecting gameplay to ensure startup overlays are gone. Stop Play to
restore character preferences automatically.

The arena retains its empty 48 by 48 floor, four walls using the mobile-compatible
runtime material, and one enemy that respawns three seconds after death. No
character profile is loaded or saved by the arena. Gameplay checks are left to
the developer; only asset refresh, compilation and console inspection were run.
