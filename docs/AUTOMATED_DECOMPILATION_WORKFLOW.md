# Automated Decompilation Workflow Guide

## Overview

This guide documents the complete workflow for using the HOHO decompilation system to process obfuscated Claude Code releases, from initial extraction through automated symbol renaming.

## System Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│ Obfuscated      │────▶│ Module           │────▶│ Symbol          │
│ cli.js Bundle   │     │ Extractor        │     │ Extractor       │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │                         │
                               ▼                         ▼
                        ┌──────────────────┐     ┌─────────────────┐
                        │ 6,100 Modules    │     │ Symbol Map      │
                        │ (original/)      │     │ (685 classes)   │
                        └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │ Manual Renaming  │◀──── YOU ARE HERE
                        │ (manual/)        │
                        └──────────────────┘
                               │
                               ▼
                        ┌──────────────────┐     ┌─────────────────┐
                        │ Learning System  │────▶│ Symbol Mapping  │
                        │ (Mapper)         │     │ Database        │
                        └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │ Automated        │
                        │ Application      │
                        │ (95%+ auto)      │
                        └──────────────────┘
```

## Phase 1: Initial Extraction

### Step 1.1: Download Claude Code
```bash
# Download the CLI (adjust version as needed)
curl -O https://example.com/claude-code-v1.0.98.js
mv claude-code-v1.0.98.js ~/original-bundles/
```

### Step 1.2: Extract Modules
```bash
# Extract all modules from the webpack bundle
hoho decomp extract ~/original-bundles/claude-code-v1.0.98.js --version 1.0.98

# This creates:
# decomp/claude-code-dev/versions/1.0.98/
#   ├── original/          (6,100 extracted modules)
#   ├── manual/           (templates for editing)
#   └── symbol-map.md     (class-method mappings)
```

### Step 1.3: Verify Extraction
```bash
# Check extraction results
hoho decomp status --version 1.0.98

# Output:
# Version: 1.0.98
# Modules: 6,100
# Classes: 685
# Methods: 5,299
# Status: Ready for manual renaming
```

## Phase 2: Manual Renaming (Initial Investment)

### Step 2.1: Identify High-Value Targets
```bash
# List top complex classes for priority renaming
hoho decomp analyze --version 1.0.98 --top 20

# Output:
# 1. Ct1 - 254 methods
# 2. B5 - 209 methods  
# 3. jIB - 191 methods
# ...
```

### Step 2.2: Rename Core Modules
Edit files in `decomp/claude-code-dev/versions/1.0.98/manual/modules/`:

```javascript
// Example: Edit Ct1.js
// Original:
class Ct1 {
  _A0(B) { return B.C1(); }
  _A1(B, Q) { B.D2(Q); }
}

// Your rename:
class ApplicationCore {
  _initialize(context) { return context.setup(); }
  _processCommand(context, command) { context.execute(command); }
}
```

### Step 2.3: Follow Renaming Best Practices

#### ✅ DO:
- Preserve structure and behavior
- Use consistent naming patterns
- Document uncertain renames
- Maintain parameter count
- Keep relationships intact

#### ❌ DON'T:
- Change functionality
- Modify control flow
- Add/remove parameters
- Break method calls
- Mix naming conventions

## Phase 3: Learning from Manual Work

### Step 3.1: Train the Learning System
```bash
# Learn from your manual edits (individual file)
hoho decomp learn \
  decomp/claude-code-dev/versions/1.0.98/original/modules/Ct1.js \
  decomp/claude-code-dev/versions/1.0.98/manual/modules/Ct1.js

# Or learn from entire directory
hoho decomp learn-dir --version 1.0.98
```

### Step 3.2: Review Learned Mappings
```bash
# Show what the system learned
hoho decomp mappings --version 1.0.98

# Output:
# Learned Mappings:
# Ct1 → ApplicationCore (confidence: 0.95)
# _A0 → _initialize (context: Ct1)
# _A1 → _processCommand (context: Ct1)
# B → context (context: Ct1._A0)
# Q → command (context: Ct1._A1)
```

### Step 3.3: Test Pattern Recognition
```bash
# Test on a sample of unmapped modules
hoho decomp test-patterns --version 1.0.98 --sample 10

# Output:
# Pattern Match Rate: 87%
# Suggested improving patterns for:
# - Event handlers (add more examples)
# - Data models (needs context)
```

## Phase 4: Automated Application

### Step 4.1: Process New Version
```bash
# When version 1.0.99 is released
hoho decomp extract ~/original-bundles/claude-code-v1.0.99.js --version 1.0.99

# Apply learned mappings automatically
hoho decomp apply --from 1.0.98 --to 1.0.99
```

### Step 4.2: Review Automated Results
```bash
# Check automation success rate
hoho decomp compare --version1 1.0.98 --version2 1.0.99

# Output:
# Total Symbols: 6,234
# Automatically Mapped: 5,912 (94.8%)
# New/Unknown: 322 (5.2%)
# Confidence Levels:
#   High (>0.9): 5,102
#   Medium (0.6-0.9): 810
#   Low (<0.6): 322
```

### Step 4.3: Manual Review & Correction
```bash
# List symbols needing manual review
hoho decomp review --version 1.0.99 --confidence-below 0.7

