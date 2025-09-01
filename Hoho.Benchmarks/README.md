# HOHO MessagePack Performance Benchmarks

Comprehensive performance benchmarks for HOHO's MessagePack-based symbol mapping database, targeting 10x performance improvement over JSON serialization.

## 🎯 Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Serialization Speed** | 5-15x faster than JSON | Operations per second |
| **File Size** | 40-70% smaller than JSON | Bytes on disk |
| **Memory Allocation** | 50-80% reduction | Allocated bytes |
| **GC Pressure** | 60-90% fewer collections | Gen0/Gen1 frequency |
| **Large Datasets** | Linear scaling to 100K+ records | Consistent performance |

## 🚀 Quick Start

### Run All Benchmarks
```bash
cd Hoho.Benchmarks
dotnet run
```

### Run Specific Benchmark Suites
```bash
# Serialization performance only
dotnet run serialization

# Memory & GC efficiency only  
dotnet run memory

# Show performance targets
dotnet run validate
```

### View Results
Results are automatically saved to `BenchmarkDotNet.Artifacts/` in multiple formats:
- **GitHub Markdown** (`*-report-github.md`) - For documentation
- **HTML** (`*-report.html`) - Interactive viewing  
- **CSV** (`*-report.csv`) - Data analysis
- **JSON** (`*-report-brief.json`) - Programmatic access

## 📊 Benchmark Suites

### 1. MessagePack vs JSON Serialization
**File**: `MessagePackSerializationBenchmark.cs`

Tests serialization/deserialization performance across different dataset sizes:
- **Small**: 100 symbol mappings
- **Medium**: 1,000 symbol mappings  
- **Large**: 10,000 symbol mappings

**Comparisons**:
- MessagePack vs System.Text.Json
- MessagePack vs Newtonsoft.Json
- Size analysis and compression ratios

### 2. Memory Allocation & GC Efficiency
**File**: `MemoryAllocationBenchmark.cs`

Measures memory usage patterns and garbage collection pressure:
- Standard allocation patterns
- Zero-allocation patterns with pooling
- Streaming operations
- Memory pressure analysis

**Techniques Tested**:
- `ArrayPool<byte>` usage
- `RecyclableMemoryStream` for LOH avoidance
- `ReadOnlyMemory<T>` and `ReadOnlySequence<T>`
- Buffer pooling patterns

### 3. Large Dataset Performance
**File**: `LargeDatasetBenchmark.cs`

Validates scalability with realistic dataset sizes:
- **10K mappings**: Typical single-project decompilation
- **50K mappings**: Large enterprise codebase  
- **100K mappings**: Extreme scale testing

**Analysis**:
- Linear scaling verification
- Memory usage per mapping
- File I/O performance
- Compression efficiency at scale

### 4. Database Operations
**File**: `DatabaseOperationBenchmark.cs`

Real-world usage pattern performance:
- **CRUD**: Add, query, update mappings
- **Search**: Pattern matching, context filtering
- **Persistence**: Save/load operations
- **Migration**: JSON to MessagePack conversion

## 🧠 Memory Optimization Features

### Zero-Allocation Patterns
```csharp
// ArrayPool usage for buffer management
var buffer = ArrayPool<byte>.Shared.Rent(estimatedSize);
try
{
    var writer = new ArrayBufferWriter<byte>(buffer);
    MessagePackSerializer.Serialize(writer, data);
    return writer.WrittenMemory;
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### Streaming Operations
```csharp
// RecyclableMemoryStream to avoid LOH allocations
using var stream = _memoryStreamManager.GetStream();
MessagePackSerializer.Serialize(stream, data);
```

### Memory-Mapped Operations
```csharp
// ReadOnlyMemory for zero-copy deserialization
var memory = data.AsMemory();
var result = MessagePackSerializer.Deserialize<T>(memory);
```

## 📈 Expected Results

### Serialization Performance
```
MessagePack vs System.Text.Json:
- Small datasets:  8-12x faster
- Large datasets:  5-15x faster
- Size reduction:  45-65%

