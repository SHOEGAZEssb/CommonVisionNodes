using CommonVisionNodes;

namespace CommonVisionNodes.Test;

public class CSharpNodeTests
{
    [Test]
    public void CSharpNode_ShouldHaveImageInputAndOutput()
    {
        // Arrange
        var node = new CSharpNode();

        // Assert
        Assert.That(node.ImageInput, Is.Not.Null);
        Assert.That(node.ImageOutput, Is.Not.Null);
        Assert.That(node.ImageInput.Direction, Is.EqualTo(PortDirection.Input));
        Assert.That(node.ImageOutput.Direction, Is.EqualTo(PortDirection.Output));
    }

    [Test]
    public void CSharpNode_DefaultCode_ShouldContainExample()
    {
        // Arrange
        var node = new CSharpNode();

        // Assert - default code should contain an example
        Assert.That(node.Code, Does.Contain("inputImage"));
        Assert.That(node.Code, Does.Contain("Filter.Gauss"));
    }

    [Test]
    public void CSharpNode_WithNullInput_ShouldOutputNull()
    {
        // Arrange
        var node = new CSharpNode();
        node.ImageInput.Value = null;

        // Act
        node.Execute();

        // Assert
        Assert.That(node.ImageOutput.Value, Is.Null);
    }

    [Test]
    public void CSharpNode_CodeProperty_ShouldBeSettable()
    {
        // Arrange
        var node = new CSharpNode();
        var newCode = "// Custom code\nreturn inputImage;";

        // Act
        node.Code = newCode;

        // Assert
        Assert.That(node.Code, Is.EqualTo(newCode));
    }

    [Test]
    public void CSharpNode_IsCompiled_ShouldBeFalseInitially()
    {
        // Arrange
        var node = new CSharpNode();

        // Assert
        Assert.That(node.IsCompiled, Is.False);
    }
}
