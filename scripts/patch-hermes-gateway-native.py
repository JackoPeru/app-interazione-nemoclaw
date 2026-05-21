#!/usr/bin/env python3
"""Patch Hermes Gateway API server for Hermes Hub native mode.

This is intentionally idempotent. It is used by the Ubuntu/headless launcher
until the same contract lands upstream in Hermes.
"""

from __future__ import annotations

import argparse
import importlib
import inspect
import os
import py_compile
import shutil
import sys
import time
from pathlib import Path


def _candidate_paths() -> list[Path]:
    candidates: list[Path] = []

    explicit = os.environ.get("HERMES_GATEWAY_API_SERVER_PATH")
    if explicit:
        candidates.append(Path(explicit).expanduser())

    try:
        module = importlib.import_module("gateway.platforms.api_server")
        candidates.append(Path(inspect.getfile(module)))
    except Exception:
        pass

    home = Path.home()
    hermes_home = Path(os.environ.get("HERMES_HOME", home / ".hermes")).expanduser()
    roots = [
        hermes_home,
        home / ".local",
        home / ".cache",
        Path.cwd(),
    ]
    rels = [
        Path("hermes-agent/gateway/platforms/api_server.py"),
        Path("gateway/platforms/api_server.py"),
        Path("src/hermes_agent/gateway/platforms/api_server.py"),
    ]
    for root in roots:
        for rel in rels:
            candidates.append(root / rel)

    # Small bounded fallback for common local source installs.
    for root in (hermes_home, Path.cwd()):
        if root.exists():
            try:
                candidates.extend(root.glob("**/gateway/platforms/api_server.py"))
            except Exception:
                pass

    unique: list[Path] = []
    seen: set[str] = set()
    for path in candidates:
        resolved = str(path.expanduser())
        if resolved not in seen:
            unique.append(path.expanduser())
            seen.add(resolved)
    return unique


