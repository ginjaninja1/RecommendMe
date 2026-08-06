# EmbyTemplatev2

A standardized, automation-ready repository template for rapidly scaffolding Emby Server plugins. 

## Features
* **Zero-Configuration Scaffolding**: Uses automated GitHub Actions to rename namespaces, solution files, and projects instantly upon repository creation.
* **.gitignore prepopulated**: To ensure obj, bin and .vs folders are excluded from repository
* **setup.bat**: instantiates a pre-commit in `.git/hooks/` so any commit with "[bump]" at the END of description increases the the version number in .csproj.
* **Working Plugin with thumbnail**: Ready to compile plugin with thumbnail, pluginui configuration page with autopostback (autosave) and task.
* **launchSettings.json**: Ready to launch Emby with breakpoints for debugging
* **Post Build Event**: Ready to copy compiled code to Emby plugins folder in current users %appdata%.
* **Supress dependency file**: No value in copying this into plugins folder.

## How to Instantiate a New Plugin

This template is completely automated via the cloud. You do not need to use `dotnet new` or run local renaming commands.

1. Click the green **Use this template** button at the top of this GitHub page.
2. Select **Create a new repository**.
3. Name your repository using your new plugin's name (e.g., `MyNewPlugin`).
4. Click **Create repository**.

### What happens in the background:
GitHub will instantly spin up a cloud action, read your repository name, and automatically update your folder paths, `.csproj`/`.slnx` filenames, and C# namespaces to match perfectly.

5. Open **GitHub Desktop** and clone your brand new repository down to your computer.
6. Run \repositoryroot\setup.bat to instantiate the [bump] pre-commit hook.
7. Launch the solution file inside `src/` and start coding immediately!