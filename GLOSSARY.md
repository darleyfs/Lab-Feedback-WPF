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

## Submissions

**Submission**:
What a student turned in for one assignment, after unzipping and keeping only the latest attempt.
_Avoid_: project, upload, attempt

**Starter file**:
A file from the course template that students don't write, such as `Utility.h` or `Console.cpp`. Starter files are hidden from the grader.
_Avoid_: excluded file, template file

**Primary file**:
The student file shown first when a submission is opened.
_Avoid_: main file

**Build log**:
The autograder's history of scores for a submission, one entry per build.
_Avoid_: results file, hdkvkt

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

## Feedback

**Feedback**:
The HTML a student receives for one submission: remarks, each rubric row or test, deductions, and the final grade.
_Avoid_: report, output

**Remarks**:
The opening message at the top of the feedback.
_Avoid_: intro, comments (comments belong to a single rubric row or test)

**Deduction**:
Points taken off the whole grade for breaking a course rule, such as multiple returns in a method.
_Avoid_: penalty

**Auto-zero**:
A deduction that makes the grade 0, such as breaking the External Resource Policy.
_Avoid_: violation

**Bad submission**:
An auto-zero for a submission that can't be graded; its remarks ask the student to resubmit.
_Avoid_: invalid submission
