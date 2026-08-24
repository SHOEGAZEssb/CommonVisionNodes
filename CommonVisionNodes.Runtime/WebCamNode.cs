using System.Runtime.InteropServices;
using DirectShowLib;
using OpenCvSharp;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime;

/// <summary>
/// Identifies a DirectShow webcam that OpenCV can open by its current device slot.
/// </summary>
/// <param name="Id">Stable DirectShow device path persisted in the graph.</param>
/// <param name="DisplayName">Friendly name shown in the node editor.</param>
/// <param name="Index">Current DirectShow device slot used only while opening the camera.</param>
public sealed record WebCamDevice(string Id, string DisplayName, int Index);

/// <summary>
/// Enumerates locally connected DirectShow webcams.
/// </summary>
public interface IWebCamDeviceProvider
{
	/// <summary>Gets the webcams currently available to DirectShow.</summary>
	IReadOnlyList<WebCamDevice> GetDevices();
}

/// <summary>
/// Enumerates DirectShow video input devices with their user-facing names.
/// </summary>
public sealed class DirectShowWebCamDeviceProvider : IWebCamDeviceProvider
{
	/// <inheritdoc/>
	public IReadOnlyList<WebCamDevice> GetDevices()
	{
		try
		{
			return [.. DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice)
				.Select((device, index) => new WebCamDevice(device.DevicePath, device.Name, index))];
		}
		catch
		{
			// Device discovery is best effort. The catalog must remain available when Windows
			// camera components or drivers are unavailable.
			return [];
		}
	}
}

/// <summary>
/// A single interleaved BGR24 webcam frame.
/// </summary>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Stride">Number of bytes from one row to the next.</param>
/// <param name="Bgr24">Frame bytes in BGR channel order.</param>
public readonly record struct WebCamFrame(int Width, int Height, int Stride, ReadOnlyMemory<byte> Bgr24);

/// <summary>
/// Supplies webcam frames to <see cref="WebCamNode"/>.
/// </summary>
public interface IWebCamCapture : IDisposable
{
	/// <summary>Whether the capture device opened successfully.</summary>
	bool IsOpened { get; }

	/// <summary>Attempts to read the next frame.</summary>
	bool TryRead(out WebCamFrame frame);
}

/// <summary>
/// Creates DirectShow webcam captures. The abstraction keeps node tests independent of local hardware.
/// </summary>
public interface IWebCamCaptureFactory
{
	/// <summary>Creates a capture for the current DirectShow device slot.</summary>
	IWebCamCapture Create(int deviceIndex);
}

/// <summary>
/// Keeps idle webcam captures alive between graph executions so reopening the same camera is fast.
/// </summary>
public interface IWebCamCapturePool
{
	/// <summary>Reserves an existing capture or opens a new one for the selected device.</summary>
	IWebCamCapture Rent(string deviceId, int deviceIndex, IWebCamCaptureFactory captureFactory);

	/// <summary>Returns a capture to the idle pool without closing the device.</summary>
	void Return(string deviceId, IWebCamCapture capture);

	/// <summary>Removes a failed capture from the pool and closes it.</summary>
	void Discard(string deviceId, IWebCamCapture capture);
}

/// <summary>
/// Process-local cache of idle DirectShow webcam captures.
/// </summary>
public sealed class WebCamCapturePool : IWebCamCapturePool, IDisposable
{
	private sealed class Entry(IWebCamCapture capture)
	{
		public IWebCamCapture Capture { get; } = capture;
		public bool IsInUse { get; set; }
	}

	private readonly Lock _sync = new();
	private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Shared pool used by normal runtime-created webcam nodes.</summary>
	public static WebCamCapturePool Shared { get; } = new();

	/// <inheritdoc/>
	public IWebCamCapture Rent(string deviceId, int deviceIndex, IWebCamCaptureFactory captureFactory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
		ArgumentNullException.ThrowIfNull(captureFactory);

		lock (_sync)
		{
			if (_entries.TryGetValue(deviceId, out var entry))
			{
				if (!entry.Capture.IsOpened)
				{
					_entries.Remove(deviceId);
					entry.Capture.Dispose();
				}
				else
				{
					if (entry.IsInUse)
						throw new InvalidOperationException("The selected webcam is already in use by another executing graph.");

					entry.IsInUse = true;
					return entry.Capture;
				}
			}

			var capture = captureFactory.Create(deviceIndex);
			if (capture.IsOpened)
				_entries.Add(deviceId, new Entry(capture) { IsInUse = true });

			return capture;
		}
	}

