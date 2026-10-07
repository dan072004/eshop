- All skills were powerful. I ended up with a spec and stories in less than 1-2 hours.
- I didn't spent much time in grilling - accepted suggestions - quite good.
- I was able to run one feature in isolated env - sandcastle pretty fast (luckily claude fixed errors and docker image for me)
- Task finished in like 7-10 minutes.
- However I noticed a business problem: The final order total was changed when display - this should never happen.
Lesson learned: be careful with grilling. Feed code review issues, back into the loop, adding to rules.

Skills
 - Vs Skills - CLAUDE.md loads into every conversation and is best for always-on project standards. Skills load on demand and are best for task-specific expertise
 - Vs subagents - Subagents run in isolated execution contexts
 - Vs Hooks - Hooks are event-driven (fire on file saves, tool calls). Skills are request-driven (activate based on what you're asking)
Summary
 - CLAUDE.md — always-on project standards
 - Skills — task-specific expertise that loads on demand
 - Hooks — automated operations triggered by events
 - Subagents — isolated execution contexts for delegated work
 - MCP servers — external tools and integrations
 
!Skills in subagents
 - Built-in agents (like Explorer, Plan, and Verify) can't access skills at all
 - Custom subagents you define can use skills, but only when you explicitly list them
 - Skills are loaded when the subagent starts, not on demand like in the main conversation
