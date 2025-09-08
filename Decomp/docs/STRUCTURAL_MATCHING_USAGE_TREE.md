# Structural Matching & Usage Tree Analysis

## Core Concept: Like IDE "Find Usages" at Scale

The HOHO decompilation system uses **structural matching** that works exactly like an IDE's "Find Usages" or "Find All References" feature, but automated across thousands of obfuscated symbols. It builds a complete reference graph showing where every symbol is defined, used, and how they relate to each other.

## How Usage Tree Walking Works

### 1. Symbol Definition Discovery
The system first identifies where symbols are defined:

```javascript
// DEFINITIONS found:
var Wu1 = U((bnB) => {...});          // Module definition
function ynB(A) {...}                  // Function definition  
class Ct1 {...}                        // Class definition
var gP0 = {...}                        // Variable definition
```

### 2. Usage Tracking
Then it finds every place those symbols are used:

```javascript
// USAGES of Wu1:
Wu1.someMethod();                      // Method call
new Wu1();                             // Constructor
extends Wu1                            // Inheritance
const x = Wu1;                         // Assignment
import Wu1                             // Import
typeof Wu1                             // Type check
Wu1 instanceof                         // Instance check
```

### 3. Reference Graph Building
Creates a complete dependency graph:

```
Wu1 (ReactModule)
├── Used by: Au1 (extends)
├── Used by: main.js (imports)
├── Calls: Bx2.initialize()
├── Contains: ynB (function)
│   └── Parameter: A → maybeIterable
├── Contains: gP0 (variable)
│   └── Referenced by: Oc.constructor
└── Exports: {getIter, Queue, ELEM}
```

## Scope-Aware Context Tracking

### The Scope Hierarchy
```
GLOBAL
├── MODULE: Wu1
│   ├── PARAMETER: bnB → "exports"
│   ├── FUNCTION: ynB
│   │   └── PARAMETER: A → "maybeIterable"
│   ├── VARIABLE: gP0 → "ReactNoopUpdateQueue"
│   └── CLASS: Oc
│       ├── CONSTRUCTOR
│       │   ├── PARAMETER: A → "props"
│       │   ├── PARAMETER: B → "context"
│       │   └── PARAMETER: Q → "updater"
│       └── METHOD: setState
│           ├── PARAMETER: A → "partialState"
│           └── PARAMETER: B → "callback"
```

### Why Scope Matters
The same identifier means different things in different scopes:

```javascript
// 'A' appears 706 times with different meanings:

// In Wu1 context: A = exports
var Wu1 = U((A) => { A.exports = {}; });

// In Oc context: A = props  
function Oc(A, B, Q) { this.props = A; }

// In ynB context: A = maybeIterable
function ynB(A) { if (A === null) return null; }

// In global function: A = data
function process(A) { return A.map(); }
```

## Reference Types & Relationships

### 1. Direct References
```javascript
Wu1.method();              // Direct call
new Wu1();                 // Constructor call
Wu1.property;              // Property access
```

### 2. Inheritance References
```javascript
class Au1 extends Wu1 {}   // Au1 inherits from Wu1
class Bv2 extends Au1 {}   // Transitive: Bv2 inherits from Wu1
```

### 3. Import/Export References
```javascript
// Module exports Wu1
module.exports = Wu1;

// Other file imports it
const Component = require('./wu1');  // Component IS Wu1
```

### 4. Call Chain References
```javascript
// Tracks complete call chains
Wu1.init() 
  → Bx2.setup()
    → Ct1.configure()
      → dP0.process()
```

## How This Enables Smart Renaming

### Pattern Recognition Through Usage
```javascript
// The system sees these patterns:
class X {
    render() { return createElement(...); }     // React component
    componentDidMount() {}                       // Lifecycle method
    setState() {}                                // State management
}
// Concludes: X is a React Component → rename to "Component"

class Y {
    execute() {}                                 // Command pattern
    canExecute() {}                             // Validation
    undo() {}                                   // Undo support
}
// Concludes: Y is a Command → rename to "Command"
```

### Relationship Preservation
```javascript
// BEFORE: Obfuscated relationships
Wu1.process() calls Bx2.handle()
Bx2.handle() calls Ct1.execute()

// AFTER: Renamed but relationships preserved
ReactModule.process() calls DatabaseConnection.handle()
DatabaseConnection.handle() calls ApplicationCore.execute()
```

## Comparison with Simple Text Replace

### Why `sd` or `sed` Fails on the Full Bundle

#### Problem 1: Scope Collision
```javascript
// Can't just replace 'A' globally:
sd 'A' 'props' cli.js  // Would replace ALL 706 instances!

// Each needs different replacement:
function Wu1(A) { }  // A → exports
function Oc(A) { }   // A → props  
function ynB(A) { }  // A → maybeIterable
```

#### Problem 2: Context Loss
```javascript
// Simple replace breaks method calls:
"Wu1.process" → "ReactModule.process"  // Correct
"data.process" → "data.process"        // Should stay unchanged
"cmd.process" → "cmd.execute"           // Different context
```

### Where Simple Replace CAN Work

#### 1. Globally Unique Classes
```bash
# These ARE unique and safe to replace globally:
sd '\bCt1\b' 'ApplicationCore' cli.js    # 254-method class
sd '\bwDB\b' 'DataStore' cli.js          # 166-method class
sd '\bjIB\b' 'StateManager' cli.js       # 191-method class
```

#### 2. Within Extracted Modules
```bash
# After extraction, within Wu1.js only:
sd '\bA\b' 'exports' Wu1.js              # Safe in module scope
sd '\bynB\b' 'getIteratorFn' Wu1.js      # Unique in module
sd '\bgP0\b' 'ReactNoopUpdateQueue' Wu1.js
```

#### 3. Batch Operations Post-Learning
```bash
#!/bin/bash
# After learning phase, generate batch renames:

# Unique classes (safe globally)
sd 'class Ct1' 'class ApplicationCore' "$1"
sd 'new Ct1' 'new ApplicationCore' "$1"
sd 'extends Ct1' 'extends ApplicationCore' "$1"

# Unique functions
sd 'function Wu1' 'function ReactModule' "$1"
```

## Building Your Own Usage Tree

### Step 1: Extract Symbol Usages
```bash
# Find all usages of a symbol
rg '\bWu1\b' cli.js > Wu1_usages.txt

# Categorize by usage type
rg 'new Wu1' cli.js         # Constructors
rg 'extends Wu1' cli.js      # Inheritance
rg 'Wu1\.' cli.js           # Method calls
rg '= Wu1' cli.js           # Assignments
```

### Step 2: Build Dependency Map
```javascript
// Track what calls what
{
  "Wu1": {
    "calls": ["Bx2.init", "Ct1.setup"],
    "calledBy": ["main", "App.constructor"],
    "extends": null,
    "extendedBy": ["Au1", "Component"]
  }
}
```

### Step 3: Apply Contextual Renaming
```javascript
// Rename based on usage patterns
if (hasRenderMethod && hasSetState) {
    rename(symbol, "Component");
} else if (hasExecuteMethod && hasCanExecute) {
    rename(symbol, "Command");
} else if (extendsError) {
    rename(symbol, symbol + "Error");
}
```

## Advanced Usage Tree Features

### 1. Transitive Dependency Resolution
```
Wu1 depends on Bx2
Bx2 depends on Ct1
Therefore: Wu1 transitively depends on Ct1
```

### 2. Circular Dependency Detection
```
Wu1 → Bx2 → Ct1 → Wu1 (circular!)
Warning: Preserve all three together
```

### 3. Dead Code Identification
```
Symbols with no usages = potentially dead code
(Unless dynamically referenced)
```

### 4. Hot Path Analysis
```
Most called functions = hot path
Rename these first for maximum clarity
```

## Practical Workflow Integration

### 1. Initial Analysis
```bash
# Generate usage report
hoho decomp analyze-usages --version 1.0.98

# Output:
# Wu1: 47 usages (15 calls, 10 constructors, 5 inherits...)
# Bx2: 23 usages (8 calls, 5 property access...)
```

### 2. Strategic Renaming
```bash
# Rename high-usage symbols first
hoho decomp rename --symbol Wu1 --to ReactModule --update-references

# System updates all 47 usages automatically
```

### 3. Validation
```bash
# Verify reference integrity
hoho decomp validate-references --version 1.0.98

# Checks:
# ✓ All method calls resolve
# ✓ All imports match exports
# ✓ No broken inheritance chains
```

## Why Structural Matching Wins

### 1. **Context Preservation**
- Knows WHERE each symbol is used
- Maintains scope boundaries
- Preserves relationships

### 2. **Pattern Recognition**
- Identifies React components
- Recognizes command patterns
- Detects state management

### 3. **Cross-Version Stability**
- Even when Wu1 becomes Zx9 in next version
- Structure remains similar
- Usage patterns stay consistent

### 4. **Automated Accuracy**
- No manual tracking needed
- Handles thousands of symbols
- Maintains consistency

## Conclusion

Structural matching with usage tree analysis is fundamentally different from simple text replacement. It understands code structure, tracks relationships, and maintains context - exactly like an IDE's refactoring tools but automated for obfuscated code at scale.

The key insight: **Don't just rename text, rename based on behavior and relationships**. This is why the system achieves 95%+ automation after initial training - it learns not just what to rename, but WHY and HOW symbols relate to each other.