	/// <inheritdoc/>
	public void Return(string deviceId, IWebCamCapture capture)
	{
		lock (_sync)
		{
			if (_entries.TryGetValue(deviceId, out var entry) && ReferenceEquals(entry.Capture, capture))
			{
				entry.IsInUse = false;
				return;
			}
		}

		capture.Dispose();
	}

	/// <inheritdoc/>
	public void Discard(string deviceId, IWebCamCapture capture)
	{
		lock (_sync)
		{
			if (_entries.TryGetValue(deviceId, out var entry) && ReferenceEquals(entry.Capture, capture))
				_entries.Remove(deviceId);
		}

		capture.Dispose();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		IWebCamCapture[] captures;
		lock (_sync)
		{
			captures = [.. _entries.Values.Select(entry => entry.Capture)];
			_entries.Clear();
		}

		foreach (var capture in captures)
			capture.Dispose();
	}
}

/// <summary>
/// Acquires frames from the selected DirectShow webcam and exposes them as three-plane CVB images.
/// </summary>
public sealed class WebCamNode : Node, IInitializable, ITriggerableNode
{
	private readonly IWebCamCaptureFactory _captureFactory;
	private readonly IWebCamDeviceProvider _deviceProvider;
	private readonly IWebCamCapturePool? _capturePool;
	private IWebCamCapture? _capture;
	private Image? _lastAcquiredImage;

	/// <summary>Optional trigger that gates frame acquisition.</summary>
	public Port TriggerInput { get; }

	/// <summary>Output containing the most recently acquired CVB image.</summary>
	public Port ImageOutput { get; }

	/// <summary>Stable DirectShow device path of the webcam selected in the editor.</summary>
	public string DeviceId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <summary>Creates a webcam node that uses the local DirectShow device list and OpenCV capture.</summary>
	public WebCamNode()
		: this(new OpenCvWebCamCaptureFactory(), new DirectShowWebCamDeviceProvider(), WebCamCapturePool.Shared)
	{
	}

	/// <summary>Creates a webcam node with supplied discovery and capture dependencies.</summary>
	public WebCamNode(IWebCamCaptureFactory captureFactory, IWebCamDeviceProvider deviceProvider)
		: this(captureFactory, deviceProvider, capturePool: null)
	{
	}

	/// <summary>Creates a webcam node with supplied discovery, capture, and retention dependencies.</summary>
	public WebCamNode(IWebCamCaptureFactory captureFactory, IWebCamDeviceProvider deviceProvider, IWebCamCapturePool? capturePool)
	{
		_captureFactory = captureFactory ?? throw new ArgumentNullException(nameof(captureFactory));
		_deviceProvider = deviceProvider ?? throw new ArgumentNullException(nameof(deviceProvider));
		_capturePool = capturePool;
		TriggerInput = AddInput("Trigger", typeof(TriggerSignal), "Optional trigger that controls when a webcam frame is acquired.");
		ImageOutput = AddOutput("Image", typeof(Image), "The most recently acquired webcam image as a CVB image.");
	}

