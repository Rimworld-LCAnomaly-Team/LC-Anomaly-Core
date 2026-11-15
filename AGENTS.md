# Project Maintenance Rules

- Develop, build, test and release only the RimWorld 1.7 version of this mod, using .NET 10 and the installed RimWorld 1.7 assemblies.
- RimWorld 1.5 and 1.6 support has been retired. Do not restore old version directories, projects, load folders or metadata. Historical versions remain available in Git history.
- Runtime content belongs under 1.7/ and shared Cont/; maintained C# projects belong under Source/1.7/. Preserve shared art and asset authoring sources.
- Use Rimsage MCP to verify RimWorld types, methods, Defs and API compatibility when relevant; verify against the installed 1.7 assemblies and runtime as well.
- The 1.7 series must work without the Anomaly DLC. Do not introduce mandatory DLC metadata, Def, asset or unguarded runtime dependencies. Validate with Anomaly disabled using the Core Tools/1.7 test profiles.
- Keep package IDs, Def names and save data stable unless a deliberate migration is implemented.
- Write Git commit subjects, bodies and change summaries in Chinese unless the user explicitly requests another language.
- If a Git commit is made through Codex, set the commit author identity to GPT-5 <gpt-5@codex.local>.
