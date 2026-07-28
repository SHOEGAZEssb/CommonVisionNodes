using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions;
using Uno.Extensions.Configuration;
using Uno.Extensions.Hosting;
using Uno.Extensions.Localization;
using Uno.UI;
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
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
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
                        .EmbeddedSource<App>()
                        .Section<AppConfig>())
                .UseLocalization()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IBackendClient>(serviceProvider =>
                    {
                        var config = serviceProvider.GetRequiredService<IOptions<AppConfig>>().Value;
                        return new BackendClient(config.BackendBaseUrl);
                    });
                    services.AddSingleton<NodeGraphViewModel>();
                    services.AddSingleton<MainViewModel>();
                }));

        MainWindow = builder.Window;

#if DEBUG && !__WASM__
        // Uno 6.5's WebAssembly ServerProcessorGenerator can fail before producing
        // this extension. Keep Studio support on desktop without breaking Debug
        // WebAssembly builds; browser debugging remains available through the
        // standard WebAssembly debugger.
        MainWindow.UseStudio();
#endif
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
}
