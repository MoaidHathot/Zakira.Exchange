# Zakira.Exchange Agent Skills

This directory contains [Agent Skills](https://agentskills.io/) - a
portable, open format for teaching AI agents how to use Zakira.Exchange
well.

Skills are simple folders containing a `SKILL.md` file. Agent clients
that support skills (opencode, Claude Code, Cursor, VS Code, Goose,
Codex, Gemini CLI, and many more) load the skill metadata at startup
and pull in the full instructions only when a task matches.

## Skills in this directory

| Skill                                 | What it teaches                                                                                                                                |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| [`zakira-exchange/`](./zakira-exchange/SKILL.md) | When and how to call the Zakira MCP tools, how to write good memory entries, how to search effectively, naming conventions, and how to install/configure the server in an MCP client. |

The skill is organised as a small `SKILL.md` map that links to focused
files under `references/`, so agents only load the bits they need.

## Installing a skill in your agent

Each agent has its own convention. The common ones:

### opencode

Point `skills.paths` at this directory in `opencode.json`:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "skills": {
    "paths": ["./skills"]
  }
}
```

Or copy `zakira-exchange/` into `~/.config/opencode/skills/`.

### Claude Code / Claude Desktop

Copy or symlink `zakira-exchange/` into `~/.claude/skills/`.

### Other clients

See the [agentskills.io client showcase](https://agentskills.io/) for
the per-client install path. Most clients accept either a project-local
`./skills/` directory or a global skills directory.

## Contributing

Skills here are versioned with the project. If you're proposing a
change:

- Keep `SKILL.md` short - it's loaded for every session that has the
  matching tools. Put detail in `references/`.
- Keep the `description` frontmatter precise about *when* to activate;
  front-load the literal keywords (tool names, "Zakira", etc.) an
  agent or user is likely to say.
- Cross-link reference files from `SKILL.md` so progressive disclosure
  actually works.
- Match the existing docs voice: present tense, concise, no emoji.

For the full Agent Skills specification, see
<https://agentskills.io/specification>.
