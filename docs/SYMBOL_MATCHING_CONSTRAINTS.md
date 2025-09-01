# Symbol Matching Constraints & Requirements

## Core Principle: Structural, Not Literal

The HOHO decompilation system uses **AST-based structural matching** through Tree-sitter, making it robust and flexible. The system matches code patterns and relationships, not exact text strings.

## What You DON'T Need to Worry About ✅

### 1. Whitespace & Formatting
The system completely ignores formatting differences:

```javascript
// ALL OF THESE ARE EQUIVALENT for matching:

// Minified
function Wu1(A,B){var Q=A.read();return Q}

// Expanded
function Wu1(A, B) {
    var Q = A.read();
    return Q;
}

// Different style
function Wu1(
    A,
    B
) {
    var Q = A.read();
    
    return Q;
}
```

### 2. Comments
Add as many comments as you want:

```javascript
// Original
function Wu1(A) { return A.read(); }

// With comments (still matches perfectly)
/**
 * Reads file content from the filesystem
 * @param {FileSystem} fs - The filesystem object
 * @returns {string} The file content
 */
function readFile(fs) {
    // Use the filesystem to read
    return fs.read(); // Returns content
}
```

### 3. Variable Names Inside Functions
Internal variable renames are fine:

```javascript
// Original
function Wu1(A) {
    var Q = A.read();
    var Z = Q.split('\n');
    return Z;
}

// Your rename (matches perfectly)
function readFileLines(filesystem) {
    var content = filesystem.read();
    var lines = content.split('\n');
    return lines;
}
```

### 4. Code Style Preferences
Use any JavaScript style:

```javascript
// All equivalent for matching:

// Arrow function
const handler = (event) => process(event);

// Traditional function
const handler = function(event) { return process(event); };

// Verbose
const handler = function handleEvent(event) {
    return process(event);
};
```

### 5. Import/Export Syntax Variations
Different module styles are handled:

```javascript
// CommonJS
module.exports = { myFunc };

// ES6
export { myFunc };

// Default export
export default myFunc;

// All map to the same semantic export
```

## What You MUST Preserve ⚠️

### 1. Semantic Behavior
Don't change what the code DOES:

```javascript
// ❌ BAD: Changed behavior
function Wu1(A) { return A.read(); }        // Original: sync read
function readFile(fs) { return fs.readSync(); }  // Wrong: different method!

// ✅ GOOD: Same behavior
function Wu1(A) { return A.read(); }        // Original
function readFile(fs) { return fs.read(); }     // Correct: same method
```

### 2. Function Signatures
Keep the same parameter count and order:

```javascript
// ❌ BAD: Changed signature
function Wu1(A, B) { }                    // Original: 2 params
function readFile(fs, path, encoding) { } // Wrong: 3 params!

// ✅ GOOD: Same signature  
function Wu1(A, B) { }                    // Original
function readFile(fs, options) { }        // Correct: still 2 params
```

### 3. Control Flow Structure
Maintain the same control flow:

```javascript
// ❌ BAD: Added else block
if (A) { B(); }                           // Original: just if
if (isValid) { process(); } else { skip(); } // Wrong: added else!

// ✅ GOOD: Same structure
if (A) { B(); }                           // Original
if (isValid) { process(); }               // Correct: just if
```

### 4. Class Hierarchies
Preserve inheritance relationships:

```javascript
// ❌ BAD: Changed inheritance
class Wu1 extends B5 { }                  // Original: extends B5
class FileReader { }                      // Wrong: no inheritance!

// ✅ GOOD: Preserved inheritance
class Wu1 extends B5 { }                  // Original
class FileReader extends BaseReader { }   // Correct: still extends
```

### 5. Method Call Relationships
Keep the same call patterns:

```javascript
// ❌ BAD: Changed call target
function Wu1() { Bx2.process(); }         // Original: calls Bx2
function initialize() { startup(); }      // Wrong: different call!

// ✅ GOOD: Preserved relationship
function Wu1() { Bx2.process(); }         // Original
function initialize() { DatabaseConnection.process(); } // Correct: renamed but same relationship
```

## Context-Aware Mapping Rules

### How Context Works
The same symbol maps differently based on where it appears:

```javascript
// Symbol 'A' in different contexts:
class Wu1 {
    constructor(A) {  // A → props (in Wu1 constructor)
        this.props = A;
    }
}

class Bx2 {
    constructor(A) {  // A → connection (in Bx2 constructor)
        this.connection = A;
    }
}

function process(A) { // A → data (in global function)
    return A.map();
}
```

### Context Hierarchy
The system tracks context at multiple levels:

1. **Global Context**: Top-level functions and classes
2. **Class Context**: Methods within a specific class
3. **Method Context**: Parameters and variables within methods
4. **Block Context**: Variables within specific blocks

```javascript
// Example context tracking:
{
  "A": {
    "global": "argument",
    "Wu1": "props",
    "Wu1.constructor": "initialProps",
    "Wu1.render": "renderArgs",
    "Bx2": "connection",
    "Bx2.query": "queryString"
  }
}
```

## Pattern Matching Capabilities

### Structural Patterns Recognized

#### 1. Function Patterns
```javascript
// The system recognizes these as the same pattern:
function Xx1(A, B) { return A + B; }
function add(first, second) { return first + second; }
// Pattern: binary operation function
```

#### 2. Class Patterns
```javascript
// These match the same pattern:
class Yy2 {
    constructor(A) { this.data = A; }
    process() { return this.data; }
}

class DataProcessor {
    constructor(input) { this.data = input; }
    process() { return this.data; }
}
// Pattern: data container class
```

