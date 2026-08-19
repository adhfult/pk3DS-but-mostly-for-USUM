<p align="center">
  <img src="https://imgur.com/a/KHpKvqS" alt="pk3DS Logo"/>
</p>

<h1 align="center">pk3DS (But Extremely Catered Towards USUM)</h1>
<h2 align="center">(Yes, it works with the Expansion Mod!).</h2>

<br />

**pk3DS (But Extremely Catered Towards USUM)** is a customized fork of pk3DS designed to give Ultra Sun & Ultra Moon ROM hackers and randomizer fans the best possible experience they can have. It includes all the features from the base pk3DS, but adds many new features and improvements specifically for USUM.

---

## What's Possible With This Fork?

Here is a look at what you can do across the program:

### RomFS & Game Data Editors

Will be updated in detail later!

### Modern Mechanics & Patch Packages

Will be updated in detail later!

### 🎲 Enhanced Universal Randomizer

Will be updated in detail later!

### Research Center & CRO Expander 4.0

Will be updated in detail later!

### 🎨 Visual Themes & Modern UI

- **4 Custom Themes**: Switch effortlessly between Dark Mode, Gray, Classic Light, and Galaxy Purple.
- **Interactive Mascot Sprites**: Clickable companion sprites featuring various iconic legendaries in the sidebar.
- **Built-in Sprite Packs**: High-quality sprites for modern items, custom forms, and elemental badges.

---

## Installation & Usage

1. Download the latest release from the [Releases page](https://github.com/adhfult/pk3DS-but-mostly-for-USUM/releases) or the [Project Pokémon forum thread](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/).
2. Extract the folder onto your computer.
3. Dump your decrypted Pokémon Ultra Sun or Ultra Moon ROM (RomFS and ExeFS).
4. Run `pk3DS.WinForms.exe` and select **File → Open** to choose your game directory.

---

## Building from Source

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or the .NET CLI

### Build Steps

```bash
git clone https://github.com/adhfult/pk3DS-but-mostly-for-USUM.git
cd pk3DS-but-mostly-for-USUM
dotnet build pk3DS.sln --configuration Release
```

The output will be located in `pk3DS.WinForms/bin/Release/net8.0-windows/`.

---

## Support & Credits

If you run into any bugs or have feedback, feel free to open an issue or reach out through the community threads:

- **Kaphotics** — For creating pk3DS and providing the foundation for 3DS Pokémon hacking.
- **ABZB** — For extensive ARM assembly reverse engineering, CRO research, and function table documentation.
- **Stracker** — For the [OWSE Overworld Script Editor](https://github.com/Strackeror/pk3DS/releases/tag/stracker-1.0).
- **Smogon & Pokémon Showdown!** — For team formatting standards, sprite assets, etc.
- **3DS ROM Hacking Discord** - My guys, love you all for the support and I'm glad this tool can finally come out.
