namespace DlnaServer.Core.Hosting
{
    /// <summary>
    /// A number that moves whenever the indexed library may have changed, so a reader can tell whether a
    /// figure it computed earlier is still current without asking the database again.
    /// </summary>
    /// <remarks>
    /// A counter to compare, not an event to wait on: any number of readers can look at it, and none of
    /// them takes anything from the others - unlike <see cref="ILibraryScanSignal"/>, where a second
    /// waiter would steal the indexer's wake-up. Moving it more often than necessary only costs a
    /// reader one recount; failing to move it leaves a reader showing old figures, so writers err
    /// towards marking.
    /// </remarks>
    public interface ILibraryChangeSignal
    {
        /// <summary>
        /// Increases every time <see cref="MarkChanged"/> is called; never decreases.
        /// </summary>
        long Generation { get; }

        /// <summary>
        /// Records that files or folders may have been added, removed, moved or re-typed.
        /// </summary>
        void MarkChanged();
    }
}
