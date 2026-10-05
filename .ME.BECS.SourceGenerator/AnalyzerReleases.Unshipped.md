; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BECSG100 | ME.BECS | Error | Invalid compiler type-input manifest
BECSG101 | ME.BECS | Warning | Stale graph injection plan blocks graph initialization but permits Editor reload
BECSG102 | ME.BECS | Warning | Compiler excludes stale dependencies of IViewIgnoreTracker owners; regenerate inputs after reload
BECSG103 | ME.BECS | Warning | Compiler classification replaces differing legacy component flags; regenerate inputs after reload
BECSG104 | ME.BECS | Warning | Missing Editor input manifest permits exporter reload without emitting bootstrap; graph freshness guards still block execution
BECSG105 | ME.BECS | Warning | Invalid consumer inputs permit Editor recovery without bootstrap or freshness metadata; Player compilation remains strict
BECSG106 | ME.BECS | Error | Invalid owner-local system publication blocks Player compilation
BECSG107 | ME.BECS | Warning | Invalid owner-local system publication permits Editor input recovery without publishing callbacks
BECSG108 | ME.BECS | Error | Invalid owner-local component/group publication blocks Player compilation
BECSG109 | ME.BECS | Warning | Invalid owner-local component/group publication permits Editor input recovery without publishing callbacks
BECSG110 | ME.BECS | Error | Invalid owner-local entity publication blocks Player compilation
BECSG111 | ME.BECS | Warning | Invalid owner-local entity publication permits Editor input recovery without publishing callbacks
BECSG112 | ME.BECS | Error | Invalid owner-local aspect publication blocks Player compilation
BECSG113 | ME.BECS | Warning | Invalid owner-local aspect publication permits Editor input recovery without publishing callbacks
BECSG114 | ME.BECS | Error | Invalid owner-local destroy publication blocks Player compilation
BECSG115 | ME.BECS | Warning | Invalid owner-local destroy publication permits Editor input recovery without publishing callbacks
BECSG116 | ME.BECS | Error | Invalid owner-local config publication blocks Player compilation
BECSG117 | ME.BECS | Warning | Invalid owner-local config publication permits Editor input recovery without publishing callbacks
BECSG118 | ME.BECS | Error | Invalid owner-local network publication blocks Player compilation
BECSG119 | ME.BECS | Warning | Invalid owner-local network publication permits Editor input recovery without publishing callbacks
BECSG120 | ME.BECS | Error | Invalid owner-local views publication blocks Player compilation
BECSG121 | ME.BECS | Warning | Invalid owner-local views publication permits Editor input recovery without publishing callbacks
BECSG122 | ME.BECS | Error | Invalid owner-local job initialization publication blocks Player compilation
BECSG123 | ME.BECS | Warning | Invalid owner-local job initialization publication permits Editor input recovery without publishing callbacks
BECSG124 | ME.BECS | Error | Invalid owner-local job statistics publication blocks Player compilation
BECSG125 | ME.BECS | Warning | Invalid owner-local job statistics publication permits Editor input recovery without publishing callbacks
BECSG126 | ME.BECS | Error | Invalid owner-local job debug publication blocks Player compilation
BECSG127 | ME.BECS | Warning | Invalid owner-local job debug publication permits Editor input recovery without publishing callbacks
BECSG128 | ME.BECS | Error | Invalid owner-local graph publication blocks Player compilation
BECSG129 | ME.BECS | Warning | Invalid owner-local graph publication permits Editor input recovery without publishing callbacks
