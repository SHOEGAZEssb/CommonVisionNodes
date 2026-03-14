using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Stemmer.Cvb;
using Stemmer.Cvb.Foundation;

namespace CommonVisionNodes;

/// <summary>
/// Allows custom C# code to process an image at runtime.
/// </summary>
public sealed class CSharpNode : Node
{
    private Func<Image, Image>? _compiledFunction;
    private string _lastCompiledCode = string.Empty;
    private string? _lastCompilationError;

    /// <summary>
    /// Input port that receives the source image.
    /// </summary>
    public Port ImageInput { get; }

    /// <summary>
    /// Output port that provides the processed image.
    /// </summary>
    public Port ImageOutput { get; }

    /// <summary>
    /// The C# code to execute. Should contain a method body that processes 'inputImage' and returns an Image.
    /// </summary>
    public string Code { get; set; } = @"// Process the input image and return the result
// Available: inputImage (Stemmer.Cvb.Image)
// You can use Stemmer.Cvb.Foundation methods

// Example: Apply a Gaussian filter
var filtered = Filter.Gauss(inputImage, FixedFilterSize.Kernel3x3);
return filtered;

// Other examples:
// return Filter.Sobel(inputImage, FilterOrientation.Horizontal, FixedFilterSize.Kernel3x3);
// return Filter.HighPass(inputImage, FixedFilterSize.Kernel5x5);
// return inputImage;";

    /// <summary>
    /// Gets the last compilation error, if any.
    /// </summary>
    public string? LastCompilationError => _lastCompilationError;

    /// <summary>
    /// Gets whether the code compiled successfully.
    /// </summary>
    public bool IsCompiled => _compiledFunction != null && _lastCompiledCode == Code;

    public CSharpNode()
    {
        ImageInput = AddInput("Image", typeof(Image), "The source image to process.");
        ImageOutput = AddOutput("Image", typeof(Image), "The processed image.");
    }

    /// <inheritdoc/>
    public override void Execute()
    {
        var inputImage = ImageInput.Value as Image;

        if (inputImage == null)
        {
            ImageOutput.Value = null;
            return;
        }

        // Compile if needed
        if (_compiledFunction == null || _lastCompiledCode != Code)
        {
            if (!TryCompile())
            {
                ImageOutput.Value = null;
                return;
            }
        }

        try
        {
            ImageOutput.Value = _compiledFunction!(inputImage);
        }
        catch (Exception ex)
        {
            _lastCompilationError = $"Runtime error: {ex.Message}";
            ImageOutput.Value = null;
        }
    }

    private bool TryCompile()
    {
        try
        {
            // Wrap user code in a method
            var wrappedCode = $@"
using System;
using Stemmer.Cvb;
using Stemmer.Cvb.Foundation;

public class UserCode
{{
    public static Image Process(Image inputImage)
    {{
        {Code}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            // Get references - include all currently loaded assemblies
            var references = new List<MetadataReference>();

            // Add all loaded assemblies that have a location
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(asm.Location));
                    }
                    catch
                    {
                        // Skip assemblies that can't be loaded as metadata
                    }
                }
            }

            // Ensure CVB assemblies are included (in case they're not loaded yet)
            var cvbImageAssembly = typeof(Image).Assembly;
            var cvbFoundationAssembly = typeof(Filter).Assembly;

            if (!references.Any(r => r.Display?.Contains(cvbImageAssembly.GetName().Name ?? "") == true))
                references.Add(MetadataReference.CreateFromFile(cvbImageAssembly.Location));

            if (!references.Any(r => r.Display?.Contains(cvbFoundationAssembly.GetName().Name ?? "") == true))
                references.Add(MetadataReference.CreateFromFile(cvbFoundationAssembly.Location));

            var compilation = CSharpCompilation.Create(
                "UserCodeAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture))
                    .ToList();

                _lastCompilationError = string.Join("\n", errors);
                _compiledFunction = null;
                return false;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var type = assembly.GetType("UserCode");
            var method = type?.GetMethod("Process", BindingFlags.Public | BindingFlags.Static);

            if (method == null)
            {
                _lastCompilationError = "Could not find Process method.";
                _compiledFunction = null;
                return false;
            }

            _compiledFunction = (Image input) => (Image)method.Invoke(null, new object[] { input })!;
            _lastCompiledCode = Code;
            _lastCompilationError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastCompilationError = $"Compilation error: {ex.Message}";
            _compiledFunction = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> RequiredUsings => ["Stemmer.Cvb.Foundation"];

    /// <inheritdoc/>
    public override void EmitCode(CodeEmitContext ctx)
    {
        var varName = ctx.GetUniqueVariable("csharp");
        var inputVar = ctx.ResolveInput(ImageInput);

        ctx.Builder.AppendLine($"// CSharp Node: Custom code");
        if (!string.IsNullOrWhiteSpace(inputVar))
        {
            ctx.Builder.AppendLine($"var {varName} = ProcessCustomCode({inputVar});");
        }
        else
        {
            ctx.Builder.AppendLine($"Image? {varName} = null;");
        }

        ctx.RegisterOutput(ImageOutput, varName);
    }

    /// <inheritdoc/>
    public override void EmitHelperMethods(StringBuilder sb)
    {
        sb.AppendLine("static Image ProcessCustomCode(Image inputImage)");
        sb.AppendLine("{");
        foreach (var line in Code.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//"))
                sb.AppendLine($"    {line}");
        }
        sb.AppendLine("}");
    }
}
