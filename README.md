# CS2 Physics Data Extractor

Extract physics collision data from Counter-Strike 2 maps (both official and workshop maps).

## What it does

This tool finds your CS2 maps and extracts their physics collision meshes as `.vphys` files. Useful for game development, analysis, or physics simulation projects.

## Requirements

- Windows PC
- Steam with Counter-Strike 2 installed
- .NET Framework/.NET Core

## Installation

1. Download or clone this repository
2. Install required packages:
   ```bash
   dotnet add package ValveResourceFormat
   dotnet add package SteamDatabase.ValvePak
   ```
3. Build and run:
   ```bash
   dotnet build
   dotnet run
   ```

## Usage

1. Run the program
2. Choose what to process:
   - All maps (official + workshop)
   - Only official maps  
   - Only workshop maps
3. Physics data saves to `vphys` folder as `{mapname}.vphys`

## How it works

- Automatically finds your Steam installation
- Scans CS2 map files (`.vpk` format)
- Extracts physics collision meshes from `world_physics` files
- Saves readable physics data for each map

## Output

Each map generates a `.vphys` file containing the collision mesh data in text format.

## License

MIT License - you can use this code however you want.

## Note

This tool only reads your locally installed CS2 files. It doesn't modify anything or connect to the internet.