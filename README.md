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
- Live Tuya Cloud device discovery and switch controls through the companion API. Routines and quick scenes are still UI prototypes.
- Tuya credentials stay on the API host; the GitHub Pages site only calls the API.
- `.github/workflows/pages.yml` builds the WebAssembly app and deploys it from the repository's default branch to GitHub Pages.

## Deploy to GitHub Pages

1. Push this repository to GitHub.
2. In the repository, open **Settings → Pages** and set the source to **GitHub Actions**.
3. Push to the default branch. The workflow builds and publishes the app; its run page shows the live URL.

For a public site that anyone can open, the repository must be public. Private-repository Pages access depends on your GitHub plan and repository settings.

## Connect Tuya devices

The repository now includes `Houseflow.Api`, an ASP.NET Core API that signs Tuya Cloud requests, lists the linked account's devices, and sends supported on/off commands. GitHub Pages cannot host this server, so deploy the API to an HTTPS host that supports ASP.NET Core 9. A Render Blueprint is included in `render.yaml`; select **New → Blueprint** in Render and connect this repository.

1. In [Tuya IoT Platform](https://iot.tuya.com/), create a **Smart Home** cloud project in the data center where your Tuya account is registered. Subscribe to IoT Core and note the Access ID and Access Secret.
2. In the project, use **Devices → Link Tuya App Account** and scan its QR code with the Smart Life or Tuya Smart app. Copy the linked account UID from the project.
3. Set these private environment variables on the API host (never commit them):

   - `TUYA_ACCESS_ID`
   - `TUYA_ACCESS_SECRET`
   - `TUYA_UID`
   - `TUYA_BASE_URL` (for example `https://openapi.tuyaeu.com` for the EU data center; use the endpoint for your project's region)
   - `HOUSEFLOW_API_KEY` (a random secret of at least 32 characters; this protects device reads and commands)
   - `FRONTEND_ORIGINS` (the exact GitHub Pages origin, such as `https://YOUR-ACCOUNT.github.io`; comma-separated origins are supported)

4. Deploy the API project at `Houseflow.Api/Houseflow.Api.csproj` and confirm `https://YOUR-API-HOST/api/status` returns `{"configured":true}`. The API must be reachable over HTTPS from the browser.
5. Set `ApiBaseUrl` in `SmartHomeAutomations/appsettings.json` to the API root URL, including a trailing slash (for example `https://YOUR-API-HOST/`). Commit and push this public URL setting, then wait for the GitHub Pages workflow to publish the frontend.
6. Open Houseflow, choose **Connect devices**, enter the same `HOUSEFLOW_API_KEY` in the dialog, and select **Save for this tab & load**. The key is kept in that browser tab's session storage. Devices are paired in the Smart Life/Tuya Smart app and linked to the cloud project; this page then imports those linked devices. Switches are shown only when Tuya reports a writable boolean switch function.

The API currently implements device discovery and on/off commands. Tuya scenes, routines, room assignment, and other device controls are not wired to cloud commands yet.
