# Houseflow Smart Home

A Blazor WebAssembly app for a personal smart home dashboard and automation hub for Tuya-compatible devices. It runs in the browser and can be hosted as static files on GitHub Pages.

## Run locally

```powershell
dotnet run --project .\SmartHomeAutomations\SmartHomeAutomations.csproj
```

Open the local URL printed by `dotnet run`.

To create a static production build locally:

```powershell
dotnet publish .\SmartHomeAutomations\SmartHomeAutomations.csproj --configuration Release --output .\publish
```

The deployable site is in `publish/wwwroot`.

## Current starting point

- Responsive dashboard with device cards, routine controls, quick scenes, and home status.
- Interactive device switches, routine switches, scene selection, and a sample “new routine” action.
- Sample values are held in browser memory and reset on reload. No Tuya account or device connection is configured yet.
- `.github/workflows/pages.yml` builds the WebAssembly app and deploys it from the repository's default branch to GitHub Pages.

## Deploy to GitHub Pages

1. Push this repository to GitHub.
2. In the repository, open **Settings → Pages** and set the source to **GitHub Actions**.
3. Push to the default branch. The workflow builds and publishes the app; its run page shows the live URL.

For a public site that anyone can open, the repository must be public. Private-repository Pages access depends on your GitHub plan and repository settings.

## Next steps

1. Choose a Tuya connection path and region: Tuya IoT Cloud API, or a local bridge if devices are exposed locally.
2. Add a separate secure backend for Tuya credentials and API calls. Never put Tuya client secrets in this browser app; anything shipped to Pages is public to visitors.
3. Implement a Tuya client service, device and capability models, and a persistent store for rooms and automations.
4. Replace the in-memory dashboard data with live device state and action calls.
5. Add an automation editor for triggers, conditions, schedules, and device actions.
