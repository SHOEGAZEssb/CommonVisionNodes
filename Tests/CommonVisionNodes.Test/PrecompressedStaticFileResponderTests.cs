using System.Text;
using CommonVisionNodes.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace CommonVisionNodes.Test;

public sealed class PrecompressedStaticFileResponderTests
{
	[Test]
	public async Task TryServeAsync_WithBrotliSupport_ShouldServeBrotliSidecarWithOriginalContentType()
	{
		using var webRoot = new WebRootFixture();
		webRoot.WriteFile("_framework/app.abc123def4.wasm", "uncompressed");
		webRoot.WriteFile("_framework/app.abc123def4.wasm.br", "brotli");

		var context = CreateContext("GET", "/_framework/app.abc123def4.wasm", "br, gzip");
		var served = await PrecompressedStaticFileResponder.TryServeAsync(
			context,
			webRoot.FileProvider,
			new FileExtensionContentTypeProvider());

		Assert.That(served, Is.True);
		Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
		Assert.That(context.Response.ContentType, Is.EqualTo("application/wasm"));
		Assert.That(context.Response.Headers[HeaderNames.ContentEncoding].ToString(), Is.EqualTo("br"));
		Assert.That(context.Response.Headers[HeaderNames.CacheControl].ToString(), Is.EqualTo("public, max-age=31536000, immutable"));
		Assert.That(context.Response.Headers[HeaderNames.Vary].ToString(), Does.Contain("Accept-Encoding"));
		Assert.That(ReadResponseBody(context), Is.EqualTo("brotli"));
	}

	[Test]
	public async Task TryServeAsync_WhenBrotliIsDisabled_ShouldUseGzipSidecar()
	{
		using var webRoot = new WebRootFixture();
		webRoot.WriteFile("_framework/app.abc123def4.wasm", "uncompressed");
		webRoot.WriteFile("_framework/app.abc123def4.wasm.gz", "gzip");

		var context = CreateContext("GET", "/_framework/app.abc123def4.wasm", "br;q=0, gzip;q=0.8");
		var served = await PrecompressedStaticFileResponder.TryServeAsync(
			context,
			webRoot.FileProvider,
			new FileExtensionContentTypeProvider());

		Assert.That(served, Is.True);
		Assert.That(context.Response.Headers[HeaderNames.ContentEncoding].ToString(), Is.EqualTo("gzip"));
		Assert.That(ReadResponseBody(context), Is.EqualTo("gzip"));
	}

	[Test]
	public async Task TryServeAsync_WithMatchingEntityTag_ShouldReturnNotModifiedWithoutBody()
	{
		using var webRoot = new WebRootFixture();
		webRoot.WriteFile("package/uno-bootstrap.js", "uncompressed");
		webRoot.WriteFile("package/uno-bootstrap.js.br", "brotli");

		var initialContext = CreateContext("GET", "/package/uno-bootstrap.js", "br");
		await PrecompressedStaticFileResponder.TryServeAsync(
			initialContext,
			webRoot.FileProvider,
			new FileExtensionContentTypeProvider());
		var entityTag = initialContext.Response.Headers[HeaderNames.ETag].ToString();

		var cachedContext = CreateContext("GET", "/package/uno-bootstrap.js", "br");
		cachedContext.Request.Headers[HeaderNames.IfNoneMatch] = entityTag;
		var served = await PrecompressedStaticFileResponder.TryServeAsync(
			cachedContext,
			webRoot.FileProvider,
			new FileExtensionContentTypeProvider());

		Assert.That(served, Is.True);
		Assert.That(cachedContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status304NotModified));
		Assert.That(cachedContext.Response.Headers[HeaderNames.ETag].ToString(), Is.EqualTo(entityTag));
		Assert.That(cachedContext.Response.Headers[HeaderNames.ContentEncoding].ToString(), Is.EqualTo("br"));
		Assert.That(ReadResponseBody(cachedContext), Is.Empty);
	}

	[Test]
	public void ApplyCacheHeaders_WithStableFrameworkName_ShouldRequireRevalidation()
	{
		var context = new DefaultHttpContext();

		PrecompressedStaticFileResponder.ApplyCacheHeaders(context.Response, "/_framework/dotnet.boot.json");

		Assert.That(context.Response.Headers[HeaderNames.CacheControl].ToString(), Is.EqualTo("no-cache"));
	}

	[Test]
	public async Task TryServeAsync_WithRangeRequest_ShouldAllowStaticFileMiddlewareToHandleIt()
	{
		using var webRoot = new WebRootFixture();
		webRoot.WriteFile("_framework/app.abc123def4.wasm", "uncompressed");
		webRoot.WriteFile("_framework/app.abc123def4.wasm.br", "brotli");

		var context = CreateContext("GET", "/_framework/app.abc123def4.wasm", "br");
		context.Request.Headers[HeaderNames.Range] = "bytes=0-9";

		var served = await PrecompressedStaticFileResponder.TryServeAsync(
			context,
			webRoot.FileProvider,
			new FileExtensionContentTypeProvider());

		Assert.That(served, Is.False);
		Assert.That(ReadResponseBody(context), Is.Empty);
	}

	private static DefaultHttpContext CreateContext(string method, string path, string acceptEncoding)
	{
		var context = new DefaultHttpContext();
		context.Request.Method = method;
		context.Request.Path = path;
		context.Request.Headers[HeaderNames.AcceptEncoding] = acceptEncoding;
		context.Response.Body = new MemoryStream();
		return context;
	}

	private static string ReadResponseBody(DefaultHttpContext context)
	{
		Assert.That(context.Response.Body, Is.TypeOf<MemoryStream>());
		var stream = (MemoryStream)context.Response.Body;
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private sealed class WebRootFixture : IDisposable
	{
		private readonly string _root = Path.Combine(Path.GetTempPath(), $"cvn-static-assets-{Guid.NewGuid():N}");

		public WebRootFixture()
		{
			Directory.CreateDirectory(_root);
			FileProvider = new PhysicalFileProvider(_root);
		}

		public PhysicalFileProvider FileProvider { get; }

		public void WriteFile(string relativePath, string contents)
		{
			var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, Encoding.UTF8.GetBytes(contents));
		}

		public void Dispose()
		{
			FileProvider.Dispose();
			Directory.Delete(_root, recursive: true);
		}
	}
}
