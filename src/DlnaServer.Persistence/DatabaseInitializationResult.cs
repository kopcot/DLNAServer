namespace DlnaServer.Persistence
{
    /// <summary>
    /// Outcome of bringing the database into a usable state at startup.
    /// </summary>
    /// <param name="Outcome">What had to be done.</param>
    /// <param name="MigrationsApplied">How many migrations were applied.</param>
    /// <param name="CorruptFileBackupPath">
    /// Where the unusable database was moved, when <paramref name="Outcome"/> is
    /// <see cref="DatabaseInitializationOutcome.Recreated"/>. Null otherwise. The file is kept rather
    /// than deleted so the failure can still be investigated.
    /// </param>
    public sealed record DatabaseInitializationResult(
        DatabaseInitializationOutcome Outcome,
        int MigrationsApplied,
        string? CorruptFileBackupPath);
}
