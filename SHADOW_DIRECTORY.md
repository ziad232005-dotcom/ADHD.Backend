# Shadow Teacher directory

Shadow Teachers apply from the app (no account). The admin reviews on the
web page, and approved teachers are shown to parents.

## Set the admin password (once, on the computer running the server)

```
dotnet user-secrets set "Admin:Password" "choose-a-long-password"
```

On a real server set the environment variable `Admin__Password` instead.
Without it the admin endpoints answer 503, so nobody can review by accident.

## Review applications

Open `http://localhost:5187/admin`, type the password, then approve or
reject. The certificate photo is shown only here.

## Where the data lives

`App_Data/shadows.json` and `App_Data/certificates/`. They hold personal
data, are listed in `.gitignore`, and must be backed up with the server.

## Endpoints

| Method | Path | Who |
|---|---|---|
| POST | `/api/shadows` | the Shadow Teacher's form |
| GET | `/api/shadows` | parents: approved teachers only |
| GET | `/api/admin/shadows?status=pending` | admin (header `X-Admin-Password`) |
| GET | `/api/admin/shadows/{id}/documents/{n}` | admin: one uploaded document |
| GET | `/api/admin/shadows/{id}/photo` | admin: profile photo |
| POST | `/api/admin/shadows/{id}/approve` | admin |
| POST | `/api/admin/shadows/{id}/reject` | admin |

## Test results sync

The child's phone sends each finished test (TOVA or CARS) to the server; the
parent's phone reads it and makes the PDF. No account is needed: the parent's
phone makes a secret key, puts it in the child's QR code, and both phones use
it. Only a hash of the key is stored (`App_Data/sync.json`).

- `POST /api/sync/push` with `{ key, kind: "tova"|"cars", id, data }`
- `POST /api/sync/pull` with `{ key }` returns `{ results: [...] }`

Limits: 500 results per key, 3000 keys, 4 KB per result, 120 requests a minute
per IP.

### Tasks and rewards (parent to child)

The parent's phone keeps one copy of the child's tasks and rewards on the
server (`App_Data/sync_tasks.json`); the child's phone reads it when it opens.

- `POST /api/sync/tasks/put` with `{ key, data }` (an object, up to 64 KB)
- `POST /api/sync/tasks/get` with `{ key }` returns `{ data, updatedAt }`
  (`data` is null when nothing was sent yet)
