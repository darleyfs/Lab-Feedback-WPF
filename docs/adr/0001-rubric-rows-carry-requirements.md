# Rubric rows carry their requirements and thresholds in results.json

Practical rubric rows are graded "X of Y": the level depends on how many requirements are met. So the grader can tick or untick requirements and get a correct score, LabTester's `Rubric::Save` writes each row's requirements (id, label, met, note) and level thresholds, plus the rubric's level percentages, into `results.json`. Lab Feedback recalculates the row's level and points with the same rule LabTester uses. Rows without requirements (Labs, and Practicals graded before this change) keep the old editable-points behaviour.

## Considered Options

- **Parse LabTester's text description and `missing:` tags.** Rejected: the format is fragile, and it doesn't include the thresholds or level percentages, so the level can't be recalculated.
- **Hard-code each Practical's rubric in Lab Feedback.** Rejected: rubrics already live in the Practical grader DLLs, and keeping a second copy would drift.
- **Per-row hand-written rules (the older HTML grading forms).** Rejected: Practical rubrics are uniformly threshold-based, and LabTester already models that.

## Consequences

The level rule now exists in two places, LabTester (C++) and Lab Feedback (C#), and the two must stay in step. Practical grader DLLs have to be rebuilt against the new LabTester before their results include requirements.
