namespace RunningDashboard.Api.Backup;

/// <summary>The backups as Settings shows them, and "Back up now".</summary>
public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/backup", (DatabaseBackup backup) => BackupResponse.From(backup));

        app.MapPost("/api/backup", (DatabaseBackup backup) =>
        {
            try
            {
                backup.BackUp();
            }
            catch (Exception)
            {
                return Results.Problem(backup.LastError, statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(BackupResponse.From(backup));
        });
    }
}
