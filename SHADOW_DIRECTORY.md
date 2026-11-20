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
| GET | `/api/admin/shadows/{id}/certificate` | admin |
| POST | `/api/admin/shadows/{id}/approve` | admin |
| POST | `/api/admin/shadows/{id}/reject` | admin |
