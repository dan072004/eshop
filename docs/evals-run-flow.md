# How an evals run flows

One run of `tests/ShoppingAssistant.Evals`, from `EvalSuiteTests` down to the results store, and how CI (`.github/workflows/evals.yml`) reports it. A static copy is in [evals-run-flow.png](evals-run-flow.png); this Mermaid source is the one to edit.

Results leave the test along two paths:

- **The evidence for each sample.** Reporting writes it when a scenario run is disposed (step 19), and `SampleScoring` reads it back (steps 20–21), so the gate scores exactly what `dotnet aieval report` shows.
- **The verdict.** `RunGate` writes `summary.md` and `evals.json` to the root of the store (step 27), and CI turns them into the job summary, the PR comment and the badge.

A failed model or judge call becomes `NoVerdict`, an infrastructure error, never a failed check.

```mermaid
sequenceDiagram
    autonumber
    participant Suite as EvalSuiteTests
    participant Cal as CalibrationRun
    participant SS as SampleScoring
    participant CR as CaseRunner
    participant Model as CachedModel (Haiku)
    participant Chat as ChatState (WebApp pipeline)
    participant Tools as World stubs + policy-api
    participant Rep as Reporting ScenarioRun
    participant Judge as RubricEvaluator (Sonnet)
    participant Store as DiskBasedResultStore
    participant Gate as RunGate
    participant CI as evals.yml

    Suite->>Cal: OnceAsync (concurrent, skipped when filtered)
    Cal->>Store: judge-calibration.* scenarios

    loop each case (4 in parallel) x 3 samples
        Suite->>SS: RunAsync(case, iteration)
        SS->>CR: RunAsync(case, iteration)
        CR->>CR: Services() + CachingKeysAsync()
        CR->>Model: OpenAsync(caseId, iteration, keys)
        Model-->>CR: CachedSample (client, calls, usage)
        CR->>Chat: ClaudeChatPipeline.Build + AddUserMessageAsync per turn
        Chat->>Model: chat request (cache hit or real call)
        Chat->>Tools: tool calls (orders, catalog, search_policies)
        Tools-->>Chat: tool results
        alt model call failed
            CR-->>SS: throw ModelCallFailedException
            SS-->>Suite: NoVerdict (infrastructure error)
        else answered
            CR-->>SS: CaseTranscript (messages, keys, usage, origin)
            SS->>Rep: CreateScenarioRunAsync + EvaluateAsync
            Rep->>Rep: 5 deterministic evaluators
            Rep->>Judge: rubric items
            Judge-->>Rep: verdicts (cached judge call)
            Rep->>Store: dispose writes results/ExecutionName/case/iteration
            SS->>Store: ReadResultsAsync
            Store-->>SS: ScenarioRunResult
            SS->>SS: Score() - check or judge error becomes NoVerdict
            SS-->>Suite: SampleResult (failures, usage, origin, knownMiss)
        end
    end

    Cal-->>Suite: CalibrationOutcome (judge errors)
    Suite->>Gate: Decide(samples, calibration, settings)
    Gate-->>Suite: GateOutcome (verdict, summary.md, badge JSON)
    Suite->>Store: WriteToAsync - summary.md + evals.json at store root
    Suite->>Suite: AssertPassed sets the dotnet test exit code

    CI->>Store: copy summary.md + evals.json to evals-out
    CI->>Store: aieval report to report.html, clean-cache, clean-results -n 5
    CI->>CI: job summary, upload evals-out
    CI->>CI: sticky PR comment from summary.md
    CI->>CI: on main, push evals.json to the badges branch
```
