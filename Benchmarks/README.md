# Image preview benchmarks

`CommonVisionNodes.Benchmarks` measures the application-controlled hot path used to move and
display live image previews:

- `ImageTransferBenchmarks` compares the original allocating 16 KiB receive path with the current
  two-buffer-per-node, 64 KiB receive path. It also measures backend metadata serialization and the
  combined current binary-protocol round trip, plus the full frontend receive-and-pixel-upload path.
- `ImageDrawingBenchmarks` measures expanding/copying Gray8, RGB24, and BGRA32 frames into a reused
  frontend BGRA pixel buffer, the operation immediately before `WriteableBitmap.Invalidate()`.
- `PreviewGenerationBenchmarks` measures backend downscaling/raw-format packing for a 2560×2048
  camera frame and compares allocating versus reusable output buffers.

Run all benchmarks from the repository root in Release mode:

```powershell
dotnet run -c Release --project Benchmarks\CommonVisionNodes.Benchmarks\CommonVisionNodes.Benchmarks.csproj -- --filter '*'
```

Run a quick smoke job while developing the benchmark itself:

```powershell
dotnet run -c Release --project Benchmarks\CommonVisionNodes.Benchmarks\CommonVisionNodes.Benchmarks.csproj -- --filter '*' --job Dry
```

Filter to one stage when investigating a regression:

```powershell
dotnet run -c Release --project Benchmarks\CommonVisionNodes.Benchmarks\CommonVisionNodes.Benchmarks.csproj -- --filter '*ImageTransfer*'
dotnet run -c Release --project Benchmarks\CommonVisionNodes.Benchmarks\CommonVisionNodes.Benchmarks.csproj -- --filter '*ImageDrawing*'
```

The transfer benchmarks deliberately exclude network latency and WebSocket implementation
overhead; they measure the framing, JSON metadata, allocation, and payload copies owned by this
repository. The drawing benchmark uses a memory-backed stand-in for Uno's pixel-buffer stream, so
it measures the upload performed by the application but not `Invalidate()`, rasterization, GPU
composition, or presentation latency. Those stages require an instrumented running UI rather than
a stable headless microbenchmark.

The current receive path keeps two exact-size raw buffers per preview node and alternates between
them. This preserves the frame currently owned by the UI while eliminating steady-state large
object heap allocations. The cache is cleared when a new graph execution starts.
