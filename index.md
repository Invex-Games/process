# Invex.Process

A small, focused .NET library for running external processes with first-class logging, real-time output streaming,
full output capture, and fail-fast error handling. It wraps `System.Diagnostics.Process` behind a clean, injectable
`IProcessRunner` abstraction so your build targets, tools, and services can shell out to executables (`dotnet`, `git`,
`npm`, …) without re-implementing redirection, buffering, and exit-code handling every time.

## Start here

- [Introduction](docs/introduction.md): the problem it solves and when to use it.
- [Getting Started](docs/getting-started.md): install the package, register it and run your first process.
- [Running Processes](docs/running-processes.md): arguments, working directories and environment variables.
- [Handling Failures](docs/handling-failures.md): fail-fast exceptions and `AllowFailedResult`.
- [API Reference](api/index.md): every public type.
