# Lab Feedback

A grading tool for PG1 assignments: it reads the autograder's results for each student's Lab or Practical, lets the grader adjust the score, and produces the HTML feedback the student receives.

## Assignments

**Lab**:
An assignment graded by automated unit tests against functions the student writes.

**Practical**:
A free-form console program assignment graded against a rubric.

**Test**:
One automated unit test in a Lab. Only Labs have tests.
_Avoid_: using "test" for a Practical's rubric row

## Rubrics

**Rubric row**:
One graded line of an assignment, worth a fixed number of points (its weight).
_Avoid_: Section, column, test result

**Requirement**:
One checkable feature within a rubric row that a submission either meets or misses.
_Avoid_: sub-check, criterion

**Level**:
The band a rubric row earns: Exemplary, Effective, Developing, Beginning, Insufficient, or Not Attempted. Each level awards a set percentage of the row's weight.
_Avoid_: grade value, rating

**Threshold**:
The fewest requirements a rubric row must meet to reach a given level, e.g. "4 of 5" for Exemplary.
_Avoid_: minimum, cutoff
