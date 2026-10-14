# Security policy

## Supported versions

Battleship is a demo and reference project. Only the latest commit on `main` is supported, and
fixes are not backported.

## Reporting a vulnerability

**Please don't report security problems in public issues, discussions or pull requests.**

Report them privately through GitHub's
[private vulnerability reporting](https://github.com/hexmasternl/battleship/security/advisories/new).
Include:

- what the problem is and where it is (file, endpoint or resource)
- the steps to reproduce it, or a proof of concept
- the impact you expect, for example token theft, reading another player's fleet, or denial of service

You can expect an acknowledgement within a week. We'll keep you updated while we work on a fix,
and credit you in the advisory unless you'd rather stay anonymous.

## Scope

In scope:

- the Users, Games and Events APIs, the gateway route table and the shared ServiceDefaults
- the token flow: access token signing and validation, and refresh token rotation
- the Angular frontend
- leaks of secrets, tokens or personal data in logs, traces or metrics

Out of scope:

- the local development setup itself: default credentials of local containers, the Aspire
  dashboard, or the dev bot, which runs only in local runs
- vulnerabilities in third-party dependencies that have no exploitable path in this project.
  Report those upstream. Dependabot keeps the dependencies up to date.
