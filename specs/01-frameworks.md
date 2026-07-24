# 01 — Frameworks in scope

Each framework is modeled generically as **Framework → Domain → Control**
(see `02-data-model.md`). This document describes how the three MVP
frameworks map onto that structure, and how a new framework is added later.

> Note: control lists below use publicly known structure (numbering, pillar
> names, clause names) and short, original paraphrased descriptions for
> mapping purposes. They are not verbatim reproductions of the paid ISO
> standard text.

## 1. ISO/IEC 27001:2022 — Information Security Management
- **Framework**: `ISO 27001:2022`
- **Domain** = one of the 4 Annex A themes:
  - A.5 Organizational controls
  - A.6 People controls
  - A.7 Physical controls
  - A.8 Technological controls
- **Control** = one of the 93 Annex A controls (e.g. `A.5.1 Policies for
  information security`, `A.8.24 Use of cryptography`), each with:
  - control code (e.g. `A.5.1`)
  - short title
  - 1–2 sentence paraphrased purpose/description
  - optional guidance link (learn.microsoft.com / iso.org landing pages, not
    paywalled full text)
- Assessment status per control uses the shared status enum in
  `02-data-model.md` (Compliant / Partial / Non-compliant / N/A).

## 2. ISO/IEC 20000-1 — Service Management System (SMS)
- **Framework**: `ISO/IEC 20000-1`
- **Domain** = major clause group, e.g.:
  - Context of the organization
  - Leadership
  - Planning
  - Support of the SMS
  - Operation (service management processes: incident, problem, change,
    capacity, availability, service level, etc.)
  - Performance evaluation
  - Improvement
- **Control** = individual requirement/clause item under each domain (e.g.
  "Incident management process is defined and followed"), with the same
  code/title/description/status shape as above.

## 3. Microsoft Azure Well-Architected Framework (WAF)
- **Framework**: `Azure Well-Architected Framework`
- **Domain** = one of the 5 pillars:
  - Reliability
  - Security
  - Cost Optimization
  - Operational Excellence
  - Performance Efficiency
- **Control** = the 59 official checklist/design-review items published per
  pillar on learn.microsoft.com, using Microsoft's own codes as-is (e.g.
  `RE:01`, `SE:05`, `CO:02`), each with short title, paraphrased description,
  and a guidance link back to the official Microsoft Learn page for that item.
- Source reference: https://learn.microsoft.com/azure/well-architected/
- **Full control catalog, seeding shape, and reference links: see
  `08-waf.md`** — that file is the authoritative WAF seed-data spec; keep
  this section as the short summary only.

## Adding a new framework later
Because Framework/Domain/Control are plain data rows (not hard-coded types),
adding a new framework requires only:
1. A new `Framework` row (name, version, description).
2. `Domain` rows under it.
3. `Control` rows under each domain.
4. Optional seed script for convenience — no schema or core code changes.

This keeps BPRadar extensible to future frameworks (e.g. NIST CSF, CIS
Controls, SOC 2) without re-architecting.
