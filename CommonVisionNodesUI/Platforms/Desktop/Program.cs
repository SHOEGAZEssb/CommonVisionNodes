using Uno.UI.Hosting;

namespace CommonVisionNodesUI.Platforms.Desktop;

internal class Program
{
	/// <summary>
	/// Starts the Uno desktop host.
	/// </summary>
	/// <param name="args">Command-line arguments supplied by the host.</param>
	[STAThread]
	public static void Main(string[] args)
	{

		var host = UnoPlatformHostBuilder.Create()
			.App(() => new App())
			.UseX11()
			.UseLinuxFrameBuffer()
			.UseMacOS()
			.UseWin32()
			.Build();

		host.Run();
	}
}