def _find_target(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser()
        if path.is_file():
            return path
        raise FileNotFoundError(f"Gateway api_server.py not found: {path}")

    for path in _candidate_paths():
        if path.is_file():
            return path

    raise FileNotFoundError(
        "Gateway api_server.py not found. Set HERMES_GATEWAY_API_SERVER_PATH "
        "to the full path of gateway/platforms/api_server.py."
    )


def _replace_once(text: str, old: str, new: str, label: str) -> tuple[str, bool]:
    if old not in text:
        raise RuntimeError(f"Patch anchor not found: {label}")
    return text.replace(old, new, 1), True


def _patch_text(text: str) -> tuple[str, list[str]]:
    changes: list[str] = []

    if '"native_protocol": {' not in text:
        text, _ = _replace_once(
            text,
            '            "platform": "hermes-agent",\n'
            '            "gateway_state": runtime.get("gateway_state"),',
            '            "platform": "hermes-agent",\n'
            '            "native_protocol": {\n'
            '                "name": "hermes-native",\n'
            '                "transport": "responses",\n'
            '                "endpoint": "/v1/responses",\n'
            '                "alias": "/v1/hermes/native",\n'
            '                "context_owner": "hermes-agent",\n'
            '                "raw_event_passthrough": True,\n'
            '            },\n'
            '            "gateway_state": runtime.get("gateway_state"),',
            "health_detailed native_protocol",
        )
        changes.append("health_detailed native_protocol")

    if '"hermes_native": True,' not in text:
        text, _ = _replace_once(
            text,
            '            "features": {\n'
            '                "chat_completions": True,',
            '            "features": {\n'
            '                "hermes_native": True,\n'
            '                "native_responses": True,\n'
            '                "native_endpoint": "/v1/hermes/native",\n'
            '                "native_event_passthrough": True,\n'
            '                "raw_hermes_events": True,\n'
            '                "strict_native_compatible": True,\n'
            '                "context_owner": "hermes-agent",\n'
            '                "planner_events": True,\n'
            '                "memory_events": True,\n'
            '                "retrieval_events": True,\n'
            '                "artifact_events": True,\n'
            '                "chat_completions": True,',
            "capabilities hermes_native",
        )
        changes.append("capabilities hermes_native")

    if '"hermes_native": {"method": "POST", "path": "/v1/hermes/native"}' not in text:
        text, _ = _replace_once(
            text,
            '                "responses": {"method": "POST", "path": "/v1/responses"},\n'
            '                "runs": {"method": "POST", "path": "/v1/runs"},',
            '                "responses": {"method": "POST", "path": "/v1/responses"},\n'
            '                "hermes_native": {"method": "POST", "path": "/v1/hermes/native"},\n'
            '                "runs": {"method": "POST", "path": "/v1/runs"},',
            "capabilities hermes_native endpoint",
        )
        changes.append("capabilities hermes_native endpoint")

    if 'await _write_event("hermes.native.protocol"' not in text:
        text, _ = _replace_once(
            text,
            '            await _write_event("response.created", {\n'
            '                "type": "response.created",\n'
            '                "response": created_env,\n'
            '            })\n'
            '            _persist_response_snapshot(created_env)',
            '            await _write_event("response.created", {\n'
            '                "type": "response.created",\n'
            '                "response": created_env,\n'
            '            })\n'
            '            await _write_event("hermes.native.protocol", {\n'
            '                "type": "hermes.native.protocol",\n'
            '                "protocol": "hermes-native",\n'
            '                "transport": "responses",\n'
            '                "endpoint": "/v1/responses",\n'
            '                "native_endpoint": "/v1/hermes/native",\n'
            '                "context_owner": "hermes-agent",\n'
            '                "raw_event_passthrough": True,\n'
            '                "strict_native_compatible": True,\n'
            '                "session_id": session_id,\n'
            '            })\n'
            '            _persist_response_snapshot(created_env)',
            "responses stream hermes.native.protocol",
        )
        changes.append("responses stream hermes.native.protocol")

    if "def _emit_raw_hermes_event" not in text:
        text, _ = _replace_once(
            text,
            "            async def _dispatch(it) -> None:",
            '            async def _emit_raw_hermes_event(payload: Dict[str, Any]) -> None:\n'
            '                """Forward Hermes-native metadata without forcing OpenAI shape."""\n'
            '                event_type = str(payload.get("type") or payload.get("event") or "hermes.event")\n'
            '                if not event_type.startswith("hermes."):\n'
            '                    event_type = "hermes." + event_type\n'
            '                payload["type"] = event_type\n'
            '                payload.setdefault("session_id", session_id)\n'
            '                await _write_event(event_type, payload)\n'
            "\n"
            "            async def _dispatch(it) -> None:",
            "responses stream raw hermes emitter",
        )
        changes.append("responses stream raw hermes emitter")

    if 'tag == "__hermes_raw_event__"' not in text:
        text, _ = _replace_once(
            text,
            '                    elif tag == "__tool_completed__":\n'
            '                        await _emit_tool_completed(payload)',
            '                    elif tag == "__tool_completed__":\n'
            '                        await _emit_tool_completed(payload)\n'
            '                    elif tag == "__hermes_raw_event__":\n'
            '                        await _emit_raw_hermes_event(payload)',
            "responses stream raw hermes dispatch",
        )
        changes.append("responses stream raw hermes dispatch")

    old_progress = (
        '            def _on_tool_progress(event_type, name, preview, args, **kwargs):\n'
        '                """Queue non-start tool progress events if needed in future.\n'
        "\n"
        '                The structured Responses stream uses ``tool_start_callback``\n'
        '                and ``tool_complete_callback`` for exact call-id correlation,\n'
        '                so progress events are currently ignored here.\n'
        '                """\n'
        "                return"
    )
    if old_progress in text:
        text = text.replace(
            old_progress,
            '            def _on_tool_progress(event_type, name, preview, args, **kwargs):\n'
            '                """Pass through Hermes-native tool/progress metadata."""\n'
            '                if str(name).startswith("_"):\n'
            "                    return\n"
            "                payload = {\n"
            '                    "type": str(event_type or "hermes.tool.progress"),\n'
            '                    "event": str(event_type or "hermes.tool.progress"),\n'
            '                    "tool": name,\n'
            '                    "label": preview,\n'
            '                    "arguments": args or {},\n'
            "                }\n"
            "                payload.update(kwargs or {})\n"
            '                _stream_q.put(("__hermes_raw_event__", payload))',
            1,
        )
        changes.append("responses tool_progress raw passthrough")
    elif '"""Pass through Hermes-native tool/progress metadata."""' not in text:
        raise RuntimeError("Patch anchor not found: responses tool_progress callback")

    if '"event": "tool.started",' not in text:
        text, _ = _replace_once(
            text,
            '            def _on_tool_start(tool_call_id, function_name, function_args):\n'
            '                """Queue a started tool for live function_call streaming."""\n'
            '                _stream_q.put(("__tool_started__", {',
            '            def _on_tool_start(tool_call_id, function_name, function_args):\n'
            '                """Queue a started tool for live function_call streaming."""\n'
            '                _stream_q.put(("__hermes_raw_event__", {\n'
            '                    "type": "hermes.tool.progress",\n'
            '                    "event": "tool.started",\n'
            '                    "tool": function_name,\n'
            '                    "toolCallId": tool_call_id,\n'
            '                    "status": "running",\n'
            '                    "arguments": function_args or {},\n'
            "                }))\n"
            '                _stream_q.put(("__tool_started__", {',
            "responses tool_start raw passthrough",
        )
        changes.append("responses tool_start raw passthrough")

    if '"event": "tool.completed",' not in text:
        text, _ = _replace_once(
            text,
            '            def _on_tool_complete(tool_call_id, function_name, function_args, function_result):\n'
            '                """Queue a completed tool result for live function_call_output streaming."""\n'
            '                _stream_q.put(("__tool_completed__", {',
            '            def _on_tool_complete(tool_call_id, function_name, function_args, function_result):\n'
            '                """Queue a completed tool result for live function_call_output streaming."""\n'
            '                _stream_q.put(("__hermes_raw_event__", {\n'
            '                    "type": "hermes.tool.progress",\n'
            '                    "event": "tool.completed",\n'
            '                    "tool": function_name,\n'
            '                    "toolCallId": tool_call_id,\n'
            '                    "status": "completed",\n'
            '                    "arguments": function_args or {},\n'
            '                    "result": function_result,\n'
            "                }))\n"
            '                _stream_q.put(("__tool_completed__", {',
            "responses tool_complete raw passthrough",
        )
        changes.append("responses tool_complete raw passthrough")

    if 'add_post("/v1/hermes/native", self._handle_responses)' not in text:
        text, _ = _replace_once(
            text,
            '            self._app.router.add_post("/v1/responses", self._handle_responses)\n'
            '            self._app.router.add_get("/v1/responses/{response_id}", self._handle_get_response)',
            '            self._app.router.add_post("/v1/responses", self._handle_responses)\n'
            '            self._app.router.add_post("/v1/hermes/native", self._handle_responses)\n'
            '            self._app.router.add_get("/v1/responses/{response_id}", self._handle_get_response)',
            "router hermes_native alias",
        )
        changes.append("router hermes_native alias")

    return text, changes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", help="Path to gateway/platforms/api_server.py")
    parser.add_argument("--check", action="store_true", help="Validate patchability without writing")
    args = parser.parse_args()

    target = _find_target(args.target)
    original = target.read_text(encoding="utf-8")
    patched, changes = _patch_text(original)

    if args.check:
        state = "already patched" if not changes else "patchable"
        print(f"Hermes native gateway patch {state}: {target}")
        if changes:
            for change in changes:
                print(f"- {change}")
        return 0

    if not changes:
        print(f"Hermes native gateway already patched: {target}")
        return 0

    backup = target.with_suffix(target.suffix + f".bak-hermes-native-{int(time.time())}")
    shutil.copy2(target, backup)
    target.write_text(patched, encoding="utf-8", newline="")
    py_compile.compile(str(target), doraise=True)
    print(f"Hermes native gateway patched: {target}")
    print(f"Backup: {backup}")
    for change in changes:
        print(f"- {change}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
