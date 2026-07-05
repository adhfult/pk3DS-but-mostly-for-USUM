<p align="center">
  <img src="https://i.imgur.com/eB3U8q6.png" alt="pk3DS Logo"/>
</p>

<h1 align="center">pk3DS (But Extremely Catered Towards USUM) </h1>

<br />

pk3DS is a ROM editor for all 3DS Pokémon games that utilizes a variety of tools developed by a large group of contributors. This is a fork of pk3DS that is extremely catered towards USUM. It has a lot of features that the original pk3DS does not have to support and foster a better environment for hacking the games. Full credits to the original developers, and to ABZB for his ARM research.

## Table of contents

- [Features Overview](#features-overview)
- [Detailed Tool & Button Guide](#detailed-tool--button-guide)
  - [Main UI & Themes](#main-ui--themes)
  - [RomFS Editors](#romfs-editors)
  - [ExeFS & CRO Editors](#exefs--cro-editors)
- [Installation](#installation)
- [Usage](#usage)
- [Support & Credits](#support--credits)

## Features Overview

Our editor features a vast variety of randomizers and editors to make every run as unique as possible. The tools currently available are:

- Trainer Battles (Pokemon / Items / Moves / Abilities / Difficulty / Classes)
- Wild Encounters (Species, Level, Gen/Legend Specific, ORAS DexNav won't crash!)
- Personal Data (Pokemon Types / Stats / Abilities / TM Learnset)
- Move Randomizer (Type / Damage Category)
- Move Learnset (Level Up / Egg Move)
- Evolutions & Mega Evolutions
- TM Moves, Special Mart Inventory, Move Tutors
- Game Text and Story Text editing

---

## Detailed Tool & Button Guide

This section breaks down the function of **every single button and program** available within this custom pk3DS build.

### Main UI & Themes

- **Visual Mode Dropdown (Options Menu)**: Allows you to switch the application's aesthetic between Dark, Grey, Light, and Galaxy Purple modes on the fly.
- **Mascot Sprites**: The sidebar includes interactive mascot sprites (XY, ORAS, SM, USUM, Zygarde, Rayquaza). Clicking on the mascot will change it!

### RomFS Editors

#### Game Text and Story Text

- **Add Line / Remove Line**: Supports adding or removing new lines (`\n`) for custom text formatting. Use the `Shift` key while in the text visualizer to instantly insert a new line.
- **New Move Handler**: Automatically adjusts the respective text files to make room for entirely new moves.
- **Find Next / Find Before / Replace All**: Standard search utilities. *Replace All* is heavily recommended when renaming existing moves.

#### Personal Stats Editor

- **Copy Moves / Paste Moves**: Copies/Pastes TM/Tutor compatibilities from one Pokémon to another.
- **Copy Set / Paste Set**: Copies all base stats, typings, and basic personal data from one Pokémon to another (extremely useful when adding new custom forms).
- **Set Catch / Set Hatch**: Instantly sets the catch rate to 255 (always catch) or hatch cycle to 0 (instant hatch).
- **Jump to Level Up / Jump to Egg Moves**: Shortcuts to instantly open the learnset editors for the current Pokémon.
- **Form Insertion Tool**: Advanced tool to inject new alternate forms for existing Pokémon.
- **Stat Visualizer**: When a vanilla USUM file is loaded, this generates a `.txt` baseline to visually compare your custom stat changes against vanilla stats.

#### Level Up & Egg Moves

- **Copy / Paste**: Move learnsets can be copied and pasted across different Pokémon.
- **Add Move / Remove Move**: Granular control over the learnset lists.
- **Import TSV / Apply Modern Sets**: Allows bulk-importing modern Generation 8/9 movepools via TSV files (compatible with the Multiversal Movepool spreadsheet).

#### Wild Encounters

- **Import TSV / Export TSV**: Mass edit wild encounter tables in Google Sheets or Excel by exporting and importing tab-separated values.
- **Version Exclusives**: A toggle tab that displays which Pokémon are missing from the current game version.
- **Fill SOS**: Automatically fills all SOS call slots with the Pokémon in the primary encounter slot.

#### Mega Evolutions

- **Alternate Forms Toggle**: Allows you to configure new alternate forms as Mega Evolutions or primal reversions.

#### Trainer Editor

- **Master / Master All**: Automatically assigns the highest level AI (Master) to a specific trainer or all trainers. Ideal for difficulty hacks.
- **Import / Export / Import Team**: Fully compatible with Pokémon Showdown! sets. Paste a Showdown team directly to overwrite a trainer's roster.
- **Max IVs All / Doubles All / PokeChange All**: Global difficulty modifiers to make all trainers have max IVs, force Double Battles, or randomize their Pokémon.
- **Showdown Set Storage**: A built-in repository to save and store commonly used Showdown sets within the program for easy access later.

#### Items Editor

- **Import / Export .txt**: Mass edit item data externally.
- **Shift to newline**: Pressing `Shift` in the item description box instantly adds a `\n` to simplify text formatting.

#### Move Editor

- **Add New Moveslot**: Creates a completely new move index (Must be used alongside the *New Move Handler* in Game Text).
- **Pokemon Champions PP**: Standardizes the PP of moves to match Pokémon Champions conventions.
- **Sync Animations / BSEQs**: Advanced tool to tie move animations. (Note: Newly added moves default to the *Pound* animation).
- **Load Vanilla Baseline / Changes Log**: Tracks your custom move modifications against a vanilla GARC baseline.
- **Shift to newline**: Pressing `Shift` in the move description box instantly adds a `\n`.

#### Battle Royale / Battle Tree

- **Showdown Import / Export / Import Box**: Bulk import Showdown sets for Battle Tree NPCs.
- **Dump / Import PKMs**: Edit NPC Pokémon externally via .txt files.
- **Set List**: Assign specific Pokémon indices to specific Battle Tree trainers.

#### OWSE (Overworld Script Editor)

- Utilizes Stracker's OWSE to edit overworld item locations, scripts, and interactables.

#### TMs Editor

- **Update Description**: Automatically updates the TM's text description to match the newly assigned move.
- **Export / Import .txt**: Mass edit TMs.
- **128 TM Support**: The editor can natively edit expanded 128 TM lists (Requires the 128 TM game patch to be applied first).

#### Type Chart

- Interactive visual grid with sprites to adjust type effectiveness (0x, 0.5x, 1x, 2x).

### ExeFS & CRO Editors

#### Poke Mart & Move Tutor (Shop.cro)

- **Add / Delete**: Allows adding or removing items from Marts and moves from Tutors. *(Note: Requires corresponding manual .cro structure adjustments if expanding limits).*
- **Tutor Bug Fix**: The legacy pk3DS bug where replacing a tutor move failed to work has been completely resolved.

#### CRO Expander 4.0

- **Advanced CRO Modification**: Integrates CRO Expander 4.0 natively. Allows for expanding `.data`, `.bss`, and `.code` segments inside CRO files (like `Shop.cro` and `Bag.cro`) to add entirely new assembly logic, tables, and pointers.

#### Research Center

- **Dynamic Hex / Assembly IDE**: An advanced work-in-progress tool for analyzing `.cro` and `.bin` files, testing ARM assembly via Keystone/Capstone, and dynamically applying assembly patches for engine modifications.

---

## Installation

To download pk3DS, all you need to do is go into our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and following the instructions there.

## Usage

To begin using pk3DS you must first download the pk3DS editor zip file. Once you've downloaded the zip file for the editor, dump your ROM from the 3DS Pokémon game of your choosing. Place the files in the same folder then simply run the pk3DS.exe file.

## Support & Credits

If any bugs or errors are caught or experienced come to our [forum page](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/) and communicate with us.

- **Kaphotics** for pk3DS
- **ABZB** for ARM research, along with massive helpfulness related to editing .cro files
- **[InfinityPlus05](https://www.pokecommunity.com/threads/averaged-movesets-for-pokemon-across-generations.530142/)** for the Multiversal Movepool
- **Stracker** for his [OWSE editor](https://github.com/Strackeror/pk3DS/releases/tag/stracker-1.0)
