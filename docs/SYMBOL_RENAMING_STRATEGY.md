# Symbol Renaming Strategy for Claude Code Decompilation

## Overview

This document outlines the systematic approach for renaming 6,100 obfuscated modules and 5,299 methods across 685 classes in the Claude Code v1.0.98 webpack bundle. The strategy prioritizes high-impact renames that will maximize pattern learning for automated decompilation of future versions.

## Renaming Principles

### 1. Consistency Over Perfection
- Use consistent naming patterns even if not 100% certain
- Same pattern → same naming convention
- Document uncertainty for later revision

### 2. Context-Aware Naming
- Same symbol can have different names in different contexts
- Track context boundaries (class, module, function scope)
- Preserve contextual relationships

### 3. Structural Over Literal
- Focus on what code DOES not what it's called
- Use behavior patterns for naming
- Ignore misleading variable names

### 4. Progressive Refinement
- Start with obvious high-confidence renames
- Build on established patterns
- Refine ambiguous cases later

## Symbol Categories & Naming Conventions

### Core Infrastructure Classes
**Pattern Recognition**: Heavy method count, widespread usage, framework patterns

| Obfuscated | Suggested Name | Reasoning |
|------------|---------------|-----------|
| Ct1 (254 methods) | ApplicationCore | Central orchestrator, manages app lifecycle |
| B5 (209 methods) | CommandProcessor | Processes tool commands and operations |
| jIB (191 methods) | StateManager | Complex state management with subscriptions |
| J3 (187 methods) | EventDispatcher | Event handling and distribution system |
| wDB (166 methods) | DataStore | Data persistence and caching layer |

### React Component Classes
**Pattern Recognition**: render(), setState(), componentDidMount patterns

| Pattern | Naming Convention | Example |
|---------|------------------|---------|
| Has render() + state | *Component | EditorComponent |
| Has hooks (useState) | *Hook or use* | useEditorState |
| Has JSX createElement | *View or *Panel | ToolPanel |
| Event handlers | handle* methods | handleClick |

### Tool Implementation Classes
**Pattern Recognition**: Matches known Claude Code tools

| Tool Signature | Class Name | Key Methods |
|---------------|------------|-------------|
| FileRead patterns | FileReadTool | read(), readSync() |
| FileWrite patterns | FileWriteTool | write(), writeSync() |
| Bash execution | BashExecutor | execute(), run() |
| Search patterns | SearchTool | grep(), glob() |
| Web operations | WebFetcher | fetch(), search() |

### Utility Classes
**Pattern Recognition**: Static methods, pure functions, no state

| Pattern | Naming Convention | Example |
|---------|------------------|---------|
| String operations | StringUtils | format(), parse() |
| Array operations | ArrayUtils | map(), filter() |
| Path operations | PathUtils | join(), resolve() |
| Date operations | DateUtils | format(), parse() |
| Validation | *Validator | validate(), check() |

### Data Model Classes
**Pattern Recognition**: Constructor with properties, getters/setters

| Pattern | Naming Convention | Example |
|---------|------------------|---------|
| Has id property | *Model | UserModel |
| DTO pattern | *Data or *DTO | RequestData |
| Configuration | *Config | AppConfig |
| Options object | *Options | ToolOptions |

## Method Naming Patterns

### Common Method Prefixes
```
get*     - Retrieves value without side effects
set*     - Updates value with side effects
is*      - Boolean check, no side effects
has*     - Boolean existence check
add*     - Adds item to collection
remove*  - Removes item from collection
update*  - Modifies existing item
create*  - Factory method creating new instance
init*    - Initialization method
load*    - Loads data from external source
save*    - Persists data to external source
handle*  - Event handler
on*      - Event listener
emit*    - Event emitter
dispatch* - Sends message/event
process* - Transforms data
validate* - Checks validity
parse*   - Converts string to structured data
format*  - Converts data to string
render*  - Generates UI output
```

### Common Method Suffixes
```
*Sync    - Synchronous version
*Async   - Asynchronous version
*Internal - Private/internal use
*Impl    - Implementation detail
*Helper  - Helper method
*Util    - Utility method
```

## Renaming Workflow

### Phase 1: High-Confidence Core (Week 1)
1. **Identify Entry Points**
   - Main application class
   - Command router/dispatcher
   - Tool initialization

2. **Trace Execution Flow**
   - Follow main → init → command flow
   - Map critical path classes
   - Document relationships

3. **Rename Core Classes**
   - Start with highest method count
   - Use behavior analysis
   - Cross-reference with known tools

### Phase 2: Pattern Expansion (Week 2)
1. **Apply Learned Patterns**
   - Use Phase 1 patterns
   - Batch similar classes
   - Validate consistency

