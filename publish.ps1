# Builds the packaged app for Windows computers without .NET or Node, into deploy/:
#   deploy/win  RunningDashboard.exe, zipped as deploy/RunningDashboard-win.zip
# It has the built frontend next to it in wwwroot. Unpacked anywhere, the app runs the whole dashboard and keeps its
# database and backups in that folder.
$ErrorActionPreference = 'Stop'

$deploy = Join-Path $PSScriptRoot 'deploy'
if (Test-Path $deploy) { Remove-Item $deploy -Recurse -Force }

Push-Location (Join-Path $PSScriptRoot 'frontend')
try {
    npm.cmd install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'The frontend build failed.' }
}
finally { Pop-Location }

function Publish-App([string] $folder, [string] $runtime) {
    $app = Join-Path $deploy $folder
    dotnet publish (Join-Path $PSScriptRoot 'backend/src/RunningDashboard.Api') `
        -c Release -r $runtime --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:AssemblyName=RunningDashboard -p:DebugType=None `
        -o $app | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "The backend publish for $runtime failed." }

    # Only for development and IIS; the packaged app reads appsettings.json alone.
    Remove-Item (Join-Path $app 'appsettings.Development.json'), (Join-Path $app 'web.config'), (Join-Path $app '*.staticwebassets.*.json') -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $PSScriptRoot 'frontend/dist/frontend/browser') (Join-Path $app 'wwwroot') -Recurse
    return $app
}

# The zip unpacks into a RunningDashboard folder.
$win = Publish-App 'win' 'win-x64'
$winZip = Join-Path $deploy 'RunningDashboard'
Rename-Item $win $winZip
Compress-Archive -Path $winZip -DestinationPath (Join-Path $deploy 'RunningDashboard-win.zip')
Rename-Item $winZip $win

Write-Host "Done: $(Join-Path $deploy 'RunningDashboard-win.zip')"
