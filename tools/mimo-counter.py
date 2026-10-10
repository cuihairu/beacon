#!/usr/bin/env python3
"""MiMo 本机用量计数器（Beacon mimo.usage 连接「用量端点」数据源）。

背景：小米官方无额度接口（2026-10-10 复核：7 端点 404、无限流头），Beacon 按用户令
「模型数不得占额度位」改本机计数。本机经 litellm 免费链的 MiMo 调用落在
/tmp/relay_req.log（每请求一行 `PATH=... MODEL=<name>`）——litellm.log 访问行无模型名
（实测），故以 relay_req.log 为计数源；litellm.log 仅取窗口内 mimo 行数作参考近似。

用法：mimo-counter [--port 18082] [--models mimo-v2.6-flash-free] [--window 60] [--relay-log /tmp/relay_req.log]
挂法：Windows Beacon 设置 → 连接 → 小米 MiMo → 用量端点 = http://192.168.5.188:18082/usage
注意：relay 日志无时间戳，窗口=计数器观测到的近 N 分钟；total=计数器存活期观测到的调用
（日志 rotate/截断不回退 total——历史行是真实发生过的调用，只是源日志不再持有）。
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

_MODEL_RE = re.compile(r"MODEL=([^\s]+)")
_TS_FMTS = ("%Y-%m-%dT%H:%M:%S", "%Y-%m-%d %H:%M:%S", "%Y/%m/%d %H:%M:%S")


def _parse_ts(line: str) -> datetime | None:
    for fmt in _TS_FMTS:
        match = re.search(r"\d{4}[-/]\d{2}[-/]\d{2}[T ]\d{2}:\d{2}:\d{2}", line)
        if match:
            try:
                return datetime.strptime(match.group(0).replace("T", " "), fmt.replace("T", " "))
            except ValueError:
                continue
    return None


class Counter:
    def __init__(self, models: list[str], window_minutes: int, relay_log: Path | None):
        self.models = [m.lower() for m in models]
        self.window = window_minutes * 60
        self.relay_log = relay_log
        self._lock = threading.Lock()
        self._total = 0
        self._recent: list[float] = []  # 观测时间戳（≈请求时间，tail 延迟秒级）
        self._last_seen: str | None = None
        self._started = time.time()

    def _matches(self, model: str) -> bool:
        model = model.lower()
        return any(model == m or model.startswith(m) or m in model for m in self.models)

    def bootstrap(self) -> tuple[int, int] | None:
        """启动全量扫 relay_req.log 得累计；返回 (inode, 偏移) 供 _tail 续读——
        否则 _tail 首扫会重复计同一文件（2026-10-10 实证：456 行计成 912）。"""
        if self.relay_log and self.relay_log.is_file():
            try:
                inode = self.relay_log.stat().st_ino
            except FileNotFoundError:
                return None
            with self.relay_log.open("r", encoding="utf-8", errors="replace") as handle:
                for line in handle:
                    match = _MODEL_RE.search(line)
                    if match and self._matches(match.group(1)):
                        self._total += 1
                return inode, handle.tell()
        return None

    def _observe(self, line: str) -> None:
        match = _MODEL_RE.search(line)
        if not match or not self._matches(match.group(1)):
            return
        now = time.time()
        with self._lock:
            self._total += 1
            self._recent.append(now)
            self._last_seen = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")

    def _tick(self) -> None:
        """清窗口外条目。"""
        while True:
            time.sleep(30)
            cutoff = time.time() - self.window
            with self._lock:
                self._recent = [t for t in self._recent if t >= cutoff]

    def snapshot(self) -> dict:
        cutoff = time.time() - self.window
        with self._lock:
            recent = [t for t in self._recent if t >= cutoff]
            return {
                "total_calls": self._total,
                "window_calls": len(recent),
                "window_minutes": self.window // 60,
                "model": self.models[0] if len(self.models) == 1 else ",".join(self.models),
                "last_seen": self._last_seen,
                "source": "relay_req.log（模型名逐行计数）+ 观测时间戳；窗口=近N分钟观测到的调用",
                "started": datetime.fromtimestamp(self._started, timezone.utc).astimezone().isoformat(timespec="seconds"),
            }


def _tail(path: Path, counter: Counter, resume: tuple[int, int] | None) -> None:
    """新文件/rotate/truncate 均从头全量重读，续读用已跟踪偏移。

    截断检测（2026-10-10 实证 bug）：relay 日志被原 truncate（inode 不变）时，旧实现
    seek(0,2) 停在旧 EOF，偏移永远大于文件大小，新行全部漏计（计数恒 0）。现按
    「inode 变化或 size < 已跟踪偏移」判定重来；截断前已计数的历史行保留在 total
    （它们是真实发生过的调用），不因日志丢失而回退。
    resume=bootstrap 的 (inode, 偏移)：启动全量只计一次，_tail 从该位置续读。
    """
    inode, offset = resume if resume is not None else (None, 0)
    while True:
        try:
            stat = path.stat()
        except FileNotFoundError:
            time.sleep(1)
            continue
        if stat.st_ino != inode or stat.st_size < offset:  # 首次出现/rotate/截断：从头部读全量
            inode = stat.st_ino
            with path.open("r", encoding="utf-8", errors="replace") as handle:
                for line in handle:
                    counter._observe(line)
                offset = handle.tell()
        else:  # 同一文件增量续读
            with path.open("r", encoding="utf-8", errors="replace") as handle:
                handle.seek(offset)
                while True:
                    current = path.stat()
                    if current.st_ino != inode or current.st_size < offset:
                        break  # rotate/截断：回外层从头全量重读
                    line = handle.readline()
                    if not line:
                        time.sleep(0.5)
                        continue
                    offset = handle.tell()
                    counter._observe(line)


def main() -> int:
    parser = argparse.ArgumentParser(description="MiMo 本机用量计数器（Beacon 用量端点数据源）")
    parser.add_argument("--port", type=int, default=18082)
    parser.add_argument("--models", default="mimo-v2.6-flash-free", help="模型名（逗号分隔，子串匹配）")
    parser.add_argument("--window", type=int, default=60, help="窗口分钟数")
    parser.add_argument("--relay-log", default="/tmp/relay_req.log")
    args = parser.parse_args()

    counter = Counter(
        [m.strip() for m in args.models.split(",") if m.strip()],
        args.window,
        Path(args.relay_log),
    )

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self) -> None:  # noqa: N802
            if self.path.rstrip("/") in ("", "/usage"):
                body = json.dumps(counter.snapshot(), ensure_ascii=False, indent=2).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            else:
                self.send_response(404)
                self.end_headers()

        def log_message(self, *_args) -> None:
            pass

    threading.Thread(target=counter._tick, daemon=True).start()
    if counter.relay_log:
        resume = counter.bootstrap()
        threading.Thread(target=_tail, args=(counter.relay_log, counter, resume), daemon=True).start()
        source = counter.relay_log
    else:
        resume = None
        source = None

    print(f"mimo-counter :{args.port} 模型={counter.models} 窗口={args.window}分 源={source} 累计={counter._total}")
    ThreadingHTTPServer(("0.0.0.0", args.port), Handler).serve_forever()
    return 0


if __name__ == "__main__":
    sys.exit(main())
