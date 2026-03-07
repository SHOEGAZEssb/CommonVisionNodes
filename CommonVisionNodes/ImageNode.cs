using Stemmer.Cvb;

namespace CommonVisionNodes
{
    public sealed class ImageNode : Node
    {
        public Port ImageOutput { get; }

        public string FilePath { get; set; } = string.Empty;

        public ImageNode()
        {
            ImageOutput = AddOutput("Image", typeof(Image));
        }
    }
}
