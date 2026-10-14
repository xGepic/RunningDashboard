# Builds the packaged app for a Windows PC without .NET or Node: publish/RunningDashboard.zip, holding
# RunningDashboard.exe and the built frontend next to it in wwwroot. Unzipped anywhere, the exe runs the whole
# dashboard and keeps its database and backups in that folder.
$ErrorActionPreference = 'Stop'

$publish = Join-Path $PSScriptRoot 'publish'
$app = Join-Path $publish 'RunningDashboard'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Push-Location (Join-Path $PSScriptRoot 'frontend')
try {
    npm.cmd install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'The frontend build failed.' }
}
finally { Pop-Location }

dotnet publish (Join-Path $PSScriptRoot 'backend/src/RunningDashboard.Api') `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:AssemblyName=RunningDashboard -p:DebugType=None `
    -o $app
if ($LASTEXITCODE -ne 0) { throw 'The backend publish failed.' }

# Only for development and IIS; the packaged app reads appsettings.json alone.
Remove-Item (Join-Path $app 'appsettings.Development.json'), (Join-Path $app 'web.config'), (Join-Path $app '*.staticwebassets.*.json') -ErrorAction SilentlyContinue

Copy-Item (Join-Path $PSScriptRoot 'frontend/dist/frontend/browser') (Join-Path $app 'wwwroot') -Recurse

Compress-Archive -Path $app -DestinationPath (Join-Path $publish 'RunningDashboard.zip')
Write-Host "Done: $(Join-Path $publish 'RunningDashboard.zip')"
