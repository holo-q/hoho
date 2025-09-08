# Decompilation Mapping Workflow

## The Correct Approach: Learn From Manual Edits

### Step 1: Initial Decompilation (Manual)
You manually decompile and refactor a portion of the obfuscated code:

**Original (obfuscated):**
```javascript
function Wu1(A, B) {
    var Q = A.readFileSync(B, 'utf8');
    var Z = Q.split('\n');
    return Z.map(function(G) {
        return G.trim();
    });
}

class Bx2 {
    constructor(A) {
        this.connection = A;
        this.pool = null;
    }
    
    async query(Q) {
        return await this.connection.query(Q);
    }
}
```

**Your Manual Edit (clean):**
```javascript
function readFileLines(fs, filepath) {
    var content = fs.readFileSync(filepath, 'utf8');
    var lines = content.split('\n');
    return lines.map(function(line) {
        return line.trim();
    });
}

class DatabaseConnection {
    constructor(connection) {
        this.connection = connection;
        this.pool = null;
    }
    
    async query(sql) {
        return await this.connection.query(sql);
    }
}
```

### Step 2: Tool Learns The Mapping

```csharp
var mapper = new DecompilationMapper();
var result = await mapper.LearnMappings(
    "decomp/original/module1.js",
    "decomp/edited/module1-clean.js"
);
```

**What It Learns:**
```json
{
  "mappings": {
    "Wu1": { "mapped": "readFileLines", "type": "Function" },
    "A": { "mapped": "fs", "type": "Parameter", "context": "Wu1" },
    "B": { "mapped": "filepath", "type": "Parameter", "context": "Wu1" },
    "Q": { "mapped": "content", "type": "Variable", "context": "Wu1" },
    "Z": { "mapped": "lines", "type": "Variable", "context": "Wu1" },
    "G": { "mapped": "line", "type": "Parameter", "context": "anonymous" },
    "Bx2": { "mapped": "DatabaseConnection", "type": "Class" },
    "A": { "mapped": "connection", "type": "Parameter", "context": "Bx2.constructor" },
    "Q": { "mapped": "sql", "type": "Parameter", "context": "Bx2.query" }
  },
  "patterns": [
    {
      "pattern": "^[A-Z]$",
      "learned": "Single letters are often generic parameters",
      "examples": ["A->fs", "B->filepath", "Q->sql"]
    },
    {
      "pattern": "^[A-Z][a-z][0-9]$",
      "learned": "Three-char pattern for classes/functions",
      "examples": ["Wu1->readFileLines", "Bx2->DatabaseConnection"]
    }
  ]
}
```

### Step 3: Apply To New Version

When version 1.0.99 comes out with slightly modified obfuscated code:

**New Version (obfuscated):**
```javascript
function Wu1(A, B, C) {  // Added parameter C
    var Q = A.readFileSync(B, C || 'utf8');  // Uses C for encoding
    var Z = Q.split('\n');
    return Z.map(function(G) {
        return G.trim();
    });
}

function Xy3(A) {  // New function
    return Wu1(A, './config.json', 'utf8');
}
```

**Tool Automatically Applies Learned Mappings:**
```javascript
function readFileLines(fs, filepath, encoding) {  // C -> encoding (new param)
    var content = fs.readFileSync(filepath, encoding || 'utf8');
    var lines = content.split('\n');
    return lines.map(function(line) {
        return line.trim();
    });
}

function Xy3(fs) {  // Pattern suggests function, A -> fs
    return readFileLines(fs, './config.json', 'utf8');
}
```

### Step 4: Incremental Manual Review

The tool shows you what it couldn't map with confidence:

```
UNMAPPED SYMBOLS:
- Xy3: New function, suggested: "Function_Xy3" (confidence: 0.3)
  Context: Calls readFileLines with config.json
  Suggestion: "readConfigLines" based on usage

REVIEW NEEDED:
- Parameter C in Wu1: Mapped to "encoding" (confidence: 0.7)
  Reason: Appears to be encoding parameter for readFileSync
```

You only need to manually fix the new/uncertain parts:
```javascript
function readConfigLines(fs) {  // Manual: Better name for Xy3
    return readFileLines(fs, './config.json', 'utf8');
}
```

### Step 5: Tool Learns From Your Corrections

The tool adds your manual correction to its mapping database:
```json
{
  "Xy3": { "mapped": "readConfigLines", "type": "Function", "confidence": 1.0 }
}
```

## Key Benefits

### 1. Minimal Manual Work Per Version
- Version 1.0.98: Manual decompile 100 functions
- Version 1.0.99: Tool maps 95, you fix 5
- Version 1.1.0: Tool maps 98, you fix 2

### 2. Preserves Your Naming Choices
Your careful naming decisions are preserved and applied consistently:
- `Wu1` always becomes `readFileLines`
- `A` in file context always becomes `fs`
- `Q` in database context always becomes `sql`

### 3. Context-Aware Mapping
Same symbol can map differently based on context:
```
In Wu1: A -> fs, Q -> content
In Bx2: A -> connection, Q -> sql
```

### 4. Pattern Learning
The tool learns your naming patterns:
- Functions that read files: `read*`
- Database classes: `*Connection`
- React components: `*Component`
- Event handlers: `handle*`

## Practical Usage

```csharp
// Initial learning from your manual work
var mapper = new DecompilationMapper();

// Learn from each manually edited file
await mapper.LearnMappings("obfuscated/file1.js", "clean/file1.js");
await mapper.LearnMappings("obfuscated/file2.js", "clean/file2.js");
// ... for all your manually edited files

// When new version arrives
string newObfuscated = await File.ReadAllTextAsync("v1.0.99/cli.js");
string autoMapped = await mapper.ApplyMappings("v1.0.99/cli.js");

// Save for manual review
await File.WriteAllTextAsync("v1.0.99/cli-automapped.js", autoMapped);

// After your manual corrections, learn from them
await mapper.LearnMappings("v1.0.99/cli.js", "v1.0.99/cli-final.js");

// Generate report
var report = mapper.GenerateReport();
Console.WriteLine(report);
```

## The Magic: Structural Matching

The tool doesn't just do naive string replacement. It matches structures:

1. **Function Signature Matching**: Parameter count, body structure
2. **Control Flow Matching**: if/else/for/while patterns
3. **String Literal Matching**: Error messages, keys, etc.
4. **Call Pattern Matching**: What functions call what
5. **Variable Usage Matching**: How variables are used

This means even if the obfuscator changes `Wu1` to `Zx9` in the next version, the tool can still match it to `readFileLines` based on its structure and behavior.

## Result: 95% Automation

After the initial manual decompilation investment:
- New versions require <5% manual work
- Your naming conventions are preserved
- Context-aware mapping prevents errors
- Each version improves the mapping database

This is the correct approach - let the tool learn from your high-quality manual work, not try to guess names automatically.