MessagePack vs Newtonsoft.Json:
- Small datasets:  15-25x faster  
- Large datasets:  10-20x faster
- Size reduction:  50-70%
```

### Memory Efficiency
```
Standard vs Zero-Allocation:
- Memory reduction: 60-80%
- GC collections:   70-90% fewer
- Allocation rate:  50-85% lower
```

## 🛠️ Advanced Configuration

### Custom Benchmark Run
```csharp
var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.Default.WithRuntime(CoreRtRuntime.CoreRt31))
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddExporter(MarkdownExporter.GitHub);

BenchmarkRunner.Run<MessagePackSerializationBenchmark>(config);
```

### Filtering Benchmarks
```bash
# Run only small dataset benchmarks
dotnet run --filter "*Small*"

# Run only serialization benchmarks
dotnet run --filter "*Serialize*"

# Run with specific categories
dotnet run --filter "Categories=Serialize"
```

## 🔍 Analyzing Results

### Key Metrics to Monitor

1. **Mean Execution Time**
   - Lower is better
   - Look for 5-15x improvement over JSON

2. **Memory Allocation**
   - Allocated bytes per operation
   - Target: 50-80% reduction

3. **Standard Deviation**
   - Lower = more consistent performance
   - Target: <10% of mean

4. **Scaling Ratios**
   - Performance should scale linearly with dataset size
   - Memory usage should be predictable

### Performance Regression Detection

```bash
# Compare against baseline
dotnet run --runtimes net8.0 net9.0

# Memory profiling
dotnet run --memory-randomization false --profiler ETW
```

## 🚨 Troubleshooting

### Common Issues

**OutOfMemoryException with Large Datasets**
- Increase heap size: `export DOTNET_gcServer=1`
- Use streaming benchmarks instead

**Inconsistent Results**
- Disable background processes
- Run with `--coldStart` flag
- Increase iteration count

**Missing Dependencies**
```bash
# Install required packages
dotnet restore
dotnet build
```

### Performance Validation Checklist

- [ ] MessagePack faster than both JSON libraries
- [ ] Binary size reduction of 40%+ achieved  
- [ ] Memory allocations reduced by 50%+
- [ ] Linear scaling maintained at all dataset sizes
- [ ] No performance regressions vs previous versions
- [ ] GC pressure significantly reduced
- [ ] Zero-allocation patterns working correctly

## 🎨 Integration with CI/CD

### GitHub Actions Example
```yaml
- name: Run Performance Benchmarks
  run: |
    cd Hoho.Benchmarks
    dotnet run serialization
    
- name: Upload Benchmark Results  
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: Hoho.Benchmarks/BenchmarkDotNet.Artifacts/
```

### Performance Regression Alerts
```yaml
- name: Check Performance Targets
  run: |
    cd Hoho.Benchmarks
    dotnet run validate
    # Parse results and fail if targets not met
```

## 📋 Results Template

After running benchmarks, document results using this template:

```markdown
## Benchmark Results - [Date]

### Environment
- OS: Linux/Windows/macOS
- CPU: [CPU Model]  
- Memory: [RAM Amount]
- .NET: [Version]

### Serialization Performance
| Operation | MessagePack | System.Text.Json | Improvement |
|-----------|-------------|------------------|-------------|
| Serialize Small | 1.2ms | 8.4ms | 7.0x faster |
| Deserialize Small | 0.8ms | 6.1ms | 7.6x faster |
| File Size Small | 2.1KB | 5.8KB | 63% smaller |

### Memory Efficiency  
| Metric | Standard | Zero-Alloc | Improvement |
|--------|----------|------------|-------------|
| Allocated | 1.2MB | 0.3MB | 75% reduction |
| Gen0 Collections | 24 | 3 | 87.5% fewer |

### Target Achievement
- [x] 10x serialization improvement: ✅ 7-8x achieved
- [x] 40% size reduction: ✅ 63% achieved  
- [x] 50% memory reduction: ✅ 75% achieved
- [x] Linear scaling: ✅ Confirmed
```

## 📚 References

- [MessagePack Specification](https://msgpack.org/)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Memory Management](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [High Performance .NET](https://docs.microsoft.com/en-us/dotnet/standard/performance/)

---

**Performance is not just about speed - it's about delivering overwhelming simplicity through technical excellence. 🚀**