2. **Tool System Mapping**
   - Match against known tools
   - FileRead, FileWrite, Bash, etc.
   - Document tool boundaries

3. **React Component Hierarchy**
   - Identify component tree
   - Map parent-child relationships
   - Name by UI function

### Phase 3: Completeness (Week 3)
1. **Utility Classification**
   - Group remaining utilities
   - Apply naming conventions
   - Document uncertainties

2. **Edge Cases**
   - Handle ambiguous classes
   - Document split decisions
   - Mark for review

3. **Validation Pass**
   - Check naming consistency
   - Verify relationships
   - Test pattern recognition

## Pattern Learning Optimization

### High-Value Patterns to Establish
1. **Namespace Patterns**
   - Tool.* for tool classes
   - UI.* for interface classes
   - Core.* for infrastructure
   - Utils.* for utilities

2. **Inheritance Patterns**
   - Base* for base classes
   - Abstract* for abstracts
   - I* for interfaces (if applicable)
   - *Impl for implementations

3. **Functional Patterns**
   - *Service for services
   - *Manager for managers
   - *Controller for controllers
   - *Handler for handlers

### Context Mapping Rules
```javascript
// Example context-aware mapping
{
  "A": {
    "in_class_Wu1": "props",      // React component context
    "in_class_Bx2": "connection",  // Database context
    "in_class_Ct1": "instance",    // Core context
    "global": "argument"           // Default context
  },
  "Q": {
    "in_database": "query",
    "in_promise": "result",
    "in_stream": "queue",
    "default": "data"
  }
}
```

## Validation Criteria

### Rename Quality Metrics
- **Consistency Score**: Same patterns → same names (target: >95%)
- **Context Accuracy**: Correct context application (target: >90%)
- **Behavioral Match**: Name matches functionality (target: >85%)
- **Learning Efficiency**: Patterns applicable to new versions (target: >90%)

### Red Flags to Avoid
- ❌ Mixing naming conventions within same category
- ❌ Using obfuscated names in renames (keeping 'A', 'B', etc.)
- ❌ Overly generic names (Thing, Stuff, Data)
- ❌ Inconsistent capitalization
- ❌ Breaking established patterns

## Documentation Requirements

### For Each Renamed Module
```markdown
## Original: Wu1
## Renamed: ReactStateManager
## Confidence: 0.95
## Reasoning: 
- Contains setState, getState methods
- Has React lifecycle patterns
- Manages component state
## Context: React state management
## Patterns:
- State management → *StateManager
- React specific → React* prefix
```

### Pattern Documentation
```markdown
## Pattern: Tool Implementation
## Recognition: 
- Has execute() or run() method
- Interacts with system resources
- Matches known tool signatures
## Naming: *Tool or *Executor
## Examples: FileReadTool, BashExecutor
## Confidence: 0.9
```

## Success Metrics

### Phase 2 Completion
- [ ] 100 core modules renamed with high confidence
- [ ] 500+ methods properly mapped to renamed classes  
- [ ] 10+ naming patterns documented
- [ ] Learning system trained on manual edits

### Automation Validation
- [ ] Test on 10% sample of remaining modules
- [ ] Achieve >80% acceptable automated suggestions
- [ ] Document pattern mismatches for refinement
- [ ] Validate context-aware mapping accuracy

### Version 1.0.99 Target
- [ ] <5% manual intervention required
- [ ] <1 hour for version upgrade
- [ ] Preserved naming consistency
- [ ] No regression in existing mappings

## Tools & Resources

### Analysis Tools
- `hoho decomp analyze` - Structural analysis
- `hoho decomp symbol-map` - Symbol extraction
- `hoho decomp learn` - Pattern learning
- `hoho decomp apply` - Automated application

### Reference Materials
- Claude Code documentation (for tool names)
- React patterns guide
- JavaScript naming conventions
- Webpack bundle structure

### Collaboration
- Symbol mapping spreadsheet
- Pattern discussion forum
- Code review process
- Version control for edits

## Risk Mitigation

### Common Pitfalls
1. **Over-Renaming**: Don't rename uncertain cases
2. **Pattern Breaking**: Maintain consistency even if imperfect
3. **Context Loss**: Always preserve context boundaries
4. **Scope Creep**: Focus on high-impact renames first

### Backup Strategy
- Version control all manual edits
- Document decision rationale
- Keep original mappings
- Enable rollback capability

## Conclusion

Success depends on establishing strong, consistent patterns in the first 100 manual renames. These patterns will train the learning system to handle 95%+ of future work automatically. Focus on high-confidence, high-impact renames that clearly demonstrate behavioral patterns rather than trying to perfectly rename everything.

The goal is not perfect deobfuscation but rather efficient, automated deobfuscation that preserves functionality understanding while dramatically reducing manual effort for future versions.