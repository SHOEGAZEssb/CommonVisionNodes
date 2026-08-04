using Uno.UI.Hosting;

namespace CommonVisionNodesUI.Platforms.WebAssembly;

/// <summary>
/// Browser WebAssembly host entry point.
/// </summary>
public class Program
{
	/// <summary>
	/// Starts the Uno WebAssembly host.
	/// </summary>
	/// <param name="args">Command-line arguments supplied by the host.</param>
	/// <returns>A task that completes when the host exits.</returns>
	public static async Task Main(string[] args)
	{
		var host = UnoPlatformHostBuilder.Create()
			.App(() => new App())
			.UseWebAssembly()
			.Build();

		await host.RunAsync();
	}
}
