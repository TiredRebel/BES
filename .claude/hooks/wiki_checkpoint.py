"""Save a progress checkpoint into .wiki/checkpoints/ so context compaction or an API stop never loses the run's state.

Claude Code runs this from the hooks in .claude/settings.json, one mode per event:

  pre-compact    PreCompact (manual or auto, i.e. the context window is full): write a checkpoint
                 (git state, execution-graph statuses, newest bus messages, last log entry, recent user
                 requests, last assistant message) and ask the compaction to keep its path.
  post-compact   PostCompact: append the compaction summary to that checkpoint.
  stop-failure   StopFailure (rate_limit, max_output_tokens, ...): write a checkpoint. Claude Code ignores
                 this event's output, so only the file matters.
  session-start  SessionStart after compaction: point the fresh context at the newest checkpoint.

The hook reads the event JSON on stdin. Everything is best effort: it always exits 0, so a broken checkpoint
never blocks the session. Standard library only.
"""
import datetime
import json
import os
import pathlib
import re
import subprocess
import sys

MAX_USER_PROMPTS = 5
MAX_PROMPT_CHARS = 600
MAX_ASSISTANT_CHARS = 2000


def run(args, cwd):
    try:
        out = subprocess.run(args, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=20)
        return out.stdout.strip() or out.stderr.strip()
    except Exception as exc:  # noqa: BLE001 - best effort by design
        return f"[unavailable: {exc}]"


def repo_root(event):
    start = event.get("cwd") or os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    top = run(["git", "rev-parse", "--show-toplevel"], start)
    return pathlib.Path(top if top and not top.startswith("[") and os.path.isdir(top) else start)


def graph_statuses(root):
    path = root / ".wiki" / "plan" / "graph.yaml"
    if not path.exists():
        return "graph.yaml not found"
    text = path.read_text(encoding="utf-8")
    pairs = re.findall(r"^  - id: (\S+)\n(?:.*\n)*?    status: (\S+)", text, re.M)
    return ", ".join(f"{node}={status}" for node, status in pairs) or "no node statuses found"


def newest_bus_messages(root, count=5):
    bus = root / ".wiki" / "agents" / "bus"
    files = sorted((p for p in bus.glob("*.md")), key=lambda p: p.stat().st_mtime, reverse=True) if bus.exists() else []
    return [p.name for p in files[:count]]


def last_log_entry(root, max_lines=14):
    path = root / ".wiki" / "log.md"
    if not path.exists():
        return "log.md not found"
    lines = path.read_text(encoding="utf-8").splitlines()
    starts = [i for i, line in enumerate(lines) if line.startswith("## ")]
    return "\n".join(lines[starts[-1]:starts[-1] + max_lines]) if starts else "no entries"


def transcript_tail(path):
    """Return (last user requests, last assistant text) from the session transcript (JSONL)."""
    prompts, last_text = [], ""
    if os.name == "nt" and re.match(r"^/[a-zA-Z]/", path or ""):  # Git Bash style /c/... → C:/...
        path = f"{path[1]}:{path[2:]}"
    try:
        for line in open(path, encoding="utf-8"):
            try:
                entry = json.loads(line)
            except ValueError:
                continue
            if entry.get("isMeta") or entry.get("isSidechain"):
                continue
            content = entry.get("message", {}).get("content")
            if isinstance(content, list):
                content = "\n".join(b.get("text", "") for b in content if isinstance(b, dict) and b.get("type") == "text")
            if not isinstance(content, str) or not content.strip():
                continue
            if entry.get("type") == "user" and not content.lstrip().startswith("<"):
                prompts.append(content.strip())
            elif entry.get("type") == "assistant":
                last_text = content.strip()
    except Exception as exc:  # noqa: BLE001
        return [f"[transcript not readable: {exc}]"], ""
    clip = lambda s, n: s if len(s) <= n else s[:n] + " …[truncated]"
    return [clip(p, MAX_PROMPT_CHARS) for p in prompts[-MAX_USER_PROMPTS:]], clip(last_text, MAX_ASSISTANT_CHARS)


