# Real-World Test Case: Decompiling Claude Code

## Sample Obfuscated Code (From cli.js)

```javascript
// Actual snippet from Claude Code cli.js
var Wu1=U((bnB)=>{
  var pA1=Symbol.for("react.element"),
      qnB=Symbol.for("react.portal");
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
  function Oc(A,B,Q){
    this.props=A,this.context=B,this.refs=mP0,this.updater=Q||gP0
  }
  Oc.prototype.setState=function(A,B){
    if(typeof A!=="object"&&typeof A!=="function"&&A!=null)
      throw Error("setState(...): takes an object");
    this.updater.enqueueSetState(this,A,B,"setState")
  };
});

var Bx2=U((Q)=>{
  var Z=X1("fs"),G=X1("path");
  async function I1(A,B){
    var W=Z.readFileSync(G.join(A,B),"utf8");
    return W.split("\n").map(Y=>Y.trim());
  }
  Q.exports={readLines:I1};
});

class Y2Q{
  constructor(A){
    this.state={count:0,text:""};
    this.handleClick=this.handleClick.bind(this);
  }
  handleClick(){
    this.setState({count:this.state.count+1});
  }
  render(){
    return D1.createElement("div",null,
      D1.createElement("button",{onClick:this.handleClick},
        `Clicked ${this.state.count} times`
      )
    );
  }
}
```

## Step 1: Manual Decompilation (You Do This)

```javascript
// Your manually cleaned version
var ReactModule = U((exports) => {
  var REACT_ELEMENT_TYPE = Symbol.for("react.element"),
      REACT_PORTAL_TYPE = Symbol.for("react.portal");
  
  function getIteratorFn(maybeIterable) {
    if (maybeIterable === null || typeof maybeIterable !== "object") return null;
    return maybeIterable = SYMBOL_ITERATOR && maybeIterable[SYMBOL_ITERATOR] || 
           maybeIterable["@@iterator"], 
           typeof maybeIterable === "function" ? maybeIterable : null;
  }
  
  var ReactNoopUpdateQueue = {
    isMounted: function() { return false; },
    enqueueForceUpdate: function() {},
    enqueueReplaceState: function() {},
    enqueueSetState: function() {}
  };
  
  function Component(props, context, updater) {
    this.props = props;
    this.context = context;
    this.refs = emptyObject;
    this.updater = updater || ReactNoopUpdateQueue;
  }
  
  Component.prototype.setState = function(partialState, callback) {
    if (typeof partialState !== "object" && 
        typeof partialState !== "function" && 
        partialState != null) {
      throw Error("setState(...): takes an object");
    }
    this.updater.enqueueSetState(this, partialState, callback, "setState");
  };
});

var FileUtilsModule = U((exports) => {
  var fs = require("fs"), 
      path = require("path");
  
  async function readFileLines(directory, filename) {
    var content = fs.readFileSync(path.join(directory, filename), "utf8");
    return content.split("\n").map(line => line.trim());
  }
  
  exports.exports = { readLines: readFileLines };
});

class CounterComponent {
  constructor(props) {
    this.state = { count: 0, text: "" };
    this.handleClick = this.handleClick.bind(this);
  }
  
  handleClick() {
    this.setState({ count: this.state.count + 1 });
  }
  
  render() {
    return React.createElement("div", null,
      React.createElement("button", { onClick: this.handleClick },
        `Clicked ${this.state.count} times`
      )
    );
  }
}
```

## Step 2: Tool Learns The Mapping

```bash
hoho decomp learn decomp/original.js decomp/cleaned.js
```

**Mappings Learned:**
```json
{
  "Wu1": "ReactModule",
  "bnB": "exports",
  "pA1": "REACT_ELEMENT_TYPE",
  "qnB": "REACT_PORTAL_TYPE",
  "ynB": "getIteratorFn",
  "A": "maybeIterable",  // in ynB context
  "gP0": "ReactNoopUpdateQueue",
  "Oc": "Component",
  "A": "props",         // in Oc context
  "B": "context",       // in Oc context
  "Q": "updater",       // in Oc context
  "mP0": "emptyObject",
  
  "Bx2": "FileUtilsModule",
  "Q": "exports",       // in Bx2 context
  "Z": "fs",
  "G": "path",
  "I1": "readFileLines",
  "A": "directory",     // in I1 context
  "B": "filename",      // in I1 context
  "W": "content",
  "Y": "line",
  
  "Y2Q": "CounterComponent"
}
```

## Step 3: New Version Released (1.0.99)

New obfuscated code with changes:

