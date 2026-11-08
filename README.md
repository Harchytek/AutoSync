# AutoSync

AutoSync allows for the synchronization of configuration files from a previous profile.

<p align="left"><img src="https://imgur.com/zWEvNFT.png" alt="AutoSync" width="510"></p>

It automates the migration and synchronization of profile settings, configuration files `.cfg`, and external mods files `.dll` across different PC environments or mod manager profiles.

## Features

- **Flexible Sourcing :** Choose a specific profile or automatically fetch from the last created profile.
- **Dual-Config Architecture :** Separates modpack settings `Harchytek.AutoSync.cfg` from personal user tweaks `UsersCustom.cfg` to prevent overwriting during updates.
- **Smart DLL Protection :** Automatically skips existing `.dll` files in destination folders to prevent Windows file-locking errors.
- **Auto-Disable :** Automatically turns off `Enabled` after a successful sync to optimize future game startups.

### Example logs

<p align="left"><img src="https://imgur.com/9LCqu9D.png" alt="LogsAutoSync" width="510"></p>

## Installation
1. Install [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Place `AutoSync.dll` in your `BepInEx/plugins` folder.
3. Launch the game once to generate the config file.

## About myself

If you have any suggestions or need help, you can contact me on Github.

I make these mods for my friends and me, but they can be useful to everyone. It's my way of contributing to the Valheim community.

<p align="left"><a href="https://github.com/Harchytek/AutoSync/issues" target="_blank"><img src="https://cdn.simpleicons.org/github/C5C2FF" alt="Github" width="31" align="absmiddle"></a>&nbsp;<strong><a href="https://github.com/Harchytek?tab=repositories" target="_blank"> Harchytek</a></strong></p>
