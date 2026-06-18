# Publishing Steps

This project is prepared for Paradox Mods as a new public code mod named `Real World Traffic Lights`.

Public source repository:
https://github.com/northernst11-bot/RealWorldTrafficLights

## First Upload

Build the mod:

```powershell
& 'C:\Users\stoot\OneDrive\Documents\New project\tools\dotnet-sdk\dotnet.exe' build 'C:\Users\stoot\OneDrive\Documents\New project\RealWorldTrafficLights_src\RealWorldTrafficLights\RealWorldTrafficLights.csproj' -c Release
```

Publish with the CS2 ModPublisher:

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
& 'C:\Users\stoot\OneDrive\Documents\New project\tools\dotnet-sdk\dotnet.exe' 'C:\Users\stoot\OneDrive\Documents\New project\Cities2_Data\Content\Game\.ModdingToolchain\ModPublisher\ModPublisher.dll' Publish 'C:\Users\stoot\OneDrive\Documents\New project\RealWorldTrafficLights_src\RealWorldTrafficLights\Properties\PublishConfiguration.xml' -c 'C:\Users\stoot\OneDrive\Documents\New project\RealWorldTrafficLights_src\RealWorldTrafficLights\publish\RealWorldTrafficLights' -v
```

After Paradox assigns a mod id, add:

```xml
<ModId Value="NEW_ID_HERE" />
```

to `Properties\PublishConfiguration.xml`, then use `NewVersion` for later file updates.

If `Publish` logs in successfully but returns `Forbidden access`, open Cities: Skylines II or Paradox Mods in the logged-in account and accept/refresh the creator publishing permissions, then rerun the same command.
