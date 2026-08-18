using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions;
using Uno.Extensions.Configuration;
using Uno.Extensions.Hosting;
using Uno.Extensions.Localization;
using CommonVisionNodesUI.Services;
using CommonVisionNodesUI.ViewModels;

namespace CommonVisionNodesUI;

/// <summary>
/// Uno application entry point and dependency-injection host setup.
/// </summary>
public partial class App : Application
{
	/// <summary>
	/// Creates the application instance.
	/// </summary>
	public App()
	{
		this.InitializeComponent();
#if __WASM__
		// Uno's upload-picker fallback maps unknown extensions to application/octet-stream,
		// which Firefox displays as "All Files". A file extension is also a valid HTML accept token.
		Uno.WinRTFeatureConfiguration.FileTypes.FileTypeToMimeMapping[".cvbgraph"] = ".cvbgraph";
#endif
	}

	/// <summary>
	/// Main application window.
	/// </summary>
	public Window? MainWindow { get; private set; }

	/// <summary>
	/// Application host used for dependency resolution.
	/// </summary>
	public IHost? Host { get; private set; }

	/// <inheritdoc/>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026",
		Justification = "The Uno hosting setup follows the supported template pattern, and the application assembly is rooted by the WebAssembly linker descriptor.")]
	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		var builder = this.CreateBuilder(args)
			.Configure(ConfigureHost);

		MainWindow = builder.Window;

		MainWindow.SetWindowIcon();

		Host = builder.Build();

		if (MainWindow.Content is not Frame rootFrame)
		{
			rootFrame = new Frame();
			MainWindow.Content = rootFrame;
		}

		if (rootFrame.Content == null)
			rootFrame.Navigate(typeof(MainPage), args.Arguments);

		MainWindow.Activate();
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026",
		Justification = "Uno's supported host configuration uses runtime type discovery. The WebAssembly linker descriptor preserves the application assembly used by that configuration.")]
	private static void ConfigureHost(IHostBuilder host)
	{
		host
#if DEBUG
			.UseEnvironment(Environments.Development)
#endif
			.UseLogging(configure: (context, logBuilder) =>
			{
				logBuilder
					.SetMinimumLevel(
						context.HostingEnvironment.IsDevelopment()
							? LogLevel.Information
							: LogLevel.Warning)
					.CoreLogLevel(LogLevel.Warning);
			}, enableUnoLogging: true)
			.UseConfiguration(configure: configBuilder =>
				configBuilder
					.EmbeddedSource<App>())
			.UseLocalization()
			.ConfigureServices(ConfigureServices);
	}

	private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
	{
	#if __WASM__
		var backendBaseUrl = context.Configuration["AppConfig:BackendBaseUrl"]
			?? "http://127.0.0.1:5077";
		services.AddSingleton<IBackendClient>(_ => new BackendClient(backendBaseUrl));
	#else
		services.AddSingleton<IBackendClient, DesktopBackendClient>();
	#endif
		services.AddSingleton<NodeGraphViewModel>();
		services.AddSingleton<MainViewModel>();
	}
}
