namespace CommonVisionNodes
{
    public interface IInitializable : IDisposable
    {
        bool IsInitialized { get; }
        void Initialize();
    }
}