# Edit the uncertain mappings
# Then re-learn from corrections
hoho decomp learn-corrections --version 1.0.99
```

## Phase 5: Continuous Improvement

### Step 5.1: Export Deobfuscated Code
```bash
# Generate fully renamed version
hoho decomp export --version 1.0.99 --output ~/deobfuscated/

# Creates clean, readable code:
# ~/deobfuscated/claude-code-1.0.99/
#   ├── index.js
#   ├── core/
#   ├── tools/
#   └── components/
```

### Step 5.2: Validate Functionality
```bash
# Run basic validation tests
hoho decomp validate --version 1.0.99

# Checks:
# - All references resolved
# - No broken imports
# - Method calls intact
# - Class relationships preserved
```

### Step 5.3: Update Symbol Database
```bash
# Commit learned patterns to database
hoho decomp commit --version 1.0.99

# Database now contains:
# - All 1.0.98 mappings
# - New 1.0.99 patterns
# - Corrected uncertainties
# - Enhanced confidence scores
```

## Workflow Optimization Tips

### Batch Processing
```bash
# Process multiple files in parallel
hoho decomp learn-dir --version 1.0.98 --parallel 8

# Apply to multiple versions
hoho decomp apply --from 1.0.98 --to "1.0.99,1.1.0,1.1.1"
```

### Pattern Templates
Create reusable pattern files:
```yaml
# patterns/react-components.yaml
patterns:
  - match: "render() { return *; }"
    rename: "*Component"
  - match: "setState(*)"
    rename: "stateful*"
  - match: "componentDidMount()"
    rename: "*LifecycleComponent"
```

Apply patterns:
```bash
hoho decomp apply-patterns --version 1.0.99 --pattern patterns/react-components.yaml
```

### Incremental Learning
```bash
# Learn only from high-confidence renames first
hoho decomp learn-incremental --version 1.0.98 --min-methods 50

# Gradually expand to smaller classes
hoho decomp learn-incremental --version 1.0.98 --min-methods 10
```

## Common Workflows

### Workflow A: Fresh Start
```bash
# 1. Extract new version
hoho decomp extract claude-v1.0.98.js --version 1.0.98

# 2. Analyze structure
hoho decomp analyze --version 1.0.98

# 3. Manual rename top 100 modules
# (edit files in manual/modules/)

# 4. Learn from edits
hoho decomp learn-dir --version 1.0.98

# 5. Apply to rest
hoho decomp apply-internal --version 1.0.98
```

### Workflow B: Version Update
```bash
# 1. Extract new version
hoho decomp extract claude-v1.0.99.js --version 1.0.99

# 2. Apply previous learnings
hoho decomp apply --from 1.0.98 --to 1.0.99

# 3. Review low-confidence
hoho decomp review --version 1.0.99

# 4. Fix and re-learn
hoho decomp learn-corrections --version 1.0.99
```

### Workflow C: Pattern Refinement
```bash
# 1. Identify problem patterns
hoho decomp analyze-failures --version 1.0.99

# 2. Create better examples
# (edit specific problem cases)

# 3. Re-learn patterns
hoho decomp learn-patterns --version 1.0.99 --focus-failures

# 4. Re-apply with new patterns
hoho decomp reapply --version 1.0.99
```

## Performance Metrics

### Time Investment by Version
| Version | Manual Hours | Automated % | Total Time |
|---------|-------------|-------------|------------|
| 1.0.98 | 40h | 0% | 40h |
| 1.0.99 | 3h | 94% | 3.2h |
| 1.1.0 | 1h | 97% | 1.1h |
| 1.1.1+ | 0.5h | 98% | 0.5h |

### Quality Metrics
```bash
# Check deobfuscation quality
hoho decomp quality --version 1.0.99

# Metrics:
# - Naming Consistency: 96%
# - Context Accuracy: 93%
# - Pattern Coverage: 89%
# - Relationship Preservation: 99%
```

## Troubleshooting

### Issue: Low Pattern Match Rate
```bash
# Diagnosis
hoho decomp diagnose --version 1.0.99

# Solutions:
# 1. Add more training examples
# 2. Improve context definitions
# 3. Refine pattern templates
```

### Issue: Broken References After Renaming
```bash
# Find broken references
hoho decomp check-refs --version 1.0.99

# Auto-fix where possible
hoho decomp fix-refs --version 1.0.99
```

### Issue: Conflicting Mappings
```bash
# Resolve conflicts
hoho decomp conflicts --version 1.0.99

# Shows:
# Symbol 'A' maps to:
#   - 'props' in context Wu1 (confidence: 0.9)
#   - 'args' in context Bx2 (confidence: 0.85)
# Resolution: Use context-specific mapping
```

## Best Practices Summary

1. **Start Small**: Focus on high-value, high-confidence renames
2. **Be Consistent**: Use same patterns across similar code
3. **Document Decisions**: Note reasoning for uncertain cases
4. **Preserve Structure**: Never change code behavior
5. **Learn Iteratively**: Refine patterns based on results
6. **Version Control**: Commit mappings after each session
7. **Validate Often**: Check references haven't broken
8. **Share Knowledge**: Export patterns for team use

## Conclusion

The automated decompilation workflow transforms a 40-hour manual task into a 30-minute automated process after initial training investment. Success depends on thoughtful initial renaming that establishes clear patterns the learning system can apply to future versions.

Focus on high-impact renames, maintain consistency, and let the system handle the repetitive work. With each version, the automation improves, approaching 98%+ accuracy for routine updates.