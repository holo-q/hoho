### Goals

Waste fewer tokens, get the job done in the least amount of tokens possible


### Immediate Improvements

Immediate improvements over Claude Code

* CLAUDE.md can reference @directory/ 
* Separate ESC to interrupt and queued messages
* Continue/resume last conversation by default
* 3rd option never ask again for command foo in any directory
* cctweaks configurations built-in
* insert newlines
* different send keys
	* send immediately (while claude is already working)
	* queue up (when claude is finished, proper queue not whatever the fake ass queue in claude code is doing)
* Immediately preview tool card before the xml tag is closed, with live feedback as it's writing
* Start multiple agents of the same type (LMFAOOO WTF ARE THEY DOING DUDE)
* Remove scroll wheel that scrolls the message history (dude je suis pu capable, y sont dans lune ben raide ahahahah)

### Verbs

Three commands come with hoho installation

* `hoho` main cli agent tui
* `ho` run a single agent operation in the repository, like `hoho` but not interactive
* `ok` for non-project version of `hoho` (personal general agent, ~/ok from anywhere and optionally can link obsidian vaults, all mcps work the same)
* `ok prompt` for single llm query

All can be used with --openrouter model, --anthropic model, --openai model etc. to immediately open into a specific model of operation

### hoho

What makes hoho the 'last' CLI agent

* Automatically connects to other conversations through shared state files that are atomatically updated
* Conversations are stored in .hoho
* side panes
	* codebase tree view (maybe we can integrate some existing treesitter thing?)
	* subagent manager
* floating panes
	* line entries that can be shown on top of the whole application in the corners to display a stack of things
	* configurable, toggled by shortcuts
	* for example...
		* a minimap preview of last 10 messages/tool
		* a minimap preview of last git diffs
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
* Interactive command flow for long artifact context splicing
	* Long artifacts that are beyond the token limit (e.g. 30k) may be manually spliced by the user page by page
	* SPACE splices the next page
	* BS erases and returns to the previous page
* x11 integration 
	* HUD drawn on top of the entire system (sticky across workspace)
	* Try doing it signal based with any custom specified binary so people can implement custom HUDs
	* Our own HUD is simple and debuggy: solid black rectangles with red text on top
* Quote messages
	* up/down in action mode navigate message history and makes it possible to quote
* Reactive UX:
	* The terminal UX runs at 60 fps and retains high performance, allowing real-time vfx and juicing in a demoscene kind of way, animating colors, characters, etc. some use cases are given below
	* Clear UX indicating that the model is working (entire program has bordered or lights up)
	* Clear UX to route human attention when awaiting on things, using video game telegraphing and signaling techniques, the same things you would see being done for invincibility blinking i-frames, etc.
	* Short burst of fast blinking pulses to direct or notify attention to UI elements
	* ASCII-space cellular automaton
		* We port the cellular automaton of the holoq.ai monument demo from typescript which allows us to do natural cellular reactive diffusion of colors and breathing coursing like electricity through the window's text, which could activate during inference/work while the agent is working
* Modular & Hackable
	* Create scripted actions that can be mapped to keys and appear in the UI contextually
		* Take the entire context and pipe it
		*
	*
* Model call to enumerate possible commands and immediately ask in rapid succession for 30+ commands what might you might wanna allow the AI to yolo call
* Dispatch prompt through a subagent worker with context up to this point (or a canon claude generates a detailed task report for null claude which is cheaper)
* The model should know every single thing that have changed between messages
	* When changing model, "the previous messages were not written by X and you are Y"
	* All project files are watched for modified date and it is notified to the LLM in the next message if those files are in context
* Reverb
	* Words that are defined in .hoho/verb/*.txt are automatically replaced and tagged like @files allowing devs to grow their own ontology and meaning
* Context insertion recommendations
	* Use haiku to predict which files in the project are relevant to the current context and suggest them
	* Topological discovery by crawling LSP reference usage network
* Some mouse support
	* Ability to click head/tail of diffs to reveal additional lines of code for more context (anywhere in context, scrolling up and still usable while agent is working)
* Intent detection
	* Use light models e.g. haiku to detect user requests
	* "Commit everything" --> auto-insert git log
		* This could be done with a scriptable trigger instead
* Multiline paste should be automatically boxed in --- ... --- and separated with empty newlines above and below, leaving the caret on newline
* Continuation
	* Some models don't wanna keep going (gpt-5)
	* Double tap enter to send a continuation prompt at random: "let's keep going", "let's roll", "keep it comin", "now we're on a roll", "lets rock n roll", "keep it groovin", "continuous on our way to the dancefloor"
* Shift-tab modes
	* Normal:
	* Plan: model cannot edit files, a prompt is injectd to specify that we're in planning mode
	* Auto-continue
* Sound effects
	* Mode 1: console ping
	* Mode 2: configurable alsa supremacy
		* sfx on fnish
		* sfx on tool
		* sfx on 
* Streaming mode toggle in configuration (no streaming = more zen)

### HUD

HUD is the subsystem which draws optimized drun style graphics on top of the entire computer, hijacking x11, wayland, etc. in the cleanest possible ways
such that any opening of hoho command centers automatically upgrades the system to an integrative development spatialization and organization scheme,
displaying information in hot-corners and sides of the screen and enabling things such as...

1. Display the list of hoho sessions
2. Signal by altering the color or showing an icon the state of the session
	* Waiting for input
	* Thinking
	* Working
	* Message about the state of the task

### Thaum

Thaum's integration


### Input

* Quick-marks / efficient leader key registration
	* Wire prompts to keyboard shortcut in realtime
	* Wire HUD minimap toggles

### UX & VFX experience

* Custom home screen with ok face in a clean context with no messages
* Impactful alien scrambling vfx like the website

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


### Cognitive States

We can have different intuition charts depending on cognitive state, however this may all be emergent from embedding/language intertopology

### Ideas

Dynamic reranking of intuitions


### Critical

Critical issues that all current CLI agents have which we need to address, indirectly is often better

- Forgets and loses design constraints and intuitions
- Forgets and loses user's soul
- Loses track of global context when codebase grows
- Project structure grows without global consideration, leading to dead code, trailing files, messy project organization, etc.

### State-based editing

The context window is refreshed more often. This prevents overreliance on context to store information about the project, and favors the extraction into specs file instead.

### Voice Integration

We will integrate voice, however it may be better to do this in a different app/library/daemon that we connect up to this project

### Worries

1. Is json the best format for mapping db
2. 
