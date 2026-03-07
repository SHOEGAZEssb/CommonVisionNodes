namespace CommonVisionNodes.Test
{
    public class NodeGraphTests
    {
        [Test]
        public void AddNode_ShouldAddNodeToGraph()
        {
            // Arrange
            var graph = new NodeGraph();
            var node = new ImageNode();

            // Act
            graph.AddNode(node);

            // Assert
            Assert.That(graph.Nodes, Does.Contain(node));
        }

        [Test]
        public void Connect_ShouldCreateConnectionBetweenPorts()
        {
            // Arrange
            var graph = new NodeGraph();
            var node1 = new ImageNode();
            var node2 = new SaveImageNode();
            graph.AddNode(node1);
            graph.AddNode(node2);

            var outputPort = node1.Outputs.First();
            var inputPort = node2.Inputs.First();

            // Act
            graph.Connect(outputPort, inputPort);

            // Assert
            var connection = graph.Connections.FirstOrDefault();
            Assert.That(connection, Is.Not.Null);
            Assert.That(outputPort, Is.EqualTo(connection.Output));
            Assert.That(inputPort, Is.EqualTo(connection.Input));
        }

        [Test]
        public void Connect_ShouldThrowExceptionForInvalidPortDirection()
        {
            // Arrange
            var graph = new NodeGraph();
            var node1 = new ImageNode();
            var node2 = new SaveImageNode();
            graph.AddNode(node1);
            graph.AddNode(node2);

            var invalidOutputPort = node2.Inputs.First();
            var inputPort = node1.Outputs.First();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => graph.Connect(invalidOutputPort, inputPort));
        }
    }
}