	/// <inheritdoc/>
	public void Initialize()
	{
		if (string.IsNullOrWhiteSpace(DeviceId))
			throw new InvalidOperationException("Select a webcam before starting the graph.");

		var device = _deviceProvider.GetDevices()
			.FirstOrDefault(candidate => string.Equals(candidate.Id, DeviceId, StringComparison.OrdinalIgnoreCase));
		if (device is null)
			throw new InvalidOperationException("The selected webcam is no longer available. Refresh the webcam list and select an available camera.");

		Dispose();
		try
		{
			_capture = _capturePool?.Rent(DeviceId, device.Index, _captureFactory) ?? _captureFactory.Create(device.Index);
			if (!_capture.IsOpened)
				throw new InvalidOperationException($"Unable to open webcam '{device.DisplayName}'. Verify that it is connected, not in use by another application, and that its Windows camera permission is enabled.");

			IsInitialized = true;
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	/// <inheritdoc/>
	public override void Execute()
	{
		if (!IsInitialized || _capture is null)
			throw new InvalidOperationException($"{nameof(WebCamNode)} must be initialized before execution.");

		if (!_capture.TryRead(out var frame))
		{
			if (_capturePool is not null)
				_capturePool.Discard(DeviceId, _capture);
			_capture = null;
			IsInitialized = false;
			throw new InvalidOperationException("The selected webcam did not provide a frame.");
		}

		var image = CreateCvbImage(frame);
		_lastAcquiredImage?.Dispose();
		_lastAcquiredImage = image;
		ImageOutput.Value = image;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_capture is not null)
		{
			if (_capturePool is not null)
				_capturePool.Return(DeviceId, _capture);
			else
				_capture.Dispose();
		}
		_capture = null;

		_lastAcquiredImage?.Dispose();
		_lastAcquiredImage = null;
		ImageOutput.Value = null;
		IsInitialized = false;
	}

	/// <inheritdoc/>
	public override void EmitCode(CodeEmitContext context)
		=> throw new NotSupportedException("Code generation does not support WebCamNode. Use the runtime graph to acquire webcam frames.");

	private static unsafe Image CreateCvbImage(WebCamFrame frame)
	{
		if (frame.Width <= 0 || frame.Height <= 0)
			throw new InvalidOperationException("Webcam returned an empty frame.");

		var requiredRowBytes = checked(frame.Width * 3);
		if (frame.Stride < requiredRowBytes || frame.Bgr24.Length < checked(frame.Stride * frame.Height))
			throw new InvalidOperationException("Webcam returned an invalid BGR24 frame buffer.");

		var image = new Image(frame.Width, frame.Height, 3);
		try
		{
			var red = image.Planes[0].GetLinearAccess();
			var green = image.Planes[1].GetLinearAccess();
			var blue = image.Planes[2].GetLinearAccess();
			var source = frame.Bgr24.Span;
			var redBase = (byte*)red.BasePtr;
			var greenBase = (byte*)green.BasePtr;
			var blueBase = (byte*)blue.BasePtr;
			var redXIncrement = red.XInc.ToInt64();
			var greenXIncrement = green.XInc.ToInt64();
			var blueXIncrement = blue.XInc.ToInt64();
			var redYIncrement = red.YInc.ToInt64();
			var greenYIncrement = green.YInc.ToInt64();
			var blueYIncrement = blue.YInc.ToInt64();

			fixed (byte* sourceBase = source)
			{
				for (var y = 0; y < frame.Height; y++)
				{
					var sourceRow = sourceBase + checked(y * frame.Stride);
					var redRow = redBase + checked((nint)(y * redYIncrement));
					var greenRow = greenBase + checked((nint)(y * greenYIncrement));
					var blueRow = blueBase + checked((nint)(y * blueYIncrement));

					for (var x = 0; x < frame.Width; x++)
					{
						var sourcePixel = sourceRow + checked(x * 3);
						*(redRow + checked((nint)(x * redXIncrement))) = sourcePixel[2];
						*(greenRow + checked((nint)(x * greenXIncrement))) = sourcePixel[1];
						*(blueRow + checked((nint)(x * blueXIncrement))) = sourcePixel[0];
					}
				}
			}

			return image;
		}
		catch
		{
			image.Dispose();
			throw;
		}
	}
}

internal sealed class OpenCvWebCamCaptureFactory : IWebCamCaptureFactory
{
	public IWebCamCapture Create(int deviceIndex) => new OpenCvWebCamCapture(deviceIndex);
}

internal sealed class OpenCvWebCamCapture : IWebCamCapture
{
	private readonly VideoCapture _capture;
	private readonly Mat _frame = new();
	private byte[] _buffer = [];

	public OpenCvWebCamCapture(int deviceIndex)
	{
		// OpenCV's DirectShow backend accepts the current DirectShow slot. Do not call
		// VideoCapture.Set here: each width/height/FPS change can synchronously renegotiate
		// the driver's media pipeline and block for tens of seconds.
		_capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
	}

	public bool IsOpened => _capture.IsOpened();

	public bool TryRead(out WebCamFrame frame)
	{
		frame = default;
		if (!IsOpened || !_capture.Read(_frame) || _frame.Empty())
			return false;

		if (_frame.Type() != MatType.CV_8UC3)
			throw new InvalidOperationException($"Webcam returned unsupported OpenCV frame type '{_frame.Type()}'. Expected BGR24.");

		var stride = checked((int)_frame.Step());
		var byteCount = checked(stride * _frame.Rows);
		if (_buffer.Length != byteCount)
			_buffer = new byte[byteCount];

		Marshal.Copy(_frame.Data, _buffer, 0, byteCount);
		frame = new WebCamFrame(_frame.Cols, _frame.Rows, stride, _buffer);
		return true;
	}

	public void Dispose()
	{
		_frame.Dispose();
		_capture.Dispose();
	}
}
