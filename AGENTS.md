# ME.BECS agent instructions

These instructions apply to all code in this ME.BECS repository. Follow applicable parent `AGENTS.md` instructions as well.

## C# code style

- Compare boolean values explicitly in conditions: `if (boolValue == true)` or `if (boolValue == false)`. Do not use `if (boolValue)` or `if (!boolValue)`. Apply this to boolean members and boolean-returning expressions too.
- A single-line `if` is allowed only when its body performs one action and there is no `else` branch. Use braces for bodies containing multiple actions.
- Every `if/else` chain must use braces around every branch, including `else if` branches. Do not write an unbraced `if/else` chain, even when each branch contains only one action.
- Follow these rules when adding or modifying code. Do not reformat unrelated code solely to enforce them.

Examples:

```csharp
if (isReady == false) return;

if (isReady == true) {
    Initialize();
    Refresh();
}

if (isReady == true) {
    Start();
} else if (isWaiting == true) {
    Wait();
} else {
    Stop();
}
```
