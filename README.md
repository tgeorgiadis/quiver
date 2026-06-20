# Quiver

[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/tgeorgiadis/quiver)](https://github.com/tgeorgiadis/quiver/blob/main/LICENSE)

>## Important
>This software was previously knows as the "N64Recomp Launcher", but this had to be changed due to the continued requests by the N64Recomp Tool Developers. If you are interested in a version of the launcher that is primarily for N64 Recompiled Games, then check out [this version](https://github.com/SirDiabo/GithubLauncher/releases/tag/v1.70).

![Quiver Screenshot](Assets/LauncherScreenshot.png)
A modern, user-friendly launcher application for managing and running GitHub-hosted applications. This tool streamlines the process of downloading, installing, and launching your favorite projects.

## Features

- **Automated Updates**: Seamlessly download and install the latest releases from GitHub
- **Version Management**: Stay up-to-date with automatic version checking and updates
- **App Management**: Easy-to-use interface for launching GitHub-hosted apps
- **Smart Integration**: Direct integration with GitHub releases for smooth updates

## Getting Started

### Prerequisites

- .NET 9 Runtime (get it [here](https://dotnet.microsoft.com/en-us/))
- Internet connection for updates and downloads

### Installation

1. Download the latest release from the [Releases](https://github.com/tgeorgiadis/quiver/releases) page
2. Extract the downloaded archive to your preferred location
3. Run the executable.

## Usage

1. Launch the application
2. The launcher will automatically check for updates on startup
3. Browse your app library through the interface
4. Select an app and click "Download/Launch" to use it

## Local Development

When building and running from source, the launcher skips automatic self-update checks so a GitHub release does not overwrite your local build output.

- **Debug builds** (`dotnet run -c Debug`): startup self-update is always skipped.
- **Release builds** run locally: set `Quiver_SKIP_UPDATES=1` (or `true`) before launching.

Manual update checks from the in-app **Check for Updates** button still work in all configurations.

```powershell
# Debug — no env var needed
dotnet run --project Quiver.csproj -c Debug

# Release local testing
$env:Quiver_SKIP_UPDATES = "1"
dotnet run --project Quiver.csproj -c Release
```

If a previous run already downloaded a release into your output folder, delete `update_check.json` and any `backup_*` folders under `bin\Debug` or `bin\Release`, then rebuild.

### Automated tests

Run the xUnit test suite:

```powershell
dotnet test Quiver.sln
```

Release configuration matches CI:

```powershell
dotnet test Quiver.sln -c Release
```

Test categories include catalog merge and sync, settings store round-trip, launcher version helpers, Windows runner command building, download asset selection, game status checks, ViewModel sorting/catalog helpers, GameManager hide/filter behavior, and Avalonia headless smoke tests.

Collect coverage locally with:

```powershell
dotnet test Quiver.sln -c Release --collect:"XPlat Code Coverage"
```

## Configuration

### GitHub API Token
To avoid hitting GitHub's API rate limits, you can provide a personal access token.
Create a token with no special permissions needed and set it in the launcher settings.
You can create a token at ```GitHub Settings -> Developer settings > Personal access tokens > Tokens (classic) > Generate new token```
You don't need to give it any special permissions. Then paste that Token into your Settings field. Do not share your Token!

### apps.json Structure

The launcher uses an `apps.json` file to manage the available apps. Every entry is user-managed and editable; there are no built-in stable, experimental, or custom categories. Add, edit, or remove entries from the **Library** using **+ Add New Entry** or each app's **Catalog** context menu.

#### External catalog sources

You can add additional `apps.json` catalogs in **Settings → General → App Catalog Sources**. Each source can be a remote URL (for example a raw GitHub link) or a local file path. Multiple sources are merged into your library; entries in the local `apps.json` take priority when the same repository appears in more than one place. Removing a source prompts you to keep its exclusive apps (copied into your local list) or remove them from the library. Installed app files on disk are never deleted automatically.

#### Community catalog lists

The `community-app-lists/` folder contains a sample community index and starter list (`n64-recomp.json`). To browse community-maintained lists:

1. Set **Community index URL** in **Settings → General** to a hosted `index.json` URL or a local file path (for example `community-app-lists/index.json` in this repo).
2. Click **Browse Community Lists** and subscribe to any entry.

When a subscribed list changes remotely, the launcher detects the diff on startup or when you click **Refresh All Sources**, then prompts you to **Apply All**, **Apply New Only**, or **Keep Current**. The launcher stores an accepted snapshot per source so your library does not change until you confirm.

See [`community-app-lists/README.md`](community-app-lists/README.md) for how to publish and maintain community lists.

#### App Entry Properties

Each app entry requires the following properties:

- **`name`** - The display name of the app as it appears in the launcher
- **`repository`** - The GitHub repository in the format `username/repository`
- **`folderName`** - The folder name where the app will be downloaded and installed
- **`appIconUrl`** - URL of the app's icon image. If null, a default icon will be used.

#### Example Configuration

```json
{
    "apps": [
        {
            "name": "Example App",
            "repository": "username/example-app-repo",
            "folderName": "ExampleApp",
            "appIconUrl": null
        },
        {
            "name": "Another App",
            "repository": "anotheruser/another-app-repo",
            "folderName": "AnotherApp",
            "appIconUrl": "link/to/an/image.png"
        }
    ]
}
```

## Support

If you encounter any issues or have questions:
- [Open an issue](https://github.com/tgeorgiadis/quiver/issues)
- Check existing issues for solutions
