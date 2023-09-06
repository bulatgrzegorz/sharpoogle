# sharpoogle

Sharpoogle is a search engine for C# language, which allows you to find method by it's signature. Project is inspired by [Hoogle](https://hoogle.haskell.org/) and work of [Tscoding](https://github.com/tsoding).
It is using [roslyn](https://github.com/dotnet/roslyn) as c# parser for both input query and searched files. Then we are able to compare signatures, finding best match using [Levenshtein distance](https://en.wikipedia.org/wiki/Levenshtein_distance).

## Installing

Sharpoogle is avaiable on nuget: https://www.nuget.org/packages/sharpoogle/ as dotnet tool.
You can install it by simply running:
```cmd
dotnet tool install --global sharpoogle --version 1.0.0
```

## Usage

After installing you can simply run:
```cmd
sharpoogle -h
```
in order to get command help.

Using tool you can either search in files in given directory (recursive search) or in specific file only.

Then command is expecting query input (`-q|--query`) in format of method signature, for example: `void (int, bool?)`. It is also possible to just pass single type: `Task<int>`.
