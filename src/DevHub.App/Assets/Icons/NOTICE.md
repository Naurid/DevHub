# Third-party notice: JetBrains icons

The SVG files in this folder are copied unmodified from the IntelliJ Platform
("New UI" / expui icon set):

- Source: https://github.com/JetBrains/intellij-community, folder `platform/icons/src/expui/`
- Commit: `84ba9581b94ec2a2e7cf69ab51d3cbffe271fff5` (fetched 2026-10-09)
- License: Apache License 2.0. The full text is in `LICENSE-2.0.txt` next to this file.
  Each SVG keeps its original copyright header.

The upstream NOTICE file reads:

> This software includes code from IntelliJ IDEA
> Copyright (C) JetBrains s.r.o.
> https://www.jetbrains.com/idea/

The repository's root `LICENSE.txt` is JetBrains' "Open-Source Build Terms", which
cover their compiled IDE builds; the icon source files themselves are Apache 2.0.

## Files (upstream path → local name, each with a `_dark` variant)

| Upstream (`expui/…`) | Local |
|---|---|
| general/add | add |
| general/close | close |
| general/copy | copy |
| general/delete | delete |
| general/edit | edit |
| general/externalTools | externalTools |
| general/import | import |
| general/moreVertical | moreVertical |
| general/refresh | refresh |
| general/search | search |
| general/settings | settings |
| general/chevronDown | chevronDown |
| general/chevronRight | chevronRight |
| nodes/folder | folder |
| nodes/template | template |
| toolwindows/palette | palette |
| toolwindows/vcs | vcs |
| vcs/commit | commit |
| vcs/fetch | fetch |
| vcs/merge | merge |
| vcs/push | push |
| vcs/update | update |
| run/run | run |
| run/stop | stop |
| status/error | error |
| status/info | info |
| status/success | success |
| status/warning | warning |

When the app is published (roadmap M9), this file and `LICENSE-2.0.txt` must ship
alongside the executable.
