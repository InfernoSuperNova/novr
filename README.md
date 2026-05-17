# [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR

### A reworked version of [UUVR](https://github.com/Raicuparta/uuvr)  designed and optimized for Nuclear Option, fixing up the many options caused by UUVR. 



## Building

1. Install a .NET/MSBuild toolchain that can build SDK-style .NET Framework 4.8 projects.
    - **Windows:** Visual Studio 2022 or Build Tools for Visual Studio 2022 with the **.NET Framework 4.8 targeting pack** installed.
    - **Linux:** the .NET SDK plus Mono/MSBuild and .NET Framework reference assemblies. Distro package names vary, but you usually want packages such as `dotnet-sdk`, `mono`, `msbuild`/`mono-msbuild`, and `mono-reference-assemblies`/`.NET Framework 4.8 reference assemblies`.
2. Have Nuclear Option installed at:
    - **Linux:** `~/.steam/steam/steamapps/common/Nuclear Option` OR `~/.steam/debian-installation/steamapps/common/Nuclear Option` OR `~/.local/share/Steam/steamapps/common/Nuclear Option`
    - **Windows:** `C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option` OR `C:\Program Files\Steam\steamapps\common\Nuclear Option` OR `D:\SteamLibrary\steamapps\common\Nuclear Option`

    If none of these options work for you, add a path in `NuclearOption.props` in the project root or set the `NUCLEAR_OPTION_GAME_DIR` environment variable.
3. Ensure [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases/latest) is installed inside the Nuclear Option directory as per the installation instructions. *Do not use 6.x for now until further notice!*

    If you are on Linux and running under Proton, you will need extra setup to ensure BepInEx is loaded. See the [BepInEx running under Proton guide](https://docs.bepinex.dev/articles/advanced/proton_wine.html#:~:text=1%2E%20Open%20winecfg%20for%20the%20target%20game).
4. Build the `Release` configuration from your IDE of choice. JetBrains Rider is tested; Visual Studio should work too. To build from the command line, run this from the project root:

    ```bash
    dotnet build NuclearOptionVirtualRealityMod.sln -c Release
    ```

    The build output is written under `build-output`, as well as copied directly to the BepInEx directory.

## Installer development

The GUI installer is built with Avalonia. Building it in `Release` automatically publishes self-contained single-file executables for Linux and Windows into `dist/`:

```bash
dotnet build NOVR.Installer/NOVR.Installer.csproj -c Release
```

Outputs:

- `dist/NOVR.Installer-Linux`
- `dist/NOVR.Installer-Win.exe`

Building the full solution in `Release` also creates `dist/NOVR.zip`, ready to upload as the mod release asset.

## License

    Nuclear Option VR Mod
    Copyright (C) 2026 InfernoSuperNova (DeltaWing)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

# Original README below

> # Universal Unity VR
>
> [![Raicuparta's VR mods](https://raicuparta.com/img/badge.svg)](https://raicuparta.com)
>
> Use [Rai Pal](https://pal.raicuparta.com) to install this mod.
>
> ## License
>
>     Rai Pal
>     Copyright (C) 2024  Raicuparta
>
>     This program is free software: you can redistribute it and/or modify
>     it under the terms of the GNU General Public License as published by
>     the Free Software Foundation, either version 3 of the License, or
>     (at your option) any later version.
>
>     This program is distributed in the hope that it will be useful,
>     but WITHOUT ANY WARRANTY; without even the implied warranty of
>     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
>     GNU General Public License for more details.
>
>     You should have received a copy of the GNU General Public License
>     along with this program.  If not, see <https://www.gnu.org/licenses/>.
