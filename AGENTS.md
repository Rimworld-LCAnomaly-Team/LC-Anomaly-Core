# Project Maintenance Rules

- Maintain only the RimWorld 1.7 version of this mod.
- Treat all RimWorld 1.5 and 1.6 files and directories as read-only reference material.
- Do not edit, build, regenerate, or update files under `1.5/`, `Source/1.5/`, `1.6/`, or `Source/1.6/` unless the user explicitly requests a change for that version in the current conversation.
- Apply normal code, project, assembly, XML, and asset changes only to the 1.7 version.
- Build and verify only the 1.7 projects by default, using .NET 10 and the installed RimWorld 1.7 assemblies.
- Use Rimsage MCP to verify RimWorld types, methods, Defs, and API compatibility when relevant.
- Write Git commit subjects, bodies, and change summaries in Chinese unless the user explicitly requests another language.
- If a Git commit is made through Codex, set the commit author identity to `GPT-5 <gpt-5@codex.local>`.
