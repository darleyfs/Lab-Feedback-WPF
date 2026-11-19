# Markers load from the staff repo's markers.json, and stay out of feedback

PG1 starters carry instructions (AGENTS.md, CLAUDE.md, copilot-instructions.md and a `#pragma region Notice` in each student file) that ask any AI agent writing code to add a section tag. The grader looks for those tags as markers. It reads the tokens from the path in the `LAB_FEEDBACK_MARKERS` environment variable, which points at `markers.json` in the private staff repo. Markers are shown only in the grader's status bar, tooltip and line highlights.

## Considered Options

- **Hard-code the tokens here.** Rejected: this repo is public, so one search would reveal every token.
- **Report markers as violations.** Rejected: a violation is evidence for an auto-zero. A marker only opens a conversation, and it can be a false positive.
- **Put markers in the feedback.** Rejected: the first flagged student would learn the scheme.

## Consequences

Markers are off until the environment variable is set. The token format (prefix plus three characters) is fixed in `MarkerCheck`, so any token with the prefix is reported, even one from another term. A student who reads the starter files can remove the tags, so markers add to the banned-term checks but don't replace them.
