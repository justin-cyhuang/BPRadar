# BPRadar

Best-Practice Radar — assesses organizations against compliance/best-practice
frameworks (ISO 27001, ISO 20000-1, Azure WAF) and, as of this context entry,
maps real-world operational issues back to those frameworks to flag likely
best-practice violations.

## Language

**Issue**:
A problem opened in an external system (e.g. incident/ticketing tool) and
manually re-entered into BPRadar for best-practice-violation analysis. BPRadar
does not create, own, or track the lifecycle of the original problem — it
only records a reference snapshot of it.
_Avoid_: Ticket, Incident, Problem (BPRadar does not model incident lifecycle)

**Root Cause**:
The known cause of an Issue, already determined by an external
process/system (e.g. incident postmortem, RCA tooling) *before* the Issue is
entered into BPRadar. Stored as a distinct field from the Issue's
description — BPRadar does not perform root cause analysis itself, only
consumes an already-identified root cause as input to violation matching.
_Avoid_: Cause, Diagnosis

**Control Keyword**:
An admin-curated trigger phrase/synonym attached to a Control (e.g. Control
`A.8.13` "Information backup" tagged with `backup`, `restore`, `data loss`).
Used as the match target for LLM-extracted keywords during violation
matching. Distinct from `Control.Title`/`Control.Description`, which stay
short paraphrased prose and are not reliable literal match targets.
_Avoid_: Tag, Synonym (use Control Keyword specifically in this context)

**Violation Match**:
A candidate link between an Issue and a Control, produced by matching
LLM-extracted keywords (from the Issue's Root Cause) against that Control's
Control Keywords, using fuzzy/stemmed comparison. Represents "this Control
is plausibly implicated by this Issue" — not yet a confirmed violation.
_Avoid_: Violation (reserve "Violation" for a confirmed/accepted match, once
that distinction is decided)

**Self-Reported State**:
What an IT admin believes/configured for a Control, captured via the
existing Survey (`SurveyResponse.ResponseLevel`) or a full Assessment
(`AssessmentResult.Status`). Both are admin-declared, not independently
verified — they represent expectation/belief, not observed fact.
_Avoid_: Current situation (too vague — always say Self-Reported State when
this specific meaning is intended)

**Observed State**:
What actually happened, as revealed by an Issue's Root Cause. Independent
of — and potentially contradicting — the Self-Reported State for the same
Control. The gap between Self-Reported State and Observed State is a
distinct finding from the gap between Observed State and the Control
baseline itself.
_Avoid_: Actual state, Ground truth

**Self-Assessment Discrepancy**:
A Violation Match where the Self-Reported State claimed compliance (or a
high maturity level) for a Control, but the Issue's Root Cause shows the
Control was actually violated. Ranked higher-priority than an ordinary gap
(where the Self-Reported State already acknowledged weakness), because it
reveals the org's own records are wrong, not just that a control is unmet.
_Avoid_: Gap, Finding (both too generic — reserve this term for the
specific "believed compliant but wasn't" case)
