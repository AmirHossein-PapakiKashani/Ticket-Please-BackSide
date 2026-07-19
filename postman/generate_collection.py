import json
from pathlib import Path


def req(name, method, path, body=None, auth=False, tests="", formdata=None):
    r = {
        "name": name,
        "request": {
            "method": method,
            "header": [],
            "url": "{{baseUrl}}" + path,
        },
        "event": [
            {
                "listen": "test",
                "script": {
                    "type": "text/javascript",
                    "exec": tests.splitlines() if tests else [],
                },
            }
        ],
    }
    if auth:
        r["request"]["auth"] = {
            "type": "bearer",
            "bearer": [{"key": "token", "value": "{{accessToken}}", "type": "string"}],
        }
    if body is not None:
        r["request"]["header"].append({"key": "Content-Type", "value": "application/json"})
        r["request"]["body"] = {"mode": "raw", "raw": body}
    if formdata is not None:
        r["request"]["body"] = {"mode": "formdata", "formdata": formdata}
    return r


def folder(name, items):
    return {"name": name, "item": items}


phase0 = folder(
    "Phase-0 Auth",
    [
        req(
            "Login validation 400",
            "POST",
            "/auth/login",
            body='{"username":"","password":""}',
            tests="""
pm.test('status 400', () => pm.response.to.have.status(400));
pm.test('errors map', () => { const j=pm.response.json(); pm.expect(j.errors).to.be.an('object'); });
""".strip(),
        ),
        req(
            "Login bad credentials 401",
            "POST",
            "/auth/login",
            body='{"username":"nouserxyz","password":"wrongpassword"}',
            tests="pm.test('status 401', () => pm.response.to.have.status(401));",
        ),
        req(
            "Profile without bearer 401",
            "GET",
            "/profile",
            tests="pm.test('status 401', () => pm.response.to.have.status(401));",
        ),
        req(
            "Login superadmin 200",
            "POST",
            "/auth/login",
            body='{"username":"{{superAdminUser}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.test('tokens present', () => { pm.expect(j.accessToken).to.be.a('string'); pm.expect(j.refreshToken).to.be.a('string'); });
pm.environment.set('accessToken', j.accessToken);
pm.environment.set('refreshToken', j.refreshToken);
""".strip(),
        ),
        req(
            "Refresh token 200",
            "POST",
            "/auth/refresh-token",
            body='{"refreshToken":"{{refreshToken}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.test('rotated tokens', () => { pm.expect(j.accessToken).to.be.a('string'); pm.expect(j.refreshToken).to.be.a('string'); });
pm.environment.set('accessToken', j.accessToken);
pm.environment.set('refreshToken', j.refreshToken);
""".strip(),
        ),
        req(
            "Get profile 200",
            "GET",
            "/profile",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
    ],
)

phase1 = folder(
    "Phase-1 Admin",
    [
        req(
            "Login PM",
            "POST",
            "/auth/login",
            body='{"username":"{{pmUser}}","password":"{{password}}"}',
            tests="""
pm.test('pm login 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "PM cannot access admin 403",
            "GET",
            "/admin/dashboard/stats",
            auth=True,
            tests="pm.test('status 403', () => pm.response.to.have.status(403));",
        ),
        req(
            "Login superadmin for admin flow",
            "POST",
            "/auth/login",
            body='{"username":"{{superAdminUser}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.environment.set('accessToken', j.accessToken);
pm.environment.set('refreshToken', j.refreshToken);
""".strip(),
        ),
        req(
            "Create plan 201",
            "POST",
            "/admin/plans",
            auth=True,
            body='{"name":"Postman Plan","durationDays":30,"price":100,"maxClientCount":5,"maxAgentCount":5,"modulesJson":"[]"}',
            tests="""
pm.test('status 201', () => pm.response.to.have.status(201));
pm.environment.set('planId', String(pm.response.json().planId));
""".strip(),
        ),
        req(
            "Create provider 201",
            "POST",
            "/admin/providers",
            auth=True,
            body='{"name":"Postman Provider","email":"postman-provider@example.com","phoneNumber":"02122222222","managerUsername":"postman.pm","managerFullName":"Postman PM","managerPassword":"ChangeMe123!"}',
            tests="""
pm.test('status 201', () => pm.response.to.have.status(201));
pm.environment.set('providerId', String(pm.response.json().providerId));
""".strip(),
        ),
        req(
            "Create subscription 201",
            "POST",
            "/admin/providers/{{providerId}}/subscriptions",
            auth=True,
            body='{"planId":{{planId}},"moneyPaid":100}',
            tests="""
pm.test('status 201', () => pm.response.to.have.status(201));
const j=pm.response.json();
if (j.subscriptionId) pm.environment.set('subscriptionId', String(j.subscriptionId));
""".strip(),
        ),
        req(
            "New PM login 200",
            "POST",
            "/auth/login",
            body='{"username":"postman.pm","password":"ChangeMe123!"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "PM profile subscription 200",
            "GET",
            "/profile/subscription",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
    ],
)

phase2 = folder(
    "Phase-2 PM",
    [
        req(
            "Login demo PM",
            "POST",
            "/auth/login",
            body='{"username":"{{pmUser}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Dashboard stats 200",
            "GET",
            "/pm/dashboard/stats",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.test('has unassignedTicketsCount', () => pm.expect(pm.response.json()).to.have.property('unassignedTicketsCount'));
""".strip(),
        ),
        req(
            "List agents 200",
            "GET",
            "/pm/agents?pageNumber=1&pageSize=20",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.test('paged items', () => pm.expect(j.items).to.be.an('array'));
if (j.items.length) pm.environment.set('agentId', String(j.items[0].userId || j.items[0].id));
""".strip(),
        ),
        req(
            "List clients 200",
            "GET",
            "/pm/clients?pageNumber=1&pageSize=20",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.test('paged items', () => pm.expect(j.items).to.be.an('array'));
if (j.items.length) pm.environment.set('clientId', String(j.items[0].clientId || j.items[0].id));
""".strip(),
        ),
    ],
)

phase3 = folder(
    "Phase-3 Requester",
    [
        req(
            "Login requester1",
            "POST",
            "/auth/login",
            body='{"username":"{{req1User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Create ticket 201",
            "POST",
            "/requester/tickets",
            auth=True,
            formdata=[
                {"key": "topic", "value": "Postman ticket", "type": "text"},
                {"key": "text", "value": "Hello from postman integration", "type": "text"},
                {"key": "priority", "value": "Medium", "type": "text"},
            ],
            tests="""
pm.test('status 201', () => pm.response.to.have.status(201));
const j=pm.response.json();
pm.test('ticketId', () => pm.expect(j.ticketId).to.be.a('number'));
pm.environment.set('ticketId', String(j.ticketId));
""".strip(),
        ),
        req(
            "Login requester2 colleague",
            "POST",
            "/auth/login",
            body='{"username":"{{req2User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Colleague GET ticket 200",
            "GET",
            "/requester/tickets/{{ticketId}}",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "Colleague POST message 403",
            "POST",
            "/requester/tickets/{{ticketId}}/messages",
            auth=True,
            formdata=[{"key": "text", "value": "should fail", "type": "text"}],
            tests="pm.test('status 403', () => pm.response.to.have.status(403));",
        ),
    ],
)

phase4 = folder(
    "Phase-4 Agent",
    [
        req(
            "Login agent1",
            "POST",
            "/auth/login",
            body='{"username":"{{agent1User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "List tickets assignedToMe",
            "GET",
            "/agent/tickets?assignedToMe=true&pageNumber=1&pageSize=20",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
pm.test('items array', () => pm.expect(j.items).to.be.an('array'));
if (j.items.length) pm.environment.set('ticketId', String(j.items[0].ticketId || j.items[0].id));
""".strip(),
        ),
        req(
            "Login agent2",
            "POST",
            "/auth/login",
            body='{"username":"{{agent2User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Get active agents",
            "GET",
            "/agent/active-agents",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const arr=pm.response.json();
pm.test('array', () => pm.expect(arr).to.be.an('array'));
if (arr.length >= 2) {
  const target = arr.find(a => (a.username || '') === 'demo.agent2') || arr[1];
  pm.environment.set('agent2Id', String(target.userId || target.id));
}
""".strip(),
        ),
        req(
            "Login agent1 again",
            "POST",
            "/auth/login",
            body='{"username":"{{agent1User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Add note 201",
            "POST",
            "/agent/tickets/{{ticketId}}/notes",
            auth=True,
            body='{"text":"internal note postman"}',
            tests="pm.test('status 201', () => pm.response.to.have.status(201));",
        ),
        req(
            "Reassign to agent2",
            "PATCH",
            "/agent/tickets/{{ticketId}}/reassign",
            auth=True,
            body='{"newAgentId":{{agent2Id}}}',
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "Old agent cannot message 403",
            "POST",
            "/agent/tickets/{{ticketId}}/messages",
            auth=True,
            formdata=[{"key": "text", "value": "should fail after reassign", "type": "text"}],
            tests="pm.test('status 403', () => pm.response.to.have.status(403));",
        ),
        req(
            "Login agent2 resolve",
            "POST",
            "/auth/login",
            body='{"username":"{{agent2User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Resolve ticket",
            "PATCH",
            "/agent/tickets/{{ticketId}}/status",
            auth=True,
            body='{"newStatus":"Resolved"}',
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "Login req1 check notes absent",
            "POST",
            "/auth/login",
            body='{"username":"{{req1User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Requester messages no notes",
            "GET",
            "/requester/tickets/{{ticketId}}/messages?pageNumber=1&pageSize=50",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
const texts=(j.items||[]).map(i => (i.text||'').toLowerCase());
pm.test('note text absent', () => pm.expect(texts.join(' ')).to.not.include('internal note postman'));
""".strip(),
        ),
    ],
)

phase5 = folder(
    "Phase-5 CM",
    [
        req(
            "Login CM",
            "POST",
            "/auth/login",
            body='{"username":"{{cmUser}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "CM dashboard 200",
            "GET",
            "/cm/dashboard/stats",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "List requesters 200",
            "GET",
            "/cm/requesters?pageNumber=1&pageSize=20",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "List tickets metadata 200",
            "GET",
            "/cm/tickets?pageNumber=1&pageSize=20",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const j=pm.response.json();
(j.items||[]).forEach(it => pm.expect(it).to.not.have.property('messages'));
""".strip(),
        ),
        req(
            "CM cannot agent chat 403",
            "GET",
            "/agent/tickets",
            auth=True,
            tests="pm.test('status 403', () => pm.response.to.have.status(403));",
        ),
    ],
)

phase6 = folder(
    "Phase-6 Full cycle",
    [
        req(
            "Login superadmin smoke",
            "POST",
            "/auth/login",
            body='{"username":"{{superAdminUser}}","password":"{{password}}"}',
            tests="pm.test('superadmin 200', () => pm.response.to.have.status(200));",
        ),
        req(
            "Login req1 for reopen",
            "POST",
            "/auth/login",
            body='{"username":"{{req1User}}","password":"{{password}}"}',
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
pm.environment.set('accessToken', pm.response.json().accessToken);
""".strip(),
        ),
        req(
            "Resolve ticketId from my tickets",
            "GET",
            "/requester/tickets?createdByMe=true&pageNumber=1&pageSize=20",
            auth=True,
            tests="""
pm.test('status 200', () => pm.response.to.have.status(200));
const items=pm.response.json().items || [];
pm.test('has tickets', () => pm.expect(items.length).to.be.above(0));
const resolved = items.find(t => (t.status || '') === 'Resolved' || (t.status || '') === 'Closed') || items[0];
pm.environment.set('ticketId', String(resolved.ticketId || resolved.id));
""".strip(),
        ),
        req(
            "Reopen ticket",
            "PATCH",
            "/requester/tickets/{{ticketId}}/reopen",
            auth=True,
            tests="pm.test('status 200', () => pm.response.to.have.status(200));",
        ),
    ],
)

collection = {
    "info": {
        "name": "Ticket Phase Integration",
        "description": "Real-API phase DoD scenarios for Ticketing System. Not mocks.",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": [phase0, phase1, phase2, phase3, phase4, phase5, phase6],
}

out = Path(r"d:\Ticket\postman\Ticket-Phase-Integration.postman_collection.json")
out.write_text(json.dumps(collection, indent=2), encoding="utf-8")
print("wrote", out)
print("folders", len(collection["item"]))
print("requests", sum(len(f["item"]) for f in collection["item"]))
