Do you find yourself manually creating or extending LACP groups whenever you add parallel network connections? AutoLACP handles it for you. Connect two or more routes between the same devices and the matching native LACP group is created or extended automatically.

[h2]Features[/h2]
[list]
[*]Automatically creates a native LACP group when two or more complete routes connect the same devices.
[*]Automatically extends an existing LACP group when another parallel route is added.
[*]Processes new connections and reconciles registered connections when a save is loaded.
[*]Supports direct connections, patch-panel routes, chained patch panels, and modular SFP connections.
[*]Uses Data Center's native LACP, save, and cable-removal behavior.
[*]Replaces manual LACP actions with unobtrusive automatic-status controls.
[/list]
[i]In short: connect your devices and let AutoLACP manage the LACP groups.[/i]

[h2]Questions, bugs and ideas[/h2]
Need support, found a bug, or have an idea? Start a new topic in the Steam Workshop Discussions:
[url=https://steamcommunity.com/sharedfiles/filedetails/discussions/3787887235]AutoLACP - Steam Workshop Discussions[/url]

Use one of these prefixes in the topic title: [b][Support] [Bug] [Idea][/b]

For problems, please include:
[list]
[*]MelonLoader/Latest.log
[*]The two devices involved
[*]The number and types of direct or patch-panel routes between them
[*]The current and expected LACP group membership
[*]A screenshot of the relevant network topology and LACP screen
[*]Whether the connections were newly created or loaded from a save
[/list]

You can also use GitHub Issues if you prefer:
[url=https://github.com/Gamareth/DataCenter-AutoLACP/issues]AutoLACP - GitHub Issues[/url]

[h3]Source code and releases[/h3]
[url=https://github.com/Gamareth/DataCenter-AutoLACP]GitHub - Data Center AutoLACP[/url]
[url=https://github.com/Gamareth/DataCenter-AutoLACP/releases]Download the latest GitHub release[/url]

[h2]Support the developer[/h2]
AutoLACP is free and open source. Want to help?
[list]
[*][b]Give the mod a Thumbs Up[/b] - This helps more players discover it and join the fun.
[*][url=https://ko-fi.com/gamareth]Support Gamareth on Ko-fi[/url] - Help support future development.
[/list]

[h2]Installation and updates[/h2]
[b]MelonLoader is required.[/b] AutoLACP currently supports single-player. Multiplayer and co-op are untested and unsupported. Custom VLAN restrictions are not tested or officially supported in version 1.0.0.

[h3]First installation[/h3]
[list=1]
[*]Install MelonLoader, start Data Center once, and then close the game.
[*]Subscribe to AutoLACP and copy:
<Steam Library>/steamapps/workshop/content/4170200/3787887235/AutoLACP.dll
to:
<Steam Library>/steamapps/common/Data Center/Mods
[*]Start Data Center and confirm that AutoLACP initialized in the MelonLoader console.
[/list]

Copy only [b]AutoLACP.dll[/b] into the Mods directory. Do not copy files into Plugins.

[h3]Automatic updates[/h3]
After the first manual installation, subscribed users receive automatic Workshop updates. Updates are verified while Data Center is running, installed after the game closes, and become active on the next launch.

Users who install the mod from GitHub without subscribing to the Workshop must update manually.
