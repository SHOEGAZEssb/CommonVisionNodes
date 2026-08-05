using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CommonVisionNodes.Launcher;

internal static class Program
{
	private const int FrameworkMissingExitCode = unchecked((int)0x80008096);
	private const string BackendUrl = "http://127.0.0.1:5077";
	private static readonly Uri HealthUri = new($"{BackendUrl}/api/health");
	private static readonly Uri WebUiUri = new($"{BackendUrl}/");
	private static readonly Uri BrowserResetUri = new($"{BackendUrl}/browser-reset");

	public static async Task<int> Main(string[] args)
	{
		if (!LauncherOptions.TryParse(args, out var options, out var error))
		{
			if (!string.IsNullOrWhiteSpace(error))
				Console.Error.WriteLine(error);

			PrintUsage();
			return string.IsNullOrWhiteSpace(error) ? 0 : 2;
		}

		var deploymentRoot = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
		var serverExecutable = Path.Combine(deploymentRoot, "Server", "CommonVisionNodes.Server.exe");
		var desktopExecutable = Path.Combine(deploymentRoot, "Desktop", "CommonVisionNodesUI.exe");

		Process? serverProcess = null;
		Process? desktopProcess = null;
		using var childProcessJob = ChildProcessJob.TryCreate();
		using var cancellation = new CancellationTokenSource();

		ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};
		Console.CancelKeyPress += cancelHandler;

		try
		{
			RequireFile(serverExecutable, "The backend executable");

			string? webRoot = null;
			if (options.Mode == LaunchMode.Web)
				webRoot = FindWebRoot(deploymentRoot);
			else
				RequireFile(desktopExecutable, "The desktop UI executable");

			Console.WriteLine("Starting CommonVisionNodes backend...");
			serverProcess = StartBackend(serverExecutable, webRoot);
			childProcessJob?.TryAdd(serverProcess);

			await WaitForBackendAsync(serverProcess, cancellation.Token);
			Console.WriteLine($"Backend ready at {BackendUrl}");

			if (options.Mode == LaunchMode.Desktop)
			{
				Console.WriteLine("Starting Uno desktop UI...");
				desktopProcess = StartDesktop(desktopExecutable);
				childProcessJob?.TryAdd(desktopProcess);

				await WaitForProcessOrCancellationAsync(desktopProcess, cancellation.Token);
			}
			else
			{
				Console.WriteLine($"Web UI available at {BackendUrl}");
				if (!options.NoBrowser)
					OpenBrowser(options.ResetBrowserCache ? BrowserResetUri : WebUiUri);

				Console.WriteLine();
				Console.WriteLine("Press Enter to stop CommonVisionNodes.");
				await WaitForEnterOrCancellationAsync(cancellation.Token);
			}

			return 0;
		}
		catch (OperationCanceledException)
		{
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine($"Launcher failed: {exception.Message}");
			return 1;
		}
		finally
		{
			Console.CancelKeyPress -= cancelHandler;
			StopProcess(desktopProcess, "desktop UI");
			StopProcess(serverProcess, "backend");
		}
	}

	private static Process StartBackend(string executable, string? webRoot)
	{
		var startInfo = new ProcessStartInfo(executable)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = Path.GetDirectoryName(executable)!,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		startInfo.ArgumentList.Add($"--urls={BackendUrl}");
		if (webRoot is not null)
			startInfo.ArgumentList.Add($"--WebRoot={webRoot}");

		var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("The backend process could not be started.");

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		return process;
	}

	private static Process StartDesktop(string executable)
	{
		var process = Process.Start(new ProcessStartInfo(executable)
		{
			UseShellExecute = true,
			WorkingDirectory = Path.GetDirectoryName(executable)!
		});

		return process
			?? throw new InvalidOperationException("The desktop UI process could not be started.");
	}

	private static async Task WaitForBackendAsync(Process process, CancellationToken cancellationToken)
	{
		using var client = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(1)
		};
		var deadline = Stopwatch.StartNew();

		while (deadline.Elapsed < TimeSpan.FromSeconds(20))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (process.HasExited)
				throw new InvalidOperationException(DescribeBackendExit(process.ExitCode));

			try
			{
				using var response = await client.GetAsync(HealthUri, cancellationToken);
				if (response.IsSuccessStatusCode)
					return;
			}
			catch (HttpRequestException)
			{
				// Kestrel has not started accepting connections yet.
			}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// The individual health request timed out; retry until the startup deadline.
			}

			await Task.Delay(150, cancellationToken);
		}

		throw new TimeoutException($"The backend did not become ready at '{HealthUri}' within 20 seconds.");
	}

	private static string DescribeBackendExit(int exitCode)
	{
		if (exitCode == FrameworkMissingExitCode)
		{
			return "The backend requires the .NET 10 ASP.NET Core Runtime (x64), but that runtime " +
				   "is not installed or could not be found. Install it and try again. To list the " +
				   "detected runtimes, run 'dotnet --list-runtimes'.";
		}

		return $"The backend stopped before becoming ready (exit code {exitCode}).";
	}

	private static async Task WaitForProcessOrCancellationAsync(Process process, CancellationToken cancellationToken)
	{
		var processExit = process.WaitForExitAsync(cancellationToken);
		var cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
		await Task.WhenAny(processExit, cancellation);
		cancellationToken.ThrowIfCancellationRequested();
	}

	private static async Task WaitForEnterOrCancellationAsync(CancellationToken cancellationToken)
	{
		var input = Task.Run(Console.ReadLine, CancellationToken.None);
		var cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
		await Task.WhenAny(input, cancellation);
		cancellationToken.ThrowIfCancellationRequested();
	}

	private static void OpenBrowser(Uri uri)
	{
		Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
		{
			UseShellExecute = true
		});
	}

	private static string FindWebRoot(string deploymentRoot)
	{
		var candidates = new[]
		{
			Path.Combine(deploymentRoot, "Web", "wwwroot"),
			Path.Combine(deploymentRoot, "Web")
		};

		foreach (var candidate in candidates)
		{
			if (File.Exists(Path.Combine(candidate, "index.html")))
				return Path.GetFullPath(candidate);
		}

		throw new FileNotFoundException(
			$"The published Web UI was not found below '{Path.Combine(deploymentRoot, "Web")}'.");
	}

	private static void RequireFile(string path, string description)
	{
		if (!File.Exists(path))
			throw new FileNotFoundException($"{description} was not found at '{path}'.", path);
	}

	private static void StopProcess(Process? process, string description)
	{
		if (process is null)
			return;

		try
		{
			if (!process.HasExited)
			{
				Console.WriteLine($"Stopping {description}...");
				process.Kill(entireProcessTree: true);
				process.WaitForExit(5000);
			}
		}
		catch (InvalidOperationException)
		{
			// The process exited while cleanup was checking its state.
		}
		finally
		{
			process.Dispose();
		}
	}

	private static void PrintUsage()
	{
		Console.WriteLine(
			"""
            CommonVisionNodes launcher

            Usage:
              CommonVisionNodes.Launcher.exe [--mode Web|Desktop] [--no-browser] [--reset-browser-cache]

            Options:
              --mode, -m       UI mode. Defaults to Web.
              --no-browser     Start Web mode without opening the default browser.
              --reset-browser-cache
                               Clear cached WebAssembly assets and legacy service workers before opening Web mode.
              --help, -h       Show this help.
            """);
	}
}

