namespace CommonVisionNodes
{
    /// <summary>
    /// A node that requires one-time initialization before execution
    /// and releases resources on disposal.
    /// </summary>
    public interface IInitializable : IDisposable
    {
        /// <summary>
        /// Whether this instance has been initialized.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Performs initialization (e.g. loading files, opening devices).
        /// </summary>
        void Initialize();
    }
}
