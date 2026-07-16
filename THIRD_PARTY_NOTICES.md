# Third-party notices

## Launcher_V2

- Project: `yanygm/Launcher_V2`
- Source: <https://github.com/yanygm/Launcher_V2>
- Local base revision used during development: `f55fc87`
- License: Academic Free License 3.0

The server, packet, networking, RHO/PIN support and shared application structure
originate from or are modified from this AFL-3.0 project. The complete license
text is in `LICENSE.md`, and the prominent modification notice is in
`NOTICE.md`.

Launcher_V2 itself acknowledges `MyPuppy/Launcher.cn_3075`,
`xpoi5010/Kartrider-File-Reader` and `lkk9898969/kart_data_Transform` as earlier
sources or references. Those acknowledgements are preserved here without
asserting endorsement by their authors.

## Launcher_HF_5136

The public behavior of `yanygm/Launcher_HF_5136` was compared during
interoperability testing. It had no explicit license at the time this release
was prepared. This repository therefore redistributes none of its source files,
binaries, resources or Git history. Protocol behavior was implemented in the
separately authored P5136 compatibility layer described in `NOTICE.md`.

## .NET

The projects target .NET 8 and use framework libraries. Official packages are
framework-dependent and do not bundle the shared .NET runtime. The single-file
applications contain the normal .NET native application host. The publish
script copies the active SDK's `LICENSE.txt` and `ThirdPartyNotices.txt` into
the ZIP packages as `DOTNET-LICENSE.txt` and
`DOTNET-THIRD-PARTY-NOTICES.txt`.

The released applications have no third-party NuGet package dependency.