internal enum LaunchMode
{
	Web,
	Desktop
}

internal sealed record LauncherOptions(LaunchMode Mode, bool NoBrowser, bool ResetBrowserCache)
{
	public static bool TryParse(
		IReadOnlyList<string> args,
		out LauncherOptions options,
		out string? error)
	{
		var mode = LaunchMode.Web;
		var noBrowser = false;
		var resetBrowserCache = false;

		for (var index = 0; index < args.Count; index++)
		{
			switch (args[index].ToLowerInvariant())
			{
				case "--mode":
				case "-m":
					if (++index >= args.Count ||
						!Enum.TryParse(args[index], ignoreCase: true, out mode))
					{
						options = new LauncherOptions(LaunchMode.Web, false, false);
						error = "--mode must be either Web or Desktop.";
						return false;
					}
					break;
				case "--no-browser":
					noBrowser = true;
					break;
				case "--reset-browser-cache":
					resetBrowserCache = true;
					break;
				case "--help":
				case "-h":
				case "/?":
					options = new LauncherOptions(mode, noBrowser, resetBrowserCache);
					error = null;
					return false;
				default:
					options = new LauncherOptions(LaunchMode.Web, false, false);
					error = $"Unknown option '{args[index]}'.";
					return false;
			}
		}

		options = new LauncherOptions(mode, noBrowser, resetBrowserCache);
		error = null;
		return true;
	}
}

internal sealed partial class ChildProcessJob : IDisposable
{
	private const uint JobObjectLimitKillOnJobClose = 0x00002000;
	private const int JobObjectExtendedLimitInformationClass = 9;
	private readonly SafeFileHandle _handle;

	private ChildProcessJob(SafeFileHandle handle)
	{
		_handle = handle;
	}

	public static ChildProcessJob? TryCreate()
	{
		if (!OperatingSystem.IsWindows())
			return null;

		var handle = CreateJobObjectW(IntPtr.Zero, null);
		if (handle.IsInvalid)
			return null;

		var information = new JobObjectExtendedLimitInformation
		{
			BasicLimitInformation = new JobObjectBasicLimitInformation
			{
				LimitFlags = JobObjectLimitKillOnJobClose
			}
		};
		var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
		var pointer = Marshal.AllocHGlobal(size);

		try
		{
			Marshal.StructureToPtr(information, pointer, fDeleteOld: false);
			if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformationClass, pointer, (uint)size))
			{
				handle.Dispose();
				return null;
			}

			return new ChildProcessJob(handle);
		}
		finally
		{
			Marshal.FreeHGlobal(pointer);
		}
	}

	public bool TryAdd(Process process)
		=> OperatingSystem.IsWindows() &&
		   AssignProcessToJobObject(_handle, process.Handle);

	public void Dispose()
		=> _handle.Dispose();

	[LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	private static partial SafeFileHandle CreateJobObjectW(IntPtr jobAttributes, string? name);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool SetInformationJobObject(
		SafeFileHandle job,
		int informationClass,
		IntPtr information,
		uint informationLength);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectBasicLimitInformation
	{
		public long PerProcessUserTimeLimit;
		public long PerJobUserTimeLimit;
		public uint LimitFlags;
		public UIntPtr MinimumWorkingSetSize;
		public UIntPtr MaximumWorkingSetSize;
		public uint ActiveProcessLimit;
		public UIntPtr Affinity;
		public uint PriorityClass;
		public uint SchedulingClass;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct IoCounters
	{
		public ulong ReadOperationCount;
		public ulong WriteOperationCount;
		public ulong OtherOperationCount;
		public ulong ReadTransferCount;
		public ulong WriteTransferCount;
		public ulong OtherTransferCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectExtendedLimitInformation
	{
		public JobObjectBasicLimitInformation BasicLimitInformation;
		public IoCounters IoInfo;
		public UIntPtr ProcessMemoryLimit;
		public UIntPtr JobMemoryLimit;
		public UIntPtr PeakProcessMemoryUsed;
		public UIntPtr PeakJobMemoryUsed;
	}
}
