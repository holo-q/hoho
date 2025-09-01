# Directory-Based Decompilation Workflow

## Directory Structure

```
claude-code-dev/
├── mappings/
│   ├── global-mappings.json         # All learned symbol mappings
│   └── version-mappings/
│       ├── 1.0.98.json              # Version-specific mappings
│       └── 1.0.99.json
├── downloads/                       # Cached downloads
│   ├── claude-code-1.0.98.tgz
│   └── claude-code-1.0.99.tgz
├── versions/
│   ├── 1.0.98/
│   │   ├── original/                # Original obfuscated code
│   │   │   ├── cli.js
│   │   │   ├── sdk.mjs
│   │   │   └── modules/
│   │   │       ├── Wu1.js          # Extracted obfuscated modules
│   │   │       ├── Bx2.js
│   │   │       └── Y2Q.js
│   │   ├── manual/                  # YOUR MANUAL EDITS GO HERE
│   │   │   ├── README.md
│   │   │   ├── cli.js              # Your clean version
│   │   │   ├── sdk.mjs             # Your clean version
│   │   │   └── modules/
│   │   │       ├── ReactModule.js  # Wu1 renamed
│   │   │       ├── FileUtils.js    # Bx2 renamed
│   │   │       └── Counter.js      # Y2Q renamed
│   │   ├── automated/               # Tool-generated deobfuscation
│   │   │   ├── cli.js
│   │   │   ├── sdk.mjs
│   │   │   └── modules/
│   │   └── analysis/
│   │       └── reports.md
│   └── 1.0.99/
│       ├── original/
│       ├── manual/
│       ├── automated/              # Mappings from 1.0.98 applied here
│       └── analysis/
└── registry.json                   # Version tracking
```

## Complete Workflow

### Step 1: Setup First Version (1.0.98)

```bash
# Setup version 1.0.98
hoho decomp setup 1.0.98

# Or download automatically
hoho decomp setup 1.0.98 --source https://registry.npmjs.org/@anthropic-ai/claude-code/-/claude-code-1.0.98.tgz
```

This creates:
```
claude-code-dev/versions/1.0.98/
├── original/       # Extracted obfuscated code
│   ├── cli.js
│   └── modules/
│       ├── Wu1.js
│       └── Bx2.js
└── manual/         # Templates for your edits
    ├── README.md
    ├── cli.js
    └── modules/
```

### Step 2: Manual Decompilation

Edit files in `claude-code-dev/versions/1.0.98/manual/`:

**original/modules/Wu1.js:**
```javascript
var Wu1=U((bnB)=>{
  var pA1=Symbol.for("react.element");
  function ynB(A){
    if(A===null)return null;
    return A;
  }
});
```

**manual/modules/ReactModule.js:** (You create this)
```javascript
var ReactModule = U((exports) => {
  var REACT_ELEMENT_TYPE = Symbol.for("react.element");
  function getIteratorFn(maybeIterable) {
    if (maybeIterable === null) return null;
    return maybeIterable;
  }
});
```

### Step 3: Learn From Your Edits

```bash
# Learn mappings from entire directory
hoho decomp learn-dir 1.0.98
```

Output:
```
Learned 5234 mappings from version 1.0.98

Mappings by file:
  cli.js: 3421 mappings
  sdk.mjs: 892 mappings
  modules/Wu1.js: 234 mappings
  modules/Bx2.js: 187 mappings

File renames learned:
  Wu1.js -> ReactModule.js
  Bx2.js -> FileUtils.js
  Y2Q.js -> Counter.js
```

### Step 4: Setup New Version (1.0.99)

```bash
# Setup version 1.0.99
hoho decomp setup 1.0.99
```

### Step 5: Apply Learned Mappings

```bash
# Apply mappings from 1.0.98 to 1.0.99
hoho decomp apply-dir 1.0.99 --source 1.0.98
```

Output:
```
Applying mappings from 1.0.98 to 1.0.99
Processing cli.js...
Processing sdk.mjs...
Processing module Wu1 -> ReactModule...
Processing module Bx2 -> FileUtils...
Processing module Zx9 -> Unknown_Zx9... (new module)

Automated deobfuscation complete for version 1.0.99
Output: claude-code-dev/versions/1.0.99/automated/
```

### Step 6: Review and Update

Check `automated/` directory for the deobfuscated code:
- 95% of symbols automatically renamed correctly
- New modules need manual review
- Copy good parts to `manual/` and fix any errors

### Step 7: Learn From Corrections

```bash
# Learn from your corrections to 1.0.99
hoho decomp learn-dir 1.0.99

# This updates the global mappings with new patterns
```

### Step 8: Future Versions Get Better

```bash
# Version 1.1.0 arrives
hoho decomp setup 1.1.0
hoho decomp apply-dir 1.1.0  # Automatically uses best available mappings

# 98% accuracy because it learned from both 1.0.98 and 1.0.99
```

## Managing Multiple Versions

### List All Versions
```bash
hoho decomp list
```

Output:
```
Managed Claude Code Versions:
============================================================

1.0.99:
  Status: Automated
  Added: 2024-01-16
  Mappings: 5342
  Path: claude-code-dev/versions/1.0.99

1.0.98:
  Status: Learned
  Added: 2024-01-15
  Mappings: 5234
  Path: claude-code-dev/versions/1.0.98
```

### Compare Versions
```bash
hoho decomp analyze 1.0.98 1.0.99
```

## Key Benefits

### 1. Organized Structure
- Each version in its own directory
- Clear separation: original / manual / automated
- Easy to compare versions

### 2. Incremental Learning
- Global mappings accumulate knowledge
- Each version improves accuracy
- File renames are tracked

### 3. Bulk Processing
- Process entire codebase at once
- Modules extracted and mapped automatically
- Consistent naming across all files

### 4. Version Management
- Download once, reuse multiple times
- Track status of each version
- See which versions have been processed

## Example: Real Usage

```bash
# Day 1: Setup and manual work
hoho decomp setup 1.0.98
# ... spend 8 hours editing files in manual/ ...
hoho decomp learn-dir 1.0.98
# Learned 5234 mappings

# Day 2: New version released
hoho decomp setup 1.0.99
hoho decomp apply-dir 1.0.99
# Check automated/, make minor fixes in manual/
hoho decomp learn-dir 1.0.99
# Learned 5342 mappings (108 new)

# Day 10: Another version
hoho decomp setup 1.1.0
hoho decomp apply-dir 1.1.0
# 98% accurate, only 15 minutes of manual work

# Day 30: Check your progress
hoho decomp list
# Shows 10 versions managed, 50,000+ mappings learned
```

## Tips

1. **Always edit in manual/** - Never edit original/ or automated/
2. **Keep file structure** - Match the original structure in manual/
3. **Use meaningful names** - The tool learns your naming patterns
4. **Commit often** - Version control the claude-code-dev directory
5. **Share mappings** - The mappings/*.json files can be shared with others

## Advanced: Parallel Processing

Process multiple versions in parallel:
```bash
# Setup multiple versions
for version in 1.0.96 1.0.97 1.0.98; do
  hoho decomp setup $version &
done
wait

# After manual edits, learn from all
for version in 1.0.96 1.0.97 1.0.98; do
  hoho decomp learn-dir $version
done

# Apply to new version using combined knowledge
hoho decomp apply-dir 1.0.99
```

This directory-based approach scales to handle dozens of versions with minimal manual effort after the initial investment.