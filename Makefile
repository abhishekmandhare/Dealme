TRUENAS  := truenas_admin@192.168.1.179
GH_REPO  := abhishekmandhare/Dealme
API_URL  := http://localhost:5001
AUTO_URL := http://localhost:3100
DB_TMP   := /tmp/d.db

.PHONY: help \
        logs logs-api \
        runs db test \
        rebuild-automation rebuild-api rebuild-web \
        test-search test-mobile test-activities login \
        ssh truenas-logs truenas-status truenas-runs \
        ci

# ── Help ───────────────────────────────────────────────────────────────────

help:
	@echo "Local"
	@echo "  logs                 tail automation container logs (-f)"
	@echo "  logs-api             tail API container logs (-f)"
	@echo "  runs [N=15]          show last N AutomationRuns from DB"
	@echo "  db                   copy DB from container + show counts"
	@echo "  test                 run .NET test suite"
	@echo "  rebuild-automation   rebuild + restart automation"
	@echo "  rebuild-api          rebuild + restart API"
	@echo "  rebuild-web          rebuild + restart web"
	@echo "  test-search          trigger 2 desktop searches (direct)"
	@echo "  test-mobile          trigger 2 mobile searches (direct)"
	@echo "  test-activities      trigger daily activities (direct)"
	@echo "  login                open login browser — VNC to port 5900"
	@echo ""
	@echo "TrueNAS"
	@echo "  ssh                  SSH into TrueNAS"
	@echo "  truenas-logs         tail automation logs on TrueNAS"
	@echo "  truenas-status       show dealme container statuses on TrueNAS"
	@echo "  truenas-runs         show last 10 AutomationRuns on TrueNAS"
	@echo ""
	@echo "CI/CD"
	@echo "  ci                   show recent GitHub Actions runs"

# ── Local ──────────────────────────────────────────────────────────────────

logs:
	docker logs dealme-automation --tail 80 -f

logs-api:
	docker logs dealme-api --tail 80 -f

N ?= 15
runs:
	docker cp dealme-api:/data/dealme.db $(DB_TMP)
	python3 scripts/runs.py $(N)

db:
	docker cp dealme-api:/data/dealme.db $(DB_TMP)
	@python3 -c "\
import sqlite3; c = sqlite3.connect('$(DB_TMP)'); \
print('AutomationRuns:', c.execute('SELECT COUNT(*) FROM AutomationRuns').fetchone()[0]); \
print('Opportunities: ', c.execute('SELECT COUNT(*) FROM Opportunities').fetchone()[0]); \
"

test:
	dotnet test tests/DealMe.Tests/DealMe.Tests.csproj --nologo

rebuild-automation:
	cd deploy && docker compose build automation && docker compose up -d automation

rebuild-api:
	cd deploy && docker compose build dealme-api && docker compose up -d dealme-api

rebuild-web:
	cd deploy && docker compose build dealme-web && docker compose up -d dealme-web

test-search:
	curl -s -X POST $(AUTO_URL)/run/bing-searches \
	  -H "Content-Type: application/json" -d '{"searchCount":2}' | python3 -m json.tool

test-mobile:
	curl -s -X POST $(AUTO_URL)/run/bing-mobile-searches \
	  -H "Content-Type: application/json" -d '{"searchCount":2}' | python3 -m json.tool

test-activities:
	curl -s -X POST $(AUTO_URL)/run/daily-activities | python3 -m json.tool

login:
	@echo "Opening login browser. Connect via VNC on port 5900."
	curl -s -X POST $(AUTO_URL)/run/login-browser | python3 -m json.tool

# ── TrueNAS ────────────────────────────────────────────────────────────────

ssh:
	ssh $(TRUENAS)

truenas-logs:
	ssh $(TRUENAS) "sudo docker logs ix-dealme-dealme-automation-1 --tail 80 -f"

truenas-status:
	ssh $(TRUENAS) "sudo docker ps --filter 'name=ix-dealme' \
	  --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'"

truenas-runs:
	ssh $(TRUENAS) "sudo docker cp ix-dealme-dealme-api-1:/data/dealme.db /tmp/tn.db 2>/dev/null; \
	  python3 -c \"\
import sqlite3; c = sqlite3.connect('/tmp/tn.db'); \
rows = c.execute('SELECT Task,Success,PointsBefore,PointsAfter,StartedAt FROM AutomationRuns ORDER BY StartedAt DESC LIMIT 10').fetchall(); \
[print(f\\\"{r[4][:19]}  {r[0]:<30}  {'ok' if r[1] else 'FAIL'}  pts {r[2]}->{r[3]}\\\") for r in rows]\""

# ── CI/CD ──────────────────────────────────────────────────────────────────

ci:
	gh run list --limit 8 --repo $(GH_REPO)
