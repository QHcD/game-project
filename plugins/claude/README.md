# Claude Bridge

This plugin turns the `claude` scaffold into a Codex-ready bridge for a local Claude Code MCP server.

## What it does

- Registers a Codex plugin manifest at `.codex-plugin/plugin.json`
- Adds `.mcp.json` that launches `claude mcp serve`
- Adds a local marketplace entry so the plugin can appear in Codex UI ordering

## Current status

This machine does not currently have the `claude` CLI installed, so the bridge is configured but not yet live.

## To activate it

1. Install Claude Code on this machine.
2. Authenticate with Claude Code so `claude mcp serve` can start successfully.
3. If the executable is not available as `claude` on your `PATH`, edit `./.mcp.json` and replace the `command` value with the full executable path.
4. Reload Codex after the CLI is available.

## Files

- `.codex-plugin/plugin.json`: Codex plugin manifest
- `.mcp.json`: local MCP server launch configuration
- `assets/claude-bridge.svg`: lightweight plugin icon

## Limitation

This bridge is configured around Claude Code's MCP server entrypoint. It is not a direct Anthropic API wrapper inside Codex.
