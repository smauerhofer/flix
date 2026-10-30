# Single type per C# file

Each C# class, record, enum, and interface has its own `.cs` file. The eight strategies are in separate files in `src/Flix.Core`. `FilterLease` and `FilterEntry` were extracted from `FilterManager` without changing the reader/manager cleanup ownership.

This is a source-layout refactor of the preceding LEI/strategy edition. It is not a newly validated production sanctions solution.

Run `dotnet restore flix.slnx`, `dotnet build flix.slnx`, and `dotnet run --project tests/Flix.SmokeTests` in an environment with the .NET 10 SDK.
