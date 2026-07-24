# 08 — Azure Well-Architected Framework (WAF) benchmark detail

This spec is the authoritative seed-data reference for the `Azure Well-Architected
Framework` entry described generically in `01-frameworks.md`. It exists so the
WAF catalog can be implemented and maintained without re-researching Microsoft
Learn every time. Descriptions below are short, original paraphrases for
mapping/seeding purposes — they are **not** verbatim copies of Microsoft Learn
text, and BPRadar must always link back to the official page rather than
reproduce full guidance.

## 1. Source of truth

- **Framework home**: https://learn.microsoft.com/en-us/azure/well-architected/
- **Official assessment tool** (what BPRadar's WAF benchmark is modeled after):
  https://learn.microsoft.com/en-us/assessments/azure-architecture-review/
  — Microsoft's own web assessment that scores a workload per pillar using
  the same checklist items listed below.
- **Pillar index**: https://learn.microsoft.com/en-us/azure/well-architected/pillars
- Each pillar publishes a **"Design review checklist"** page
  (`/{pillar}/checklist`) containing a numbered table of `Code | Recommendation`
  rows. That table is the canonical control list BPRadar seeds from. Each row
  also links to a dedicated guide article (one per control) with detailed
  implementation recommendations, tradeoffs, and Azure service mapping —
  these per-control URLs should be stored as the `GuidanceUrl` on the seeded
  `Control` row (see §4).
- Last verified against Microsoft Learn: 2026-07-24. Pillar checklist pages
  show `ms.update-cycle: 1095-days` (~3 years) and are periodically revised —
  see §6 for the re-verification process.

## 2. Domain = Pillar mapping

| Domain (Pillar) | Code prefix | Control count | Checklist URL |
| --- | --- | --- | --- |
| Reliability | `RE` | 10 | `/azure/well-architected/reliability/checklist` |
| Security | `SE` | 12 | `/azure/well-architected/security/checklist` |
| Cost Optimization | `CO` | 14 | `/azure/well-architected/cost-optimization/checklist` |
| Operational Excellence | `OE` | 11 | `/azure/well-architected/operational-excellence/checklist` |
| Performance Efficiency | `PE` | 12 | `/azure/well-architected/performance-efficiency/checklist` |

**Total: 59 controls.** `ControlCode` in BPRadar = the Microsoft code as-is
(e.g. `RE:01`), so imported CSV/XLSX rows using the official codes align
automatically (see `04-import.md` alignment rules).

## 3. Full control catalog (paraphrased)

Each control below shows `Code — short title` and a one-line paraphrased
intent. `GuidanceUrl` = `https://learn.microsoft.com/en-us/azure/well-architected/{pillar-path}/{slug}`.

### 3.1 Reliability (`RE`) — pillar path `reliability`
| Code | Slug | Short title | Paraphrased intent |
| --- | --- | --- | --- |
| RE:01 | `simplify` | Simplicity & efficiency | Keep the design as simple as the business requirements allow; avoid unneeded complexity. |
| RE:02 | `identify-flows` | Flow identification & criticality | Enumerate user/system flows and rank them by business criticality. |
| RE:03 | `failure-mode-analysis` | Failure mode analysis | Systematically identify dependencies, failure points, and mitigations (FMEA-style). |
| RE:04 | `metrics` | Reliability targets | Set explicit availability/recovery targets (SLO/RTO/RPO) to drive design and health modeling. |
| RE:05 | `redundancy` | Redundancy | Add redundant components/instances for critical flows to meet reliability targets. |
| RE:06 | `scaling` | Scaling strategy | Implement timely, mostly-automatic scaling across app/data/infra tiers. |
| RE:07 | `self-preservation` | Self-healing | Build in self-preservation/self-healing so the workload degrades gracefully and recovers. |
| RE:08 | `reliability-test` | Resiliency testing / chaos engineering | Proactively test failure and load scenarios rather than waiting for real incidents. |
| RE:09 | `disaster-recovery` | Disaster recovery plans | Maintain a tested, documented DR plan covering the whole system, not just pieces. |
| RE:10 | `monitoring` | Health monitoring | Continuously track uptime/reliability signals to support detection and post-incident review. |

### 3.2 Security (`SE`) — pillar path `security`
| Code | Slug | Short title | Paraphrased intent |
| --- | --- | --- | --- |
| SE:01 | `establish-baseline` | Security baseline | Define a security baseline aligned to compliance/industry/platform norms and measure against it. |
| SE:02 | `secure-development-lifecycle` | Secure SDLC | Bake security into every phase of the development lifecycle. |
| SE:03 | `data-classification` | Data classification | Label data/systems by sensitivity to drive design and prioritization decisions. |
| SE:04 | `segmentation` | Segmentation | Create intentional boundaries across network, identity, and resource organization. |
| SE:05 | `identity-access` | Identity & access management | Enforce least-privilege, auditable, modern IAM for all users/components. |
| SE:06 | `networking` | Network controls | Filter and isolate ingress/egress traffic with defense-in-depth. |
| SE:07 | `encryption` | Encryption | Encrypt data at rest/in transit using modern, platform-native methods aligned to classification. |
| SE:08 | `harden-resources` | Hardening | Reduce attack surface by tightening configuration on every component. |
| SE:09 | `application-secrets` | Secrets management | Protect, rotate, and audit application secrets. |
| SE:10 | `monitor-threats` | Threat detection | Run a holistic, modern threat-monitoring strategy feeding SecOps processes. |
| SE:11 | `test` | Security testing | Combine testing approaches to validate prevention and detection controls. |
| SE:12 | `incident-response` | Incident response | Define and rehearse incident response procedures with clear ownership. |

### 3.3 Cost Optimization (`CO`) — pillar path `cost-optimization`
| Code | Slug | Short title | Paraphrased intent |
| --- | --- | --- | --- |
| CO:01 | `create-culture-financial-responsibility` | Financial responsibility culture | Train teams and foster spend accountability and automation. |
| CO:02 | `cost-model` | Cost model | Maintain an estimate of initial/run-rate/ongoing costs with budget buffer. |
| CO:03 | `collect-review-cost-data` | Cost data review | Capture daily cost data, trends, forecasts; automate anomaly alerts. |
| CO:04 | `set-spending-guardrails` | Spending guardrails | Use policy/limits/gates to prevent overspend, favoring automation. |
| CO:05 | `get-best-rates` | Best rates | Regularly review pricing tiers/models/commitments/purchasing plans. |
| CO:06 | `align-usage-to-billing-increments` | Billing alignment | Match resource usage patterns to how the service actually bills. |
| CO:07 | `optimize-component-costs` | Component cost optimization | Remove/right-size unneeded or underused features and resources. |
| CO:08 | `optimize-environment-costs` | Environment cost optimization | Align spend across prod/non-prod/DR environments to actual need. |
| CO:09 | `optimize-flow-costs` | Flow cost optimization | Spend proportional to each flow's business priority. |
| CO:10 | `optimize-data-costs` | Data cost optimization | Tune tiering, retention, replication, and storage format for cost. |
| CO:11 | `optimize-code-costs` | Code cost optimization | Adjust code so it needs fewer/cheaper resources for the same requirements. |
| CO:12 | `optimize-scaling-costs` | Scaling cost optimization | Choose scale-unit configurations and demand-shaping to control cost. |
| CO:13 | `optimize-personnel-time` | Personnel time optimization | Reduce toil (noisy alerts, slow builds, poor debugging) that wastes engineer time. |
| CO:14 | `consolidation` | Consolidation | Increase density and reuse shared/centralized resources where possible. |

### 3.4 Operational Excellence (`OE`) — pillar path `operational-excellence`
| Code | Slug | Short title | Paraphrased intent |
| --- | --- | --- | --- |
| OE:01 | `devops-culture` | DevOps culture | Define shared practices and a blameless, continuous-improvement culture. |
| OE:02 | `formalize-operations-tasks` | Standardize operations | Make routine/ad-hoc/emergency operations consistent and predictable. |
| OE:03 | `formalize-development-practices` | Standardize development | Formalize and make transparent the full software delivery lifecycle. |
| OE:04 | `tools-processes` | Tooling & process standards | Standardize tools, source control, patterns, and documentation. |
| OE:05 | `infrastructure-as-code-design` | Infrastructure as code | Provision via IaC, preferring declarative approaches, for consistency. |
| OE:06 | `workload-supply-chain` | Deployment supply chain | Drive change through automated, tested pipelines across environments. |
| OE:07 | `observability` | Observability / instrumentation | Design telemetry/metrics/logging that validates decisions and guides improvement. |
| OE:08 | `incident-response` | Incident management process | Define roles/procedures for rapid detection, diagnosis, recovery. |
| OE:09 | `testing` | Quality/testing practices | Align testing approach with business objectives and quality bars. |
| OE:10 | `enable-automation` | Automation | Automate repetitive, high-ROI tasks reliably and securely. |
| OE:11 | `safe-deployments` | Safe deployment practices | Use small, gated, progressive releases with rollback plans. |

### 3.5 Performance Efficiency (`PE`) — pillar path `performance-efficiency`
| Code | Slug | Short title | Paraphrased intent |
| --- | --- | --- | --- |
| PE:01 | `performance-targets` | Performance targets | Set numeric, requirement-driven performance targets per flow. |
| PE:02 | `capacity-planning` | Capacity planning | Plan capacity ahead of predictable demand changes. |
| PE:03 | `select-services` | Service selection | Choose services/tiers that can hit targets and absorb capacity changes. |
| PE:04 | `monitoring` | Performance monitoring | Measure performance consistently over time against baselines. |
| PE:05 | `scale-partition` | Scaling & partitioning | Design scale units and partitioning for controlled, reliable scaling. |
| PE:06 | `performance-test` | Performance testing | Regularly test in production-like environments against targets. |
| PE:07 | `optimize-code-infrastructure` | Code/infra optimization | Keep code/infra lean, performant, and platform-offloaded. |
| PE:08 | `optimize-data-performance` | Data performance optimization | Tune stores, partitions, and indexes for actual access patterns. |
| PE:09 | `prioritize-critical-flows` | Critical flow prioritization | Focus optimization effort on the highest-priority flows first. |
| PE:10 | `optimize-operational-tasks` | Operational task impact | Minimize how routine ops (backups, scans, rotations) hurt performance. |
| PE:11 | `respond-live-performance-issues` | Live incident response | Have clear ownership/communication for live performance problems. |
| PE:12 | `continuous-performance-optimize` | Continuous optimization | Keep monitoring and tuning components that degrade over time. |

## 4. Seeding shape (maps to `02-data-model.md`)

```
Framework: { Code: "AZURE_WAF", Name: "Azure Well-Architected Framework", Version: "2024-11" }
Domain:    { FrameworkId, Code: "RE", Name: "Reliability", SortOrder: 1 }
Control:   { DomainId, Code: "RE:01", Title: "Simplicity & efficiency",
             Description: "<paraphrase from §3.1>",
             GuidanceUrl: "https://learn.microsoft.com/en-us/azure/well-architected/reliability/simplify" }
```
- `Framework.Version` should track the WAF content revision informally (e.g.
  a date string), since Microsoft does not publish a single WAF version
  number the way ISO does. Bump it whenever re-verification (§6) finds
  material changes (added/removed/renumbered controls).
- Domain `SortOrder` follows the pillar order Microsoft presents them in:
  Reliability, Security, Cost Optimization, Operational Excellence,
  Performance Efficiency.
- Seed data lives as a JSON/CSV fixture checked into the repo (exact location
  decided at Phase 1 bootstrap — see `06-tech-stack.md`), loaded by an
  idempotent seeding routine keyed on `FrameworkCode + ControlCode` so re-runs
  update titles/links without duplicating rows.

## 5. Guidance for scoring, weighting, and dashboard use

- **Default weighting**: treat all 59 controls as equal weight (score
  contribution = 1/59) for MVP. Do not invent a Microsoft-endorsed weighting
  scheme — the official assessment tool's exact internal weighting isn't
  published; note this as an assumption in the UI ("Equal-weighted per
  control; not an official Microsoft score").
- **Domain rollup**: pillar/domain score = average of its controls' scores
  (Reliability = average of RE:01–RE:10, etc.), consistent with how the
  Azure Well-Architected Review presents a per-pillar percentage.
- **Do not claim parity with Azure Advisor score** — Advisor score
  (`/azure/advisor/azure-advisor-score`) is derived from live resource
  telemetry/configuration, not a manual/import-based checklist. BPRadar's WAF
  benchmark is a design-review self-assessment, a different (complementary)
  measurement. Call this out in `05-dashboard.md` copy if not already clear.
- **Baseline/target profiles** (`02-data-model.md` `BaselineTarget`): a
  sensible WAF starter baseline is "all 59 controls at Compliant" for a
  greenfield target, or a lower per-pillar target for legacy workloads —
  leave the actual numbers to the user/org, do not hardcode assumptions.

## 6. Keeping this spec current

Microsoft revises WAF checklist pages periodically (each page's front-matter
`ms.date`/`updated_at` shows the last edit). Before each major re-seed:
1. Refetch each `/{pillar}/checklist` page listed in §2.
2. Diff the `Code | Recommendation` table against §3 of this file.
3. If codes were added/removed/renumbered, update this file first, then the
   seed fixture, then bump `Framework.Version`.
4. Never copy full recommendation paragraphs verbatim — keep paraphrasing to
   one line, and always keep `GuidanceUrl` pointing at the live Microsoft page
   so users can read full official guidance there.

## 7. Further reference (for implementers)

- Assessment tool (start an assessment): https://learn.microsoft.com/en-us/assessments/azure-architecture-review/
- Pillar overview matrix: https://learn.microsoft.com/en-us/azure/well-architected/pillars
- Per-pillar design principles (useful for in-app tooltips/help text):
  - Reliability: https://learn.microsoft.com/en-us/azure/well-architected/reliability/principles
  - Security: https://learn.microsoft.com/en-us/azure/well-architected/security/principles
  - Cost Optimization: https://learn.microsoft.com/en-us/azure/well-architected/cost-optimization/principles
  - Operational Excellence: https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/principles
  - Performance Efficiency: https://learn.microsoft.com/en-us/azure/well-architected/performance-efficiency/principles
- Training path (background reading, not required for coding):
  https://learn.microsoft.com/en-us/training/paths/azure-well-architected-framework/
- Azure Architecture Center reference architectures (context for evidence
  links users may attach to a control result):
  https://learn.microsoft.com/en-us/azure/architecture/browse/
- Azure Advisor / Advisor score (related but distinct telemetry-based score,
  useful for a future "cross-check" feature, out of scope for MVP):
  https://learn.microsoft.com/en-us/azure/advisor/azure-advisor-score

## 8. Relationship to other specs
- `01-frameworks.md` §3 stays the short summary; this file is the detailed
  seed reference it points to.
- `02-data-model.md` — `Control.GuidanceUrl` field should be added if not
  already present, to store the per-control Microsoft Learn link from §3/§4.
- `04-import.md` — `ControlCode` values for WAF rows must match §3 exactly
  (e.g. `RE:01`, not `RE-01` or `WAF-REL-01`); update the import spec's
  example rows to use real codes instead of placeholders.
- `06-tech-stack.md` — seeding routine location/approach.