```javascript
// Version 1.0.99 - symbols changed, new parameter added
var Zx9=U((cnB)=>{  // Wu1 -> Zx9, bnB -> cnB
  var rA1=Symbol.for("react.element"),  // pA1 -> rA1
      snB=Symbol.for("react.portal");    // qnB -> snB
  function znB(B){  // ynB -> znB, A -> B
    if(B===null||typeof B!=="object")return null;
    return B=cP0&&B[cP0]||B["@@iterator"],typeof B==="function"?B:null
  }
  // ... rest similar with different symbols
});

var Cy3=U((R)=>{  // Bx2 -> Cy3, Q -> R
  var X=X1("fs"),Y=X1("path");  // Z -> X, G -> Y
  async function J2(B,C,D){  // I1 -> J2, added parameter D for encoding
    var V=X.readFileSync(Y.join(B,C),D||"utf8");  // W -> V
    return V.split("\n").map(Z=>Z.trim());  // Y -> Z
  }
  R.exports={readLines:J2};
});
```

## Step 4: Tool Automatically Applies Mappings

```bash
hoho decomp apply decomp/v1.0.99/cli.js --output decomp/v1.0.99/cli-deobfuscated.js
```

**Result:**
```javascript
// Automatically deobfuscated based on learned mappings
var ReactModule=U((exports)=>{  // Zx9 -> ReactModule, cnB -> exports
  var REACT_ELEMENT_TYPE=Symbol.for("react.element"),  // rA1 -> REACT_ELEMENT_TYPE
      REACT_PORTAL_TYPE=Symbol.for("react.portal");    // snB -> REACT_PORTAL_TYPE
  function getIteratorFn(maybeIterable){  // znB -> getIteratorFn, B -> maybeIterable
    if(maybeIterable===null||typeof maybeIterable!=="object")return null;
    return maybeIterable=cP0&&maybeIterable[cP0]||maybeIterable["@@iterator"],
           typeof maybeIterable==="function"?maybeIterable:null
  }
  // ... rest automatically mapped
});

var FileUtilsModule=U((exports)=>{  // Cy3 -> FileUtilsModule
  var fs=require("fs"),path=require("path");  // X -> fs, Y -> path
  async function readFileLines(directory,filename,encoding){  // J2 -> readFileLines
    // D is new parameter, tool suggests "encoding" based on usage
    var content=fs.readFileSync(path.join(directory,filename),encoding||"utf8");
    return content.split("\n").map(line=>line.trim());
  }
  exports.exports={readLines:readFileLines};
});
```

## Step 5: Manual Review (Minimal)

Tool shows what needs review:

```
NEW/UNCERTAIN MAPPINGS:
1. Parameter D in readFileLines: mapped to "encoding" (confidence: 0.7)
   Context: Used as third parameter to readFileSync
   
2. Symbol cP0: Not found in mappings (new symbol)
   Context: Appears to be SYMBOL_ITERATOR replacement
   Suggestion: "SYMBOL_ITERATOR"

MANUAL REVIEW NEEDED: 2 items
AUTOMATICALLY MAPPED: 47 items (96% confidence)
```

## Performance Metrics

### Initial Version (1.0.98)
- Manual decompilation: 100% manual effort
- Time: ~8 hours for 1000 modules
- Symbols renamed: 5,000+

### Next Version (1.0.99)  
- Automatic mapping: 96% of symbols
- Manual review: 4% of symbols (new/changed only)
- Time: ~30 minutes
- Effort reduction: **94%**

### Future Versions (1.1.0+)
- Expected automation: 98%+
- Manual work: Only truly new features
- Time: ~15 minutes per version

## Key Insights

1. **Structural Matching Works**: Even when `Wu1` becomes `Zx9`, the tool recognizes it's the same React module based on structure

2. **Context Preservation**: Same symbol (`A`, `B`, `Q`) maps differently based on context (function, module, class)

3. **Pattern Learning**: Tool learns your naming conventions:
   - React modules: `*Module`
   - Components: `*Component`  
   - Utils: `*Utils`
   - Handlers: `handle*`

4. **Incremental Improvement**: Each version makes the tool smarter, reducing manual work over time

## Failure Modes & Edge Cases

### Handled Well:
- Symbol renaming between versions
- New parameters added to functions
- Modules split or combined
- Dead code elimination

### Requires Manual Intervention:
- Completely new architectural patterns
- Major refactoring (class → function)
- Renamed string literals
- Changed control flow

## Optimization Strategies

1. **Batch Learning**: Process multiple files at once
2. **Parallel Mapping**: Apply mappings concurrently
3. **Caching**: Cache structural analysis between runs
4. **Incremental Updates**: Only reanalyze changed modules
5. **Pattern Database**: Share learned patterns across projects