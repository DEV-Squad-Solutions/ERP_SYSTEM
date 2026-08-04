## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

## C# DTO construction

- Use named arguments when manually constructing response DTOs, report rows, summaries, or other records that contain multiple primitive values, especially adjacent values of the same type such as `Count`, `Weight`, `Quantity`, `Price`, and `Total`.
- Do not rely on positional argument order for financial, quantity, pagination, currency, or date fields. The source property and target parameter name must be visible at the construction site.
- Named arguments are not supported inside LINQ expression trees. For EF Core projections, use an anonymous type or a member-initialized projection with explicit property names instead of a long positional constructor.
- Add property-level regression assertions for fields that could be swapped while still compiling.

## Automatic multi-agent routing

For complex tasks, automatically delegate independent work:

- Use `luna_explorer` for code exploration, relationship tracing,
  documentation research, test-log analysis, and other read-only work.
- Use `sol_worker` for architecture decisions, implementation,
  concurrency, financial logic, migrations, and difficult debugging.
- Luna must not edit files.
- Sol owns code modifications.
- Run independent investigations in parallel.
- Wait for all agents and combine their results before the final response.
- Do not use multiple write agents on overlapping files.
