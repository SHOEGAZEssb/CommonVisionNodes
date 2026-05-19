using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.ViewModels;

/// <summary>
/// View model for a rendered connection between two ports.
/// </summary>
/// <remarks>
/// Creates a connection view model.
/// </remarks>
/// <param name="connection">Serialized connection DTO.</param>
/// <param name="source">Source output port.</param>
/// <param name="target">Target input port.</param>
public class ConnectionViewModel(ConnectionDto connection, PortViewModel source, PortViewModel target)
{

	/// <summary>
	/// Serialized connection DTO.
	/// </summary>
	public ConnectionDto Connection { get; } = connection;

	/// <summary>
	/// Source output port.
	/// </summary>
	public PortViewModel Source { get; } = source;

	/// <summary>
	/// Target input port.
	/// </summary>
	public PortViewModel Target { get; } = target;
}
