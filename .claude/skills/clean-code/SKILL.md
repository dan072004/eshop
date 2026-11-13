---
name: clean-code
description: Clean Code rules (Robert C. Martin) for C# — newspaper metaphor, one level of abstraction per method, small well-named functions, no hidden side effects. Use this skill whenever you write, add, edit, refactor or review any .cs file in this repo, even for a small method, and whenever the user says the code is hard to read, messy, "not clean", or asks to clean up, simplify or restructure code.
---

# Clean Code

Code that buries the main flow under Console output, file I/O and JSON plumbing is hard to read. The goal of every change is that a reader can open a file, read the top method, and understand what happens without scrolling through details.

When refactoring existing code, keep behavior identical (same output, same files written) and run `dotnet build` afterwards.

## Scope: whole file, then whole project
Every file should read top-down, not just the method they pointed at. A clean method sitting in a file that still reads bottom-up is still hard to follow, so:

- **Any file you edit:** after the requested change, reorder the *whole file* into newspaper order (rule 1). Public entry points go first, and each callee sits below its first caller. Shared low-level helpers (JSON, HTML, schema builders) go at the bottom. Moving methods doesn't change behavior, so it's always safe.
- **Other rule violations in that file:** fix the cheap, local ones (magic values, naming, extracting from mixed-abstraction methods). List the bigger ones (signature changes that ripple into other files, introducing typed records) in your reply instead of doing them unasked.
- **A project-wide request** ("clean up the project", "apply clean code everywhere"): go file by file through every `.cs` outside `obj/` and `bin/`. Run `dotnet build` after each file, so a break is easy to trace. Finish with a short per-file summary.
- **Whenever you only touched one file:** end your reply by offering the project-wide pass in one line.

## The two core rules

These matter most; most of the other rules follow from them.

### 1. Newspaper metaphor (stepdown rule)
A class reads top to bottom like an article. The headline is the public entry method, which summarizes the whole flow. Each method it calls sits directly below, in call order, one level more detailed. The reader can stop reading at whatever depth they need, and never has to jump up and down the file.

### 2. One level of abstraction per method
A method either **orchestrates** (calls named steps) or **does details** (file I/O, Console, string formatting, LINQ, JSON). Mixing them makes the reader switch between "what" and "how" on every line, and the main logic gets lost.

```csharp
// Before: the flow is buried among details
public async Task<JsonArray> RunEvaluation(...)
{
    var dataset = JsonNode.Parse(await File.ReadAllTextAsync(datasetFile))!.AsArray();
    var results = await RunConcurrently(dataset, tc => RunTestCase(tc!, ...), "Graded", "test cases");
    var averageScore = results.Average(r => (int)r["score"]!);
    Console.WriteLine($"Average score: {averageScore:0.##}");
    await File.WriteAllTextAsync(jsonOutputFile, ...);
    await File.WriteAllTextAsync(htmlOutputFile, GenerateHtmlReport(results));
    Console.WriteLine($"Saved results to ...");
    ...
}

// After: reads like a table of contents; details live in the methods below it
public async Task<JsonArray> RunEvaluation(...)
{
    var dataset = await LoadDataset(datasetFile);
    var results = await GradeAll(dataset, runPrompt, extraCriteria);
    PrintAverageScore(results);
    await SaveReports(results, options);
    return results;
}
```

## Supporting rules

3. **Small methods that do one thing.** Roughly 5–15 lines. If you can extract a piece and give it a name that says more than its code does, extract it. Don't extract for its own sake: a one-line wrapper whose name just repeats the code adds a jump without adding meaning.

4. **Names that reveal intent.** Methods are verbs (`GradeOutput`, `LoadDataset`), data are nouns (`testCases`, `averageScore`). Avoid vague names like `func`, `list`, `data`, `temp`. Name the *what*, not the *how* (`GradeAll`, not `RunWithSemaphore`). A good name is what lets the orchestrating method read like prose.
   **Constants are PascalCase:** every `const`, whether a field or a local, and whatever its access (`private`, `internal`, `public`). Write `const string Model`, `const int MaxTokens`, never `model` or `maxTokens`. In an object initializer, `Model = Model` is fine: the right-hand side resolves to the constant. Named-argument labels still follow the parameter names (`Chat(messages, model: Model)`).

