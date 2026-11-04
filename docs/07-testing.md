# 7. Testing

## Running the tests

```powershell
dotnet test
```

No MySQL server is needed. The tests use an in-memory **SQLite** database, and everything else is the real production code: services, repositories, the audit interceptor, ASP.NET Identity, controllers, views and filters. A full run takes about 15 seconds.

## What is tested (Phase 1)

| File | Checks |
|---|---|
| `OrganizationServiceTests.cs` | seed data (24 departments, 8 roles, Head Office); unique names and codes; audit fields filled in; audit rows written with only the changed fields; soft delete hides the row but keeps it; unit names unique per department; no units in inactive departments; inactive departments left out of drop-downs |
| `UserAdminServiceTests.cs` | new staff get roles and must change their password; password hashes never reach the audit trail; staff can't get the Candidate role; the last HR Administrator can't be deactivated; nobody can deactivate themselves; an assigned HOD keeps the HOD role; only HOD-role users can be a department's HOD |
| `WebTests.cs` | anonymous users go to the login page; security headers present; admin signs in, creates a department, sees it in the audit trail; forms without the anti-forgery token are rejected; 5 wrong passwords lock an account; a temporary password forces a password change; a Recruitment Officer can't open master data; a candidate registers, is blocked until they confirm their email, confirms via the emailed link, and can't open staff pages |

## How the tests are built

* `Infrastructure/TestDatabase.cs` registers the real Application + Infrastructure services, then swaps MySQL for SQLite.
* `Infrastructure/GrsmsWebFactory.cs` starts the **whole website** in memory with the same swap, plus a test administrator, and puts emails, logs and keys in temporary folders.
* `HttpTestExtensions.PostFormAsync` behaves like a browser: it opens the form, reads the anti-forgery token and posts.

## What SQLite can't prove

SQLite is not MySQL. Before each release, also run the app once against a real MySQL server, to check:
* the SQL script applies cleanly (`database/*.sql`);
* duplicate checks ignore upper/lower case. MySQL's collation is case-insensitive, while SQLite's is case-sensitive, so the tests deliberately use exact-case duplicates.

## Adding tests for a new module

1. Service rules: copy the pattern in `OrganizationServiceTests` (`new TestDatabase()`, then one `NewScope()` per simulated request).
2. Screens: add a method to `WebTests` (or a new class with `IClassFixture<GrsmsWebFactory>`), sign in with `client.SignInAsync(...)` and drive forms with `PostFormAsync`.
