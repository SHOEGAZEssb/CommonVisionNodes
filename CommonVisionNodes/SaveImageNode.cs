using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class SaveImageNode : Node
    {
        public Port ImageInput { get; }

        public string FilePath { get; set; } = string.Empty;

        public SaveImageNode()
        {
            ImageInput = AddInput("Image", typeof(Image));
        }
    }
}
