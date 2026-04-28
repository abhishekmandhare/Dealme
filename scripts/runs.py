#!/usr/bin/env python3
"""Print recent AutomationRuns from the local dealme-api SQLite DB.

Usage:
    docker cp dealme-api:/data/dealme.db /tmp/d.db && python3 scripts/runs.py [N]

N defaults to 15.
"""
import sqlite3, sys, os

db = '/tmp/d.db'
limit = int(sys.argv[1]) if len(sys.argv) > 1 else 15

if not os.path.exists(db):
    print(f"DB not found at {db}. Run: docker cp dealme-api:/data/dealme.db {db}")
    sys.exit(1)

c = sqlite3.connect(db)
rows = c.execute('''
    SELECT Task, Success, PointsBefore, PointsAfter, Error, LogOutput, StartedAt
    FROM AutomationRuns
    ORDER BY StartedAt DESC
    LIMIT ?
''', (limit,)).fetchall()

for task, ok, pts_before, pts_after, error, log_output, started_at in rows:
    pts = f"pts {pts_before}->{pts_after}" if pts_before is not None else "pts n/a"
    status = "ok" if ok else "FAIL"
    print(f"{started_at[:19]}  {task:<30}  {status}  {pts}")
    if error:
        print(f"   ERROR: {error}")
    if log_output:
        try:
            lines = [l for l in __import__('json').loads(log_output) if l.strip()]
        except Exception:
            lines = [l for l in log_output.strip().split('\n') if l.strip()]
        # Show last meaningful line (skip the "Starting ..." header)
        tail = lines[-1] if lines else ''
        if tail:
            print(f"   {tail}")
    print()
