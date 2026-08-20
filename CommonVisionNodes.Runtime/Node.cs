using System.Text;

namespace CommonVisionNodes.Runtime
{
	/// <summary>
	/// Base class for all processing nodes in a <see cref="NodeGraph"/>.
	/// </summary>
	public abstract class Node
	{
		/// <summary>
		/// Unique identifier for this node instance.
		/// </summary>
		public Guid Id { get; } = Guid.NewGuid();

		/// <summary>
		/// Duration of the most recent <see cref="Execute"/> call.
		/// Updated automatically by <see cref="NodeGraph.Execute"/>.
		/// </summary>
		public TimeSpan LastExecutionTime { get; internal set; }

		private readonly List<Port> _inputs = [];
		private readonly List<Port> _outputs = [];

		/// <summary>
		/// Input ports that receive data from other nodes.
		/// </summary>
		public IReadOnlyList<Port> Inputs => _inputs;

		/// <summary>
		/// Output ports that provide data to other nodes.
		/// </summary>
		public IReadOnlyList<Port> Outputs => _outputs;

		/// <summary>
		/// Registers a new input port on this node.
		/// </summary>
		/// <param name="name">Display name of the port.</param>
		/// <param name="type">The data type the port accepts.</param>
		/// <param name="description">Human-readable description of the port.</param>
		/// <returns>The created input port.</returns>
		protected Port AddInput(string name, Type type, string description = "")
		{
			var port = new Port(this, name, type, PortDirection.Input, description);
			_inputs.Add(port);
			return port;
		}

		/// <summary>
		/// Removes an input port previously registered on this node.
		/// Intended for nodes whose persisted configuration determines their port count.
		/// </summary>
		/// <param name="port">Input port to remove.</param>
		protected void RemoveInput(Port port)
		{
			ArgumentNullException.ThrowIfNull(port);
			if (port.Node != this || port.Direction != PortDirection.Input || !_inputs.Remove(port))
				throw new InvalidOperationException("Input port does not belong to this node.");
		}

		/// <summary>
		/// Registers a new output port on this node.
		/// </summary>
		/// <param name="name">Display name of the port.</param>
		/// <param name="type">The data type the port provides.</param>
		/// <param name="description">Human-readable description of the port.</param>
		/// <returns>The created output port.</returns>
		protected Port AddOutput(string name, Type type, string description = "")
		{
			var port = new Port(this, name, type, PortDirection.Output, description);
			_outputs.Add(port);
			return port;
		}

		/// <summary>
		/// Removes an output port previously registered on this node.
		/// Intended for nodes whose persisted configuration determines their port count.
		/// </summary>
		/// <param name="port">Output port to remove.</param>
		protected void RemoveOutput(Port port)
		{
			ArgumentNullException.ThrowIfNull(port);
			if (port.Node != this || port.Direction != PortDirection.Output || !_outputs.Remove(port))
				throw new InvalidOperationException("Output port does not belong to this node.");
		}

		/// <summary>
		/// Processes input data and produces output. Called once per graph execution cycle.
		/// </summary>
		public abstract void Execute();

		// Code generation

		/// <summary>
		/// Preferred variable name used in generated code.
		/// </summary>
		public virtual string CodeVariableName => "result";

		/// <summary>
		/// Using directives required by the code this node emits.
		/// </summary>
		public virtual IReadOnlyList<string> RequiredUsings => [];

		/// <summary>
		/// Emits the main body code for this node into the given <paramref name="context"/>.
		/// </summary>
		/// <param name="context">The code generation context.</param>
		public virtual void EmitCode(CodeEmitContext context) { }

		/// <summary>
		/// Emits reusable helper methods that the main body code depends on.
		/// </summary>
		/// <param name="sb">The builder to append helper methods to.</param>
		public virtual void EmitHelperMethods(StringBuilder sb) { }

		/// <summary>
		/// Emits reusable helper methods with access to the current code-generation context.
		/// </summary>
		/// <param name="sb">The builder to append helper methods to.</param>
		/// <param name="context">The code-generation context.</param>
		public virtual void EmitHelperMethods(StringBuilder sb, CodeEmitContext context)
			=> EmitHelperMethods(sb);

		/// <summary>
		/// Whether helper methods emitted by this node may be shared with other instances
		/// of the same runtime type.
		/// </summary>
		public virtual bool ReuseHelperMethodsAcrossInstances => true;
	}
}
