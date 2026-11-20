# Markers load from the staff repo's markers.json, and stay out of feedback

PG1 starters carry instructions (AGENTS.md, CLAUDE.md, copilot-instructions.md and a `#pragma region Notice` in each student file) that ask any AI agent writing code to end its local variable names with the assignment's section code. The grader reports identifiers with that ending as markers. It reads the codes from the path in the `LAB_FEEDBACK_MARKERS` environment variable, which points at `markers.json` in the private staff repo. Markers are shown only in the grader's status bar, tooltip and line highlights.

## Considered Options

- **Hard-code the codes here.** Rejected: this repo is public, so one search would reveal every code.
- **Visible tags (an `assisted:` comment label, or a `#pragma region` named after the code).** Rejected: students spot them and delete them.
- **Suffix method names too.** Rejected: the tests call student methods by name.
- **Report markers as violations.** Rejected: a violation is evidence for an auto-zero. A marker only opens a conversation, and it can be a false positive.
- **Put markers in the feedback.** Rejected: the first flagged student would learn the scheme.

## Consequences

Markers are off until the environment variable is set. Comments are skipped, because the starter's own notice spells out the code. A two-letter suffix can occasionally match by chance, and a student who reads the notice can rename the variables, so markers add to the banned-term checks rather than replace them.
