# Contributing

Issues and pull requests are welcome. For anything bigger than a small fix, open an issue first so we can agree the approach.

## Branches

- `main` carries both lines: Umbraco 18 (package 18.x) and Umbraco 17 (package 17.x). `UmbracoMajor` (`18` by default, or `17`) picks the line. `v17` is frozen history.
- Open pull requests against `main`. A change applies to both lines unless it sits behind `#if UMBRACO_17` or in a per-line file.
- `bash build/test.sh` runs the unit and integration tests once per line.

## Build and test

You need the .NET 10 SDK (17 and 18) or the .NET 11 SDK (19) and Node (the version in `src/Umbraco.Community.ArchivedUsers/Client/.nvmrc`).

```bash
dotnet restore src/Umbraco.Community.ArchivedUsers.slnx --locked-mode
dotnet build src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-restore
dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-build
# the default line is 18; add -p:UmbracoMajor=17 to each command for 17, or run: bash build/test.sh

cd src/Umbraco.Community.ArchivedUsers/Client
npm ci --ignore-scripts && npm run build && npm test
```

The built client in `wwwroot/` is committed. Rebuild it when you change the client, and CI fails if it drifts.

## Try it on the demo site

`demo/` is an Umbraco site with seeded demo users. Set two user secrets, then run it:

```bash
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet user-secrets set "ArchivedUsersDemo:UserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite
```

The first run installs Umbraco; run it again to seed the demo users.

## Try it in your own site

Pack with a unique version each time, because NuGet's global cache keeps serving the first build of any version number:

```bash
dotnet pack src/Umbraco.Community.ArchivedUsers -c Release -o ~/local-nuget -p:Version=18.0.0-local.$(date +%s)   # use 17.0.0-local... with -p:UmbracoMajor=17 for the 17 line
dotnet nuget add source ~/local-nuget --name local   # once
dotnet add package Umbraco.Community.ArchivedUsers --version 18.0.0-local.<timestamp>
```

## Before you open a pull request

- Add or update tests for what you changed, and run them.
- Keep user data out of logs: user keys only, never names, emails or reasons.
- Security issues go through private reporting (the Security tab), never a public issue or PR.
