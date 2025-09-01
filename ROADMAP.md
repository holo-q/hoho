### Immediate Improvements

Immediate improvements over Claude Code

* Separate ESC to interrupt and queued messages
* continue/resume last conversation by default

### hoho

What makes hoho the 'last' CLI agent

* automatically connects to other conversations through shared state files that are atomatically updated
* conversations are stored in .hoho
* has panes
* mcp manager to add from global mcp list to project dynamically (no command hell) e.g.:
	* `claude mcp` -> opens a manager for this repo
	* `/mcp` opens the same manager (existing manager in claude with more features)
* track file modification to communicate to the LLM when files have been modified outside of context, so it stays up to date
* pass the current time to LLM along with a USER.md
* all .claude config can be both in the root of the project and in .claude
* .hoho/
	* .hoho on first launch detects other agent config and asks if it should be configured as a proxy, making .holo a reference to .claude, .gemini, etc. that the code uses as the source of data, with additional config on top.
	* it provides a migration route which automatically cleans up all old agent files, asserting hoho as the master CLI agent. all files are placed into .hoho/migrated/yyyy-mm-dd/
	* extend new /commands on a per-project basis
		* .hoho/name.cmd.md individual commands
		* .hoho/name.cmd.md individual commands
		* .hoho/server.mcp.json -- individual mcps
		* .hoho/config.json -- any number of commands or mcp servers
		* .hoho/intuitions/*.txt -- intuition repertoire, any number of abstract intuitions about how to act
		* .hoho/souls/soul-1/*.txt -- additional intuition repertoire about concepts
		* .hoho/souls/soul-2/*.txt -- additional intuition repertoire about concepts
		* .hoho/souls/soul-3/*.txt -- additional intuition repertoire about concepts
	* newly detected files are asked for inclusion in message prompt
		* indexed in .hoho/cache.json
	* modify message prompt
	* modify HOHO.md order
* extend message with pre/post-processing events that can activate meta-reflection prompts, meta-agentic loop, etc.
	* use this to create data extraction pipeline so that no intuition or project knowledge is ever lost
		* message-level extraction, then global context integration intermittently
* `hoho list` command shows list of recent project paths which can be unfolded to see conversations, and search like fzf (maybe we can even integrate directly with it)
* CLAUDE.md -> HOHO.md	
* Wire prompts to keyboard shortcut
	* Quick-marks / efficient leader key registration
* Resume / go key
	* Auto-resume detection with hint
	* Auto-hint on escape
	* Auto-hint on error
* Run shell commands (hoho should function like a shell augmentation)
	* Shell mode? allowing the user to cd around, etc. instead of entering prompts
* Auto-summary insertion
	* 
* CONTEXT.md can be added to any file such that it's injected in context when the directory is ls'd or included, like a concept definition for this content partition
	* Can contain a metadata section at the top
	* Can specify the order of files in this directory so that the files are always shown in a specific order, whether ls or full content
	* The context templating can therefore present these document under a certain narrative, for example "These markdowns document our journey over the course of the project", this way the model can correctly sequence and structure its attention such as to maintain chronological consistency, causality, amendments, trajectory, etc.

### Commands

A good command requests Claude to create an artifact which incorporates context into 

### Use-cases

Ideas of things that the user should be able to setup

1. Auto-insert linter or command output into every prompt (e.g. linting)
2. Auto-import after code-edit
3. Auto-encode before code compression
4. Extract preference profile on sentiment hook

### Hooks

Based on the features above, the user may want the following event signals to handle:

1. Code edited
	- Filter on filetype
2. Context compressed
3. Conversation started
4. Prompt sent
5. Message received
6. Tool use
7. Message sentiment
	1. Disagreement initiated
	2. Disagreement resolved
8. Off-context summary

A useful thing to do is to insert intuitions or hints automatically, perhaps from an embedding database or the autoencoding set



### Ideas

Dynamic reranking of intuitions