5. **Few arguments.** 0–2 is ideal, 3 at most. Group related arguments into a `record` (e.g. `EvalOptions(string DatasetFile, string JsonOutput, string HtmlOutput)`). A bool flag means the method does two things, so split it into two methods.
   When the same arguments are passed down a whole call chain (for example `runPromptFunction` and `extraCriteria` going `GradeAll` → `RunTestCase` → `GradeOutput`), they are the object's state, so make them constructor parameters. Don't use settable properties, which leave the object half-initialized until someone sets them. Settings that are passed on to helper methods but have sensible defaults (output file paths) become `init`-only properties with those defaults, set in an object initializer. That keeps the constructor at 3 parameters or fewer and the object immutable. Values that only the entry method uses, and that change on every call (the input dataset), stay as parameters.

6. **No hidden side effects (command–query separation).** A method that returns data shouldn't also print or write files. A method that prints or saves shouldn't also compute the business result. This way a caller can tell from the name what will happen.

7. **Cross-cutting concerns stay out of the logic.** Progress reporting, logging, concurrency and persistence each live in their own methods or helpers. Example: `RunConcurrently` shouldn't build `"{verb} {n}/{count} {noun}"` strings. Give it an `Action<int, int> onProgress` and let the caller decide what to print.

8. **Comments explain *why*, not *what*.** If a comment restates the code, a better name is the fix. Keep comments about intent, trade-offs and non-obvious API behavior.

9. **DRY.** Pull repeated snippets (JSON parse/serialize, prompt building, message setup) into helpers. Reuse the helpers the codebase already has instead of rewriting them.

10. **No magic values.** Model IDs, file paths, limits and format strings get a named `const` or `static readonly` field, so their meaning and their single place of change are both obvious.

11. **Vertical formatting.** Fields and constants at the top. Related lines together, a blank line between separate ideas. One class per concept.

12. **Error handling is one thing.** A `try/catch` gets its own method with nothing else in it. Prefer throwing, or returning an empty collection, over returning `null`.

13. **Typed data inside, raw JSON at the edges.** Convert `JsonNode` into `record`s where data enters (loading a file, parsing a response) and back to JSON where it leaves. Code like `(int)r["score"]!` in the middle of the logic hides what the data is.

14. **Classes: single responsibility, high cohesion.** A class has one reason to change. Test for cohesion by grouping the methods by the fields and helpers they use. If the groups don't touch each other's data (say, dataset generation vs grading vs HTML reporting), each group is its own class. Shared plumbing (the structured-output call, throttled concurrency, JSON save/load) goes into small helper classes that all of them use, instead of being copied. Example: a `PromptEvaluator` that did all of it became `DatasetGenerator`, `PromptEvaluator`, `EvaluationReport`, `StructuredChat`, `ThrottledRunner` and `EvalJson`. Splitting changes callers in other files, so when editing unasked, list the split in your reply rather than doing it. When asked to clean up a file or the project, do it.

## When reviewing instead of editing
Go through the file top-down. For each issue, name the rule, the method, and a concrete fix (for example, which methods to extract and what to call them). Lead with the issues under rules 1–2, because they hurt readability most.

## Checklist before finishing
- Does the *whole file* read top-down, entry point first, callees below in call order?
- Does every method stay at one level of abstraction?
- Does every class have one responsibility, with all its methods working on the same data?
- Is every method short, with a name that says what it does?
- Do all methods take 3 or fewer arguments, and no bool flags?
- Are query methods free of Console/file side effects?
- Are progress, logging and concurrency kept out of the business logic?
- Are there no magic values and no duplicated snippets?
- Are all `const` names (fields and locals) PascalCase?
- Does `dotnet build` succeed?
