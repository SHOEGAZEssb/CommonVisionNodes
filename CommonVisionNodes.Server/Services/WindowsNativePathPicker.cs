using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Server.Services;

/// <summary>
/// Shows Windows shell path pickers from the local execution backend.
/// </summary>
public sealed partial class WindowsNativePathPicker
{
	private const int CancelledHResult = unchecked((int)0x800704C7);

	private readonly SemaphoreSlim _pickerGate = new(1, 1);

	private static readonly Guid FileOpenDialogClassId = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
	private static readonly Guid FileSaveDialogClassId = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
	private static readonly Guid ShellItemInterfaceId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

	/// <summary>
	/// Shows the requested picker on a dedicated STA thread.
	/// </summary>
	public async Task<string?> PickAsync(PathPickerRequestDto request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (!OperatingSystem.IsWindows())
			throw new PlatformNotSupportedException("Native backend path pickers are only available on Windows.");

		var ownerWindow = GetForegroundWindow();
		await _pickerGate.WaitAsync(cancellationToken);
		try
		{
			// Do not abandon an already visible native dialog if the HTTP request is disconnected.
			return await PickOnWindowsAsync(request, ownerWindow);
		}
		finally
		{
			_pickerGate.Release();
		}
	}

	[SupportedOSPlatform("windows")]
	private static Task<string?> PickOnWindowsAsync(PathPickerRequestDto request, nint ownerWindow)
	{
		var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try
			{
				completion.TrySetResult(ShowPicker(request, ownerWindow));
			}
			catch (Exception exception)
			{
				completion.TrySetException(exception);
			}
		})
		{
			IsBackground = true,
			Name = "CommonVisionNodes path picker"
		};
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();

		return completion.Task;
	}

	[SupportedOSPlatform("windows")]
	private static string? ShowPicker(PathPickerRequestDto request, nint ownerWindow)
	{
		var classId = request.Mode == PathPickerModeDto.SaveFile
			? FileSaveDialogClassId
			: FileOpenDialogClassId;
		var dialogType = Type.GetTypeFromCLSID(classId, throwOnError: true)!;
		var dialog = (IFileDialog)Activator.CreateInstance(dialogType)!;

		try
		{
			ConfigureDialog(dialog, request);

			// The shell dialog may live outside the backend process. Granting foreground permission
			// helps it appear in front of the browser that initiated the loopback request.
			_ = CoAllowSetForegroundWindow(dialog, nint.Zero);

			var result = dialog.Show(ownerWindow);
			if (result == CancelledHResult)
				return null;
			Marshal.ThrowExceptionForHR(result);

			dialog.GetResult(out var item);
			try
			{
				item.GetDisplayName(ShellItemDisplayName.FileSystemPath, out var pathPointer);
				try
				{
					return Marshal.PtrToStringUni(pathPointer);
				}
				finally
				{
					Marshal.FreeCoTaskMem(pathPointer);
				}
			}
			finally
			{
				Marshal.FinalReleaseComObject(item);
			}
		}
		finally
		{
			Marshal.FinalReleaseComObject(dialog);
		}
	}

	[SupportedOSPlatform("windows")]
	private static void ConfigureDialog(IFileDialog dialog, PathPickerRequestDto request)
	{
		dialog.GetOptions(out var options);
		options |= FileDialogOptions.ForceFileSystem |
				   FileDialogOptions.NoChangeDirectory |
				   FileDialogOptions.PathMustExist;
		options |= request.Mode switch
		{
			PathPickerModeDto.OpenFile => FileDialogOptions.FileMustExist,
			PathPickerModeDto.OpenFolder => FileDialogOptions.PickFolders,
			PathPickerModeDto.SaveFile => FileDialogOptions.OverwritePrompt,
			_ => throw new ArgumentOutOfRangeException(nameof(request.Mode))
		};
		dialog.SetOptions(options);

		if (!string.IsNullOrWhiteSpace(request.Title))
			dialog.SetTitle(request.Title);

		var extensions = request.FileExtensions
			.Where(extension => !string.IsNullOrWhiteSpace(extension))
			.Select(NormalizeExtension)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (request.Mode != PathPickerModeDto.OpenFolder && extensions.Length > 0)
		{
			dialog.SetFileTypes(1,
			[
				new FileDialogFilterSpec
				{
					Name = "Supported files",
					Specification = string.Join(';', extensions.Select(extension => $"*{extension}"))
				}
			]);
		}

		if (request.Mode == PathPickerModeDto.SaveFile)
		{
			var fileName = GetSuggestedFileName(request);
			if (!string.IsNullOrWhiteSpace(fileName))
				dialog.SetFileName(fileName);
			if (extensions.FirstOrDefault() is { Length: > 1 } defaultExtension)
				dialog.SetDefaultExtension(defaultExtension[1..]);
		}

		var initialDirectory = ResolveInitialDirectory(request.InitialPath);
		if (initialDirectory is not null)
			SetInitialDirectory(dialog, initialDirectory);
	}

	private static string? ResolveInitialDirectory(string? initialPath)
	{
		if (!string.IsNullOrWhiteSpace(initialPath))
		{
			if (Directory.Exists(initialPath))
				return Path.GetFullPath(initialPath);

			if (File.Exists(initialPath))
				return Path.GetDirectoryName(Path.GetFullPath(initialPath));

			try
			{
				var parent = Path.GetDirectoryName(Path.GetFullPath(initialPath));
				if (parent is not null && Directory.Exists(parent))
					return parent;
			}
			catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
			{
				// Fall through to the default location.
			}
		}

		var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		return Directory.Exists(documents) ? documents : null;
	}

	private static string? GetSuggestedFileName(PathPickerRequestDto request)
	{
		if (!string.IsNullOrWhiteSpace(request.SuggestedFileName))
			return Path.GetFileName(request.SuggestedFileName.Trim());

		if (!string.IsNullOrWhiteSpace(request.InitialPath))
			return Path.GetFileName(request.InitialPath.Trim());

		return null;
	}

	[SupportedOSPlatform("windows")]
	private static void SetInitialDirectory(IFileDialog dialog, string directoryPath)
	{
		var interfaceId = ShellItemInterfaceId;
		var result = SHCreateItemFromParsingName(directoryPath, nint.Zero, ref interfaceId, out var item);
		if (result < 0)
			return;

		try
		{
			dialog.SetFolder(item);
		}
		finally
		{
			Marshal.FinalReleaseComObject(item);
		}
	}

	private static string NormalizeExtension(string extension)
	{
		var normalized = extension.Trim();
		return normalized.StartsWith('.') ? normalized : $".{normalized}";
	}

	[Flags]
	private enum FileDialogOptions : uint
	{
		OverwritePrompt = 0x00000002,
		NoChangeDirectory = 0x00000008,
		PickFolders = 0x00000020,
		ForceFileSystem = 0x00000040,
		PathMustExist = 0x00000800,
		FileMustExist = 0x00001000
	}

	private enum ShellItemDisplayName : uint
	{
		FileSystemPath = 0x80058000
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct FileDialogFilterSpec
	{
		[MarshalAs(UnmanagedType.LPWStr)]
		public string Name;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string Specification;
	}

	[ComImport]
	[Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IFileDialog
	{
		[PreserveSig]
		int Show(nint parentWindow);

		void SetFileTypes(
			uint fileTypeCount,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] FileDialogFilterSpec[] filterSpecifications);

		void SetFileTypeIndex(uint fileTypeIndex);

		void GetFileTypeIndex(out uint fileTypeIndex);

		void Advise([MarshalAs(UnmanagedType.Interface)] object events, out uint cookie);

		void Unadvise(uint cookie);

		void SetOptions(FileDialogOptions options);

		void GetOptions(out FileDialogOptions options);

		void SetDefaultFolder(IShellItem shellItem);

		void SetFolder(IShellItem shellItem);

		void GetFolder(out IShellItem shellItem);

		void GetCurrentSelection(out IShellItem shellItem);

		void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

		void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

		void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

		void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

		void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

		void GetResult(out IShellItem shellItem);

		void AddPlace(IShellItem shellItem, uint alignment);

		void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string defaultExtension);

		void Close(int result);

		void SetClientGuid(ref Guid clientGuid);

		void ClearClientData();

		void SetFilter([MarshalAs(UnmanagedType.Interface)] object filter);
	}

	[ComImport]
	[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IShellItem
	{
		void BindToHandler(nint bindContext, ref Guid handlerId, ref Guid interfaceId, out nint result);

		void GetParent(out IShellItem parent);

		void GetDisplayName(ShellItemDisplayName displayName, out nint name);

		void GetAttributes(uint mask, out uint attributes);

		void Compare(IShellItem other, uint hint, out int order);
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
	private static extern int SHCreateItemFromParsingName(
		[MarshalAs(UnmanagedType.LPWStr)] string path,
		nint bindContext,
		ref Guid interfaceId,
		out IShellItem shellItem);

	[DllImport("ole32.dll", PreserveSig = true)]
	private static extern int CoAllowSetForegroundWindow(
		[MarshalAs(UnmanagedType.IUnknown)] object comObject,
		nint reserved);

	[LibraryImport("user32.dll")]
	private static partial nint GetForegroundWindow();
}
