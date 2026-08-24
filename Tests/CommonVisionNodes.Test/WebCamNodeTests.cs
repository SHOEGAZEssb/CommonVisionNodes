using System.Runtime.InteropServices;
using CommonVisionNodes.Runtime;
using Stemmer.Cvb;

namespace CommonVisionNodes.Test;

public class WebCamNodeTests
{
	[Test]
	public void Constructor_ShouldExposeTriggerAndCvbImageOutput()
	{
		var node = CreateNode(new FakeCapture());

		using (Assert.EnterMultipleScope())
		{
			Assert.That(node.TriggerInput.Type, Is.EqualTo(typeof(TriggerSignal)));
			Assert.That(node.ImageOutput.Type, Is.EqualTo(typeof(Image)));
			Assert.That(node.Inputs, Has.Count.EqualTo(1));
			Assert.That(node.Outputs, Has.Count.EqualTo(1));
		}
	}

	[Test]
	public void Execute_ShouldConvertBgr24FrameToPlanarRgbCvbImage()
	{
		var capture = new FakeCapture(new WebCamFrame(
			Width: 2,
			Height: 1,
			Stride: 8,
			Bgr24: new byte[] { 10, 20, 30, 40, 50, 60, 99, 99 }));
		var node = CreateNode(capture);

		try
		{
			node.Initialize();
			node.Execute();

			var image = (Image)node.ImageOutput.Value!;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(image.Width, Is.EqualTo(2));
				Assert.That(image.Height, Is.EqualTo(1));
				Assert.That(image.Planes, Has.Count.EqualTo(3));
				Assert.That(ReadPixel(image, planeIndex: 0, x: 0), Is.EqualTo(30));
				Assert.That(ReadPixel(image, planeIndex: 1, x: 0), Is.EqualTo(20));
				Assert.That(ReadPixel(image, planeIndex: 2, x: 0), Is.EqualTo(10));
				Assert.That(ReadPixel(image, planeIndex: 0, x: 1), Is.EqualTo(60));
				Assert.That(ReadPixel(image, planeIndex: 1, x: 1), Is.EqualTo(50));
				Assert.That(ReadPixel(image, planeIndex: 2, x: 1), Is.EqualTo(40));
			}
		}
		finally
		{
			node.Dispose();
		}
	}

	[Test]
	public void Initialize_ShouldResolveSelectedDeviceAndOpenItsCurrentDirectShowSlot()
	{
		var factory = new FakeCaptureFactory(new FakeCapture());
		var devices = new FakeDeviceProvider(
			new WebCamDevice("path-a", "First Camera", 0),
			new WebCamDevice("path-b", "Preferred Camera", 3));
		var node = new WebCamNode(factory, devices)
		{
			DeviceId = "path-b"
		};

		try
		{
			node.Initialize();

			Assert.That(factory.DeviceIndex, Is.EqualTo(3));
			Assert.That(node.IsInitialized, Is.True);
		}
		finally
		{
			node.Dispose();
		}
	}

	[Test]
	public void Dispose_ShouldReleaseCaptureAndClearOutput()
	{
		var capture = new FakeCapture(new WebCamFrame(1, 1, 3, new byte[] { 1, 2, 3 }));
		var node = CreateNode(capture);
		node.Initialize();
		node.Execute();

		node.Dispose();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(capture.IsDisposed, Is.True);
			Assert.That(node.IsInitialized, Is.False);
			Assert.That(node.ImageOutput.Value, Is.Null);
		}
	}

	[Test]
	public void Dispose_WithCapturePool_ShouldKeepCaptureOpenForTheNextGraphExecution()
	{
		var capture = new FakeCapture();
		var factory = new FakeCaptureFactory(capture);
		var devices = new FakeDeviceProvider(new WebCamDevice("camera-path", "Test Camera", 0));
		using var pool = new WebCamCapturePool();
		var first = new WebCamNode(factory, devices, pool) { DeviceId = "camera-path" };
		var second = new WebCamNode(factory, devices, pool) { DeviceId = "camera-path" };

		first.Initialize();
		first.Dispose();
		second.Initialize();

		using (Assert.EnterMultipleScope())
		{
			Assert.That(factory.CreateCount, Is.EqualTo(1));
			Assert.That(capture.IsDisposed, Is.False);
			Assert.That(second.IsInitialized, Is.True);
		}

		second.Dispose();
	}

	[Test]
	public void Initialize_WhenSelectedDeviceIsUnavailable_ShouldExplainHowToRecover()
	{
		var node = new WebCamNode(new FakeCaptureFactory(new FakeCapture()), new FakeDeviceProvider())
		{
			DeviceId = "missing-camera"
		};

		var exception = Assert.Throws<InvalidOperationException>(node.Initialize);

		Assert.That(exception!.Message, Does.Contain("Refresh the webcam list"));
	}

	private static WebCamNode CreateNode(FakeCapture capture)
		=> new(new FakeCaptureFactory(capture), new FakeDeviceProvider(new WebCamDevice("camera-path", "Test Camera", 0)))
		{
			DeviceId = "camera-path"
		};

	private static byte ReadPixel(Image image, int planeIndex, int x)
	{
		var access = image.Planes[planeIndex].GetLinearAccess();
		return Marshal.ReadByte(access.BasePtr, checked((int)(x * access.XInc.ToInt64())));
	}

	private sealed class FakeCaptureFactory(FakeCapture capture) : IWebCamCaptureFactory
	{
		public int DeviceIndex { get; private set; } = -1;
		public int CreateCount { get; private set; }

		public IWebCamCapture Create(int deviceIndex)
		{
			DeviceIndex = deviceIndex;
			CreateCount++;
			return capture;
		}
	}

	private sealed class FakeDeviceProvider(params WebCamDevice[] devices) : IWebCamDeviceProvider
	{
		public IReadOnlyList<WebCamDevice> GetDevices() => devices;
	}

	private sealed class FakeCapture(WebCamFrame? frame = null) : IWebCamCapture
	{
		private readonly WebCamFrame? _frame = frame;

		public bool IsOpened => true;
		public bool IsDisposed { get; private set; }

		public bool TryRead(out WebCamFrame frame)
		{
			frame = _frame ?? default;
			return _frame.HasValue;
		}

		public void Dispose() => IsDisposed = true;
	}
}
