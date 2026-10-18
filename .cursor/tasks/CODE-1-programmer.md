# CODE-1 — programmer slot (DeepSeek)

Owner: `programmer`. Model: `deepseek-v4-pro`. Runs next to walk, ops, dog, bird, flora.

Job, in order:

1. If the build status above says BROKEN, fix that error with the smallest possible edit.
2. Review the files the other slots changed in the last hour for the failure classes we already paid for: a local that shadows an outer local, a switch no reader consumes any more, a gate that can never pass, a flag written but never read.
3. Write findings and fixes into `Trials/Runs/opt_agent_proposals.md`, section `programmer`.

Acceptance: the trial build compiles, and every fix names file, line, cause, change.