#### 3. Module Patterns
```javascript
// These are recognized as the same:
var Zz3 = U((exports) => {
    exports.func = () => {};
});

var UtilityModule = U((moduleExports) => {
    moduleExports.func = () => {};
});
// Pattern: CommonJS module wrapper
```

## Advanced Matching Features

### 1. Fuzzy Structural Matching
The system handles minor structural variations:

```javascript
// Original has early return
function Wu1(A) {
    if (!A) return null;
    return A.process();
}

// Your version with guard clause (matches!)
function processData(input) {
    if (!input) {
        return null;
    }
    return input.process();
}
```

### 2. Pattern Learning
The system learns your naming patterns:

```javascript
// After seeing several examples:
Wu1 → FileReader
Bx2 → DatabaseConnection
Qz3 → NetworkClient

// It learns the pattern:
// [A-Z][a-z][0-9] → PascalCase descriptive class name
```

### 3. Cross-Version Tracking
Handles symbol changes between versions:

```javascript
// Version 1.0.98
function Wu1() { return "data"; }

// Version 1.0.99 (symbol changed)
function Xy9() { return "data"; }

// System recognizes: Wu1 → Xy9 (same structure, same string literal)
```

## Validation & Safety

### Automatic Validation Checks

The system performs these checks automatically:

1. **Reference Integrity**: All renamed symbols still reference correctly
2. **Call Graph Preservation**: Function calls maintain relationships  
3. **Type Compatibility**: Renamed methods match expected signatures
4. **Scope Preservation**: Variable scopes remain valid

### Manual Validation Points

You should verify:

1. **Semantic Correctness**: Names accurately describe functionality
2. **Consistency**: Similar patterns use similar names
3. **Domain Accuracy**: Business logic names make sense
4. **Readability**: Code is more understandable after renaming

## Common Pitfalls & Solutions

### Pitfall 1: Over-Renaming
**Problem**: Renaming uncertain symbols with low confidence
**Solution**: Leave uncertain symbols for later passes

### Pitfall 2: Breaking Patterns
**Problem**: Using different names for same pattern
**Solution**: Document and follow naming conventions

### Pitfall 3: Losing Context
**Problem**: Generic names that lose specific meaning
**Solution**: Use descriptive, context-aware names

### Pitfall 4: Changing Behavior
**Problem**: "Improving" code during renaming
**Solution**: Only rename, never refactor

## Best Practices Summary

### DO ✅
- Preserve all structural relationships
- Use consistent naming patterns
- Document uncertain decisions
- Maintain parameter order and count
- Keep inheritance hierarchies
- Add helpful comments
- Format code for readability

### DON'T ❌
- Change method behaviors
- Modify control flow
- Add or remove parameters
- Break method calls
- Mix naming conventions
- Refactor during renaming
- Guess at uncertain symbols

## Example: Perfect Rename

```javascript
// ORIGINAL (obfuscated):
var Wu1=U((bnB)=>{
  var pA1=Symbol.for("react.element"),qnB=Symbol.for("react.portal");
  function ynB(A){
    if(A===null||typeof A!=="object")return null;
    return A=bP0&&A[bP0]||A["@@iterator"],typeof A==="function"?A:null
  }
  var gP0={
    isMounted:function(){return!1},
    enqueueForceUpdate:function(){},
    enqueueReplaceState:function(){},
    enqueueSetState:function(){}
  };
  bnB.exports={Queue:gP0,getIter:ynB,ELEM:pA1,PORT:qnB};
});

// YOUR RENAME (perfect):
var ReactCoreModule = U((exports) => {
  // React element type symbols
  var REACT_ELEMENT_TYPE = Symbol.for("react.element"),
      REACT_PORTAL_TYPE = Symbol.for("react.portal");
  
  /**
   * Gets the iterator function from a potentially iterable object
   * @param {any} maybeIterable - Object that might be iterable
   * @returns {Function|null} Iterator function or null
   */
  function getIteratorFn(maybeIterable) {
    if (maybeIterable === null || typeof maybeIterable !== "object") {
      return null;
    }
    // Note: bP0 likely represents Symbol.iterator
    return maybeIterable = SYMBOL_ITERATOR && maybeIterable[SYMBOL_ITERATOR] || 
           maybeIterable["@@iterator"], 
           typeof maybeIterable === "function" ? maybeIterable : null;
  }
  
  // React's no-op update queue for components
  var ReactNoopUpdateQueue = {
    isMounted: function() { return false; },
    enqueueForceUpdate: function() {},
    enqueueReplaceState: function() {},
    enqueueSetState: function() {}
  };
  
  exports.exports = {
    Queue: ReactNoopUpdateQueue,
    getIter: getIteratorFn,
    ELEM: REACT_ELEMENT_TYPE,
    PORT: REACT_PORTAL_TYPE
  };
});
```

This rename:
- ✅ Preserves all structure
- ✅ Maintains exports object
- ✅ Keeps function signatures
- ✅ Uses meaningful names
- ✅ Adds helpful comments
- ✅ Improves readability
- ✅ Documents uncertainties (bP0 note)

The learning system will map:
- `Wu1` → `ReactCoreModule`
- `bnB` → `exports`
- `pA1` → `REACT_ELEMENT_TYPE`
- `qnB` → `REACT_PORTAL_TYPE`
- `ynB` → `getIteratorFn`
- `gP0` → `ReactNoopUpdateQueue`
- `A` → `maybeIterable` (in ynB context)

And apply these mappings with high confidence to similar patterns in future versions!