def write_checkpoint(event, label):
    root = repo_root(event)
    folder = root / ".wiki" / "checkpoints"
    folder.mkdir(parents=True, exist_ok=True)
    now = datetime.datetime.now(datetime.timezone.utc)
    path = folder / f"{now:%Y-%m-%dT%H%M%SZ}-{label}.md"
    prompts, last_text = transcript_tail(event.get("transcript_path", ""))
    status = run(["git", "status", "--short"], root).splitlines()
    body = [
        "---",
        f'title: "Checkpoint {now:%Y-%m-%d %H:%M:%S}Z ({label})"',
        "type: checkpoint",
        "status: auto",
        f"updated: {now:%Y-%m-%d}",
        'related: ["[[index]]", "[[log]]"]',
        f"session_id: {event.get('session_id', 'unknown')}",
        f"trigger: {event.get('trigger') or event.get('error') or label}",
        "---",
        "",
        f"# Checkpoint {now:%Y-%m-%d %H:%M:%S}Z ({label})",
        "",
        "Written by `.claude/hooks/wiki_checkpoint.py`. To resume: read [[index]], this page, then the newest [[log]]",
        "entries. The facts below come from git and the wiki files; the request and message excerpts come from the",
        "session transcript.",
        "",
        "## Git",
        "",
        f"- Branch: `{run(['git', 'branch', '--show-current'], root)}`",
        "- Last commits:",
        "",
        "```",
        run(["git", "log", "--oneline", "-5"], root),
        "```",
        "",
        f"- Working tree ({len(status)} changed paths, first 40):",
        "",
        "```",
        "\n".join(status[:40]) or "clean",
        "```",
        "",
        "- Worktrees:",
        "",
        "```",
        run(["git", "worktree", "list"], root),
        "```",
        "",
        "## Execution graph",
        "",
        graph_statuses(root),
        "",
        "## Newest bus messages",
        "",
        "\n".join(f"- `agents/bus/{name}`" for name in newest_bus_messages(root)) or "none",
        "",
        "## Last log entry",
        "",
        "```",
        last_log_entry(root),
        "```",
        "",
        "## Recent user requests (oldest first)",
        "",
        "\n\n".join(f"> {p}".replace("\n", "\n> ") for p in prompts) or "none found",
        "",
        "## Last assistant message",
        "",
        f"> {last_text}".replace("\n", "\n> ") if last_text else "none found",
        "",
    ]
    if event.get("custom_instructions"):
        body += ["## Compaction instructions", "", event["custom_instructions"], ""]
    path.write_text("\n".join(body), encoding="utf-8", newline="\n")
    return root, path


def newest_checkpoint(root, session_id=None):
    folder = root / ".wiki" / "checkpoints"
    files = sorted(folder.glob("*.md"), reverse=True) if folder.exists() else []
    for p in files:
        if session_id is None or f"session_id: {session_id}" in p.read_text(encoding="utf-8"):
            return p
    return None


def emit(event_name, context):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": event_name, "additionalContext": context}}))


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    try:
        event = json.load(sys.stdin)
    except ValueError:
        event = {}
    if mode == "pre-compact":
        root, path = write_checkpoint(event, f"precompact-{event.get('trigger', 'unknown')}")
        emit("PreCompact", f"A progress checkpoint was saved to {path.relative_to(root).as_posix()}. "
                           "Keep that path and the execution-graph node statuses in the summary.")
    elif mode == "stop-failure":
        write_checkpoint(event, f"stopfailure-{event.get('error') or 'unknown'}")
    elif mode == "post-compact":
        root = repo_root(event)
        path = newest_checkpoint(root, event.get("session_id")) or write_checkpoint(event, "postcompact")[1]
        with open(path, "a", encoding="utf-8", newline="\n") as f:
            f.write("\n## Compaction summary\n\n" + (event.get("compact_summary") or "(none provided)") + "\n")
    elif mode == "session-start":
        root = repo_root(event)
        path = newest_checkpoint(root)
        if path:
            emit("SessionStart", f"Context was compacted. Before continuing, read .wiki/index.md, the checkpoint "
                                 f"{path.relative_to(root).as_posix()} and the newest entries of .wiki/log.md. "
                                 f"Execution graph: {graph_statuses(root)}.")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:  # noqa: BLE001 - never block the session
        print(f"wiki_checkpoint: {exc}", file=sys.stderr)
    sys.exit(0)
