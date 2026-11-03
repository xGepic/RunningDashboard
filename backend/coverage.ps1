# Runs the backend tests with code coverage and builds an HTML report into backend/coverage (open index.html), with a
# summary in the terminal and the results as Cobertura.xml, which the Tests workflow sums up on GitHub. The EF
# migrations are generated code and left out.
# Release, as a running backend locks bin/Debug.
$ErrorActionPreference = 'Stop'

$results = Join-Path $PSScriptRoot 'tests/RunningDashboard.Api.Tests/TestResults'
$report = Join-Path $PSScriptRoot 'coverage'
# Only this run's results, or the report would add up earlier runs.
foreach ($folder in $results, $report) {
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
}

Push-Location $PSScriptRoot
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet test -c Release --collect:'XPlat Code Coverage' --results-directory $results `
        -- 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=**/Migrations/*.cs'
    if ($LASTEXITCODE -ne 0) { throw 'The tests failed.' }

    dotnet reportgenerator "-reports:$results/*/coverage.cobertura.xml" "-targetdir:$report" '-reporttypes:Html;TextSummary;Cobertura' |
        Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The coverage report could not be built.' }

    Get-Content (Join-Path $report 'Summary.txt') -TotalCount 20
    Write-Host "`nReport: $(Join-Path $report 'index.html')"
}
finally { Pop-Location }
