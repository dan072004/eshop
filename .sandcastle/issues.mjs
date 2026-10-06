// Issue tracker adapter for the local Markdown tickets in .scratch/ (see docs/agents/issue-tracker.md).
//
//   node .sandcastle/issues.mjs list          open, unblocked `ready-for-agent` tickets as JSON
//   node .sandcastle/issues.mjs view <id>     print one ticket
//   node .sandcastle/issues.mjs close <id>    set its Status to `done`
//
// A ticket's id is its path relative to .scratch/, e.g. `coupons/issues/01-order-totals-subtract-discounts.md`.

import { existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const ROOT = ".scratch";
const STATUS_LINE = /^\*\*Status:\*\*\s*(.+)$/m;
const BLOCKED_BY_LINE = /^\*\*Blocked by:\*\*\s*(.+)$/m;

function readTicket(id) {
  const path = join(ROOT, id);
  if (!existsSync(path)) throw new Error(`No ticket at ${path}`);
  return { path, text: readFileSync(path, "utf8") };
}

function allTickets() {
  if (!existsSync(ROOT)) return [];
  return readdirSync(ROOT, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .flatMap((feature) => {
      const dir = join(ROOT, feature.name, "issues");
      if (!existsSync(dir)) return [];
      return readdirSync(dir)
        .filter((f) => /^\d+-.+\.md$/.test(f))
        .sort()
        .map((file) => {
          const id = `${feature.name}/issues/${file}`;
          const text = readFileSync(join(dir, file), "utf8");
          const number = file.match(/^(\d+)/)[1];
          const blockedBy = (text.match(BLOCKED_BY_LINE)?.[1] ?? "").match(/\b\d+\b/g) ?? [];
          return {
            id,
            feature: feature.name,
            number,
            title: text.match(/^#\s+(.+)$/m)?.[1] ?? file,
            status: text.match(STATUS_LINE)?.[1].trim() ?? "needs-triage",
            blockedBy,
            spec: existsSync(join(ROOT, feature.name, "spec.md")) ? `${ROOT}/${feature.name}/spec.md` : null,
            body: text,
          };
        });
    });
}

function list() {
  const tickets = allTickets();
  const statusOf = new Map(tickets.map((t) => [`${t.feature}/${t.number}`, t.status]));
  const open = tickets.filter(
    (t) =>
      t.status === "ready-for-agent" &&
      t.blockedBy.every((n) => statusOf.get(`${t.feature}/${n.padStart(t.number.length, "0")}`) === "done"),
  );
  console.log(JSON.stringify(open, null, 2));
}

function close(id) {
  const { path, text } = readTicket(id);
  if (!STATUS_LINE.test(text)) throw new Error(`${path} has no **Status:** line`);
  writeFileSync(path, text.replace(STATUS_LINE, "**Status:** done"));
  console.log(`Closed ${id}`);
}

const [command, id] = process.argv.slice(2);
if (command === "list") list();
else if (command === "view" && id) console.log(readTicket(id).text);
else if (command === "close" && id) close(id);
else {
  console.error("Usage: node .sandcastle/issues.mjs list | view <id> | close <id>");
  process.exit(1);
}
