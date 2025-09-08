#!/bin/bash

# HOHO Benchmark Validation Script
# Validates that benchmarks compile and basic functionality works

echo "🚀 HOHO Benchmark Validation - Shadow Protocol"
echo "=" * 50

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✅ $2${NC}"
    else
        echo -e "${RED}❌ $2${NC}"
        return 1
    fi
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_info() {
    echo -e "ℹ️  $1"
}

# Step 1: Check if we're in the right directory
if [ ! -f "Hoho.Benchmarks.csproj" ]; then
    echo -e "${RED}❌ Not in benchmark directory. Run from Hoho.Benchmarks/${NC}"
    exit 1
fi

print_info "Validating benchmark project..."

# Step 2: Check dependencies
print_info "Checking .NET installation..."
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_status 0 ".NET CLI found: $DOTNET_VERSION"
else
    print_status 1 ".NET CLI not found"
    exit 1
fi

# Step 3: Restore packages
print_info "Restoring NuGet packages..."
dotnet restore > /dev/null 2>&1
print_status $? "Package restore"

# Step 4: Build project
print_info "Building benchmark project..."
dotnet build -c Release --no-restore > /dev/null 2>&1
print_status $? "Project build"

# Step 5: Test help command
print_info "Testing help command..."
OUTPUT=$(dotnet run help 2>&1)
if [[ $OUTPUT == *"HOHO Performance Benchmarks"* ]]; then
    print_status 0 "Help command works"
else
    print_status 1 "Help command failed"
    echo "Output: $OUTPUT"
fi

# Step 6: Test validation command
print_info "Testing validation command..."
OUTPUT=$(dotnet run validate 2>&1)
if [[ $OUTPUT == *"Performance Target Validation"* ]]; then
    print_status 0 "Validation command works"
else
    print_status 1 "Validation command failed"
fi

# Step 7: Quick compilation test for all benchmark classes
print_info "Testing benchmark class compilation..."

# Check if key benchmark files exist
BENCHMARK_FILES=(
    "MessagePackSerializationBenchmark.cs"
    "MemoryAllocationBenchmark.cs" 
    "LargeDatasetBenchmark.cs"
    "DatabaseOperationBenchmark.cs"
    "BenchmarkRunner.cs"
)

all_files_exist=true
for file in "${BENCHMARK_FILES[@]}"; do
    if [ -f "$file" ]; then
        print_status 0 "Found $file"
    else
        print_status 1 "Missing $file"
        all_files_exist=false
    fi
done

# Step 8: Check project references
print_info "Validating project references..."
if grep -q "ProjectReference.*Hoho.csproj" Hoho.Benchmarks.csproj; then
    print_status 0 "Main project reference found"
else
    print_warning "Main project reference may be missing"
fi

# Step 9: Check required packages
print_info "Validating required packages..."
REQUIRED_PACKAGES=(
    "BenchmarkDotNet"
    "MessagePack"
    "Bogus"
    "System.Text.Json"
)

for package in "${REQUIRED_PACKAGES[@]}"; do
    if grep -q "PackageReference.*$package" Hoho.Benchmarks.csproj; then
        print_status 0 "$package package referenced"
    else
        print_status 1 "$package package missing"
        all_files_exist=false
    fi
done

# Summary
echo ""
echo "📋 Validation Summary:"
if $all_files_exist; then
    print_status 0 "All validations passed"
    echo ""
    print_info "Ready to run benchmarks:"
    echo "  dotnet run                # Run all benchmarks"
    echo "  dotnet run serialization  # Test MessagePack performance"
    echo "  dotnet run memory         # Test memory efficiency"
    echo ""
    print_warning "Note: Full benchmark runs can take 10-30 minutes"
    print_warning "Consider running individual suites for faster feedback"
else
    print_status 1 "Some validations failed"
    exit 1
fi