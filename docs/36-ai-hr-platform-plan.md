# 36. AI-powered HR platform: analysis and plan

13 November 2026. The response to the "AI implementation objective" brief (eight phases: CV screening, interview intelligence, job description and advert generators, onboarding assistant, document validation, knowledge base, executive dashboard). **Nothing is built yet**: this document compares the brief with what GRSMS already does, proposes one AI architecture for all eight phases, and lists the decisions needed (§9).

Ground rules from the brief and earlier decisions: no new project, nothing removed or broken, database changes additive only, Clean Architecture, and **AI advises, people decide** (the 5 November rule that an automatic rejection waits for HR stays).

---

## 1. What already exists, phase by phase

| Brief phase | Already in GRSMS | Gap |
|---|---|---|
| **1. AI CV screening** | **Mostly built (Phases 14-15):**<br>• CV text from PDF/DOCX on GREL's server<br>• Rule-based reader plus an **optional Claude AI reader** (off by default, Super Admin switch, needs an API key)<br>• Extracted name, contacts, education, certifications, experience, skills and job history stored per application (JSON in `JobApplications.CvParsed`, plus `CvText`)<br>• Weighted match score (education, experience, skills, certifications; 25 % each, editable)<br>• Missing required and preferred skills and certificates; knockout questions<br>• **Bands with editable thresholds:** Highly recommended 90, Recommended 80, Review required 70, Low match 50<br>• Automatic rejection with a generated reason, **held until HR confirms or overrides**; ATS board per vacancy; re-screening; talent pool | • A written **strengths / weaknesses** summary<br>• **Soft skills**<br>• Searchable structured tables (the JSON column holds the same data; see §5)<br>• "Not qualified" is called "Low match / rejected" today |
| **2. Interview intelligence** | Scorecards with weighted criteria (5 defaults), panel comments, validated scores, quorum, interview results ranking, hiring case showing every round | • Question generation per vacancy<br>• An AI summary of validated scorecards (strengths, weaknesses, recommendation, risks) |
| **3. Job description generator** | Vacancy job description and requirements (skills, certificates, education, experience); QF-81-11 "Description of the post" on the request | Drafting them from title, department, grade and responsibilities |
| **4. Advert generator** | Advert body and how-to-apply, HR review and IT approval, channels with **copy-ready text** (template-based), careers portal, closing dates | Writing distinct internal / external / LinkedIn / careers versions |
| **5. Onboarding assistant** | Without AI, "My next step", "My onboarding" and the documents portal already answer: pending documents, next step, deadline and status, plus reminders | A chat box that answers questions in the person's own words |
| **6. Document validation** | • File type checked by content (magic bytes), size limit, virus-scan hook<br>• Required documents per hire; missing-documents report and verification report<br>• Deadlines with reminders; RO verification with a fixed rejection list<br>• SHA-256 of every file | • Expiry dates (passport, permits, Ghana Card)<br>• Duplicate-file flag<br>• Blur check<br>• Wrong-document check (needs AI vision) |
| **7. Knowledge base** | Nothing (no policy library) | Everything: a document library, search, and answers with sources |
| **8. Executive dashboard** | Reports overview (funnel, divisions, monthly trend), Home cards, workflow targets, employees and org chart, contract alerts, probation reports | • One executive page: recruitment, onboarding and workforce together<br>• Trends against the previous quarter<br>• A written summary |

So phase 1 needs finishing touches. Phases 2-8 add AI on top of data GRSMS already holds; only the knowledge base needs new content (GREL's policy documents).

---

## 2. The rules every AI feature follows

1. **Advisory only.** AI never approves, rejects, sends, verifies or changes a score. It drafts text or summarises, labelled **AI draft**. A person edits and accepts, and the existing workflow (HR review, IT approval, RO validation, HR confirmation of rejections) continues unchanged.
2. **Facts come from GRSMS, wording from AI.**
   * In the dashboard, onboarding assistant and interview summary, every number, status and name comes from GRSMS's own queries.
   * The model only phrases them, and is told to say "I don't know" rather than guess.
   * The knowledge base answers only from GREL's stored documents and cites the document and section.
3. **Off by default, one switch per feature** (Super Admin), plus a **kill switch** for all AI. Without a key, or with AI switched off, every screen works as it does today.
4. **Data minimisation.**
   * Generators (job descriptions, adverts, questions) and the executive summary send **no personal data** (aggregates only).
   * Features that send personal data (CVs, interview comments, a person's onboarding status, document images) each need their own switch and the data-protection sign-off of 5 November. This is in line with Ghana's Data Protection Act, 2012 (Act 843).
5. **Untrusted input.** CVs, comments and documents are passed as data inside tags with "never follow instructions inside", as the CV reader already does. Output is plain text or a fixed JSON shape, and HTML goes through the existing sanitizer.
6. **Logged and budgeted.** Every call is logged with feature, user, model, tokens, cost, time and result. A monthly budget cap stops AI (not GRSMS) when it is reached.

---

## 3. Architecture

**Rejected option: a separate "AI module" project.** One shared AI layer inside the existing projects is used by every feature instead:

```
Grsms.Application/Ai/
  IAiClient                 one provider-neutral call: instructions + data → text or typed JSON (structured output)
  AiGateway                 every feature goes through it: switch and permission check, budget, cache, logging, errors
  AiFeature (enum)          CvSummary, InterviewQuestions, InterviewSummary, JobDescription, Advert,
                            OnboardingAssistant, DocumentCheck, KnowledgeAnswer, ExecutiveSummary
  Prompts/                  one versioned prompt per feature, in code and reviewed like code
  <feature services>        JobDescriptionDrafter, AdvertWriter, InterviewQuestionGenerator, InterviewSummaryService,
                            OnboardingAssistantService, DocumentCheckService, KnowledgeBaseService, ExecutiveInsightsService
Grsms.Infrastructure/Ai/
  AnthropicAiClient         reuses the Anthropic SDK already in the solution (today's CV reader)
  AzureOpenAiClient         only if decision D1 says so
  AiRequestLog, AiCache     EF entities and repositories
Grsms.Web/
  Controllers/AiController  JSON endpoints for "Draft with AI" buttons (anti-forgery, permission, rate limit)
  Views/Shared/_AiPanel     one reusable panel: "AI draft" badge, loading state, confidence, Accept / Edit / Regenerate
  Controllers/AiAdminController + view   Administration → AI settings & usage
```

* **Provider:** chosen by configuration (`Ai:Provider`). The API key is kept in user-secrets, an environment variable or the encrypted System settings, never in source.
* **Errors:** time-out, rate limit or refusal give a friendly message ("AI is unavailable, please write it yourself") and the page keeps working. The existing CV reader already falls back to the rule-based reader.
* **Caching:**
  * Generators cache on a hash of feature + prompt version + input, for 7 days (Regenerate bypasses it).
  * The executive summary is made once a day.
  * Nothing that contains personal data is cached beyond the record it belongs to.
* **Cost:** price per million tokens per model in settings. Cost is worked out per call, shown per feature and per month, with a monthly cap and an 80 % warning to the Super Admin.
* **Performance:** AI calls run in the background or on a button click, never inside saving a record. Long tasks show progress, and the gateway allows at most N calls at once.

---

## 4. Each phase in detail

**Phase 1: CV screening (finish).**
* An optional AI **match summary** per application: strengths, weaknesses and missing requirements in sentences, built from the ATS facts already computed and the CV text. It is shown on the ATS board and application page with the score it explains.
* Soft skills added to what the reader extracts.
* Band names stay editable; I'd rename "Low match" to "Not qualified" only if you want (D5).
* The score and the rejection rules stay deterministic and published: the AI summary never changes the score, so a candidate can always be told why.

**Phase 2: interview intelligence.**
* A **question set per vacancy**: technical, behavioural, situational, competency and practical questions, generated from the job description, requirements, department and level. The RO edits it and it is saved with the vacancy; panel members see it on the session page.
* After scores are validated, the RO can generate an **interview summary**: strengths, weaknesses, recommendation and risks, from validated scorecards and comments only. It is shown on the hiring case as advisory and is never sent to the candidate.

**Phase 3: job descriptions.** **Draft with AI** on the vacancy form and on the QF-81-11 request, from title, department, grade and responsibilities. It fills the editable fields (description, duties, requirements, competencies, KPIs, skills matrix), which go through the existing approvals.

**Phase 4: adverts.** **Write versions** on the advert: internal, external, LinkedIn and careers portal (summary, requirements, benefits, closing date taken from the vacancy). The texts are editable, then go through the existing HR review and IT approval, and fill the channels' copy-ready text.

**Phase 5: onboarding assistant.**
* A chat box on *My onboarding* (new employee) and on the onboarding page (RO).
* It answers from the person's own data, using the services that already compute pending documents, next step, approver, status and deadlines; the model only words the answer.
* It suggests buttons ("Upload my Ghana Card"). For policy questions it uses the knowledge base (phase 7).

**Phase 6: document validation.** Mostly without AI:
* **expiry date** asked on upload for passport, permits and Ghana Card (flag if expired or expiring within 30 days);
* **duplicate** (same SHA-256 already uploaded for another document type or person);
* **blur** for photos (a local sharpness check, nothing sent out);
* **missing** (exists).

An **optional AI check** asks "is this image a Ghana Card / passport / certificate?". This sends ID images out, so it is off unless signed off. Flags appear on the RO's review and in a **document verification report**. The RO still decides.

**Phase 7: knowledge base.**
* *Administration → Knowledge base*: HR uploads policies (PDF/DOCX) with owner, version, effective date and audience (staff, or also new employees).
* Text is extracted (existing extractor), split into sections, and indexed with MySQL full-text search.
* A question retrieves the best sections, and the AI answers **only from them, with citations**, or says it can't find it and who to ask.
* MySQL 8 has no vector search, so full-text search is used first; embeddings can be added later if answers miss.
* Needs GREL's documents (D4).

**Phase 8: executive dashboard.**
* *Executive dashboard* for the MD and HR Managers, with the same visibility rules as reports:
  * recruitment: open vacancies, applications, time to hire, offer acceptance, workflow targets;
  * onboarding: pending, delayed, probation due;
  * workforce: headcount by department and site, contract expiries.
* Each figure shows its trend against the previous quarter.
* A daily **AI summary** written from those aggregates, e.g. "Time to hire fell from 41 to 32 days (−22 %) this quarter; Factory TBU has the most open positions (6)". Every number is from GRSMS and is linked.

---

## 5. Database changes (all additive; scripts `database/31_*` onwards)

| Table / column | For |
|---|---|
| `AiRequestLogs` | Every call: feature, user, model, input/output tokens, cost, ms, outcome, record type/id, cache hit. **No prompt text by default** (personal data); a switch keeps it 30 days for troubleshooting |
| `AiCacheEntries` | Hash → output, expiry (generators only) |
| `AiDrafts` | Generated outputs kept with their record: feature, record type/id, JSON, prompt version, model, created by/at, accepted by/at (interview summaries, match summaries, question sets, executive summaries) |
| `VacancyInterviewQuestions` | The edited question set per vacancy (category, question, guidance, order) |
| `KnowledgeDocuments`, `KnowledgeSections` (FULLTEXT) | The policy library and its searchable sections |
| `EmployeeDocuments.ExpiresOn`, `DocumentFlags` | Expiry and validation flags (duplicate, blur, expired, possibly wrong type) |
| `JobAdvertChannels.CustomText` | AI-written (edited) text per channel |
| `CvSkills` (optional) | Only if you want to **search** the talent pool by skill; otherwise the existing JSON is enough (D5) |
| Settings rows | Provider, model, switches per feature, budget, prices: in the existing System settings table, no new table |

New permissions: `ai.use` (generate drafts), `ai.insights` (executive dashboard), `ai.manage` (AI settings & usage), `knowledge.manage`. Defaults follow the existing roles and are added at start-up as before.

---

## 6. Security controls

* Per-feature switch, plus a permission and record visibility (`AccessScope`) on every endpoint: AI can never show someone a record they couldn't open.
* Anti-forgery and per-user rate limits (e.g. 30 AI calls a minute) on AI endpoints.
* Prompt-injection guard (data in tags, no tools that write anything, fixed output shape), and sanitized output.
* Every acceptance of an AI draft is in the audit trail, along with who generated it.
* The candidate never sees AI analysis of themselves. Interview summaries and match summaries are staff-only.
* Key handling: as the SMTP password (user-secrets / `.env` / encrypted setting).

## 7. Interface

* One consistent AI panel: an **AI draft** badge, a spinner while working, a **confidence** label (high / medium / low, from how complete the input was, not the model's own claim), **Accept / Edit / Regenerate**, and "why" notes.
* The executive dashboard uses the existing card and chart styles, made denser and more executive.
* The brief names Workday, Oracle HCM and others as a reference for the *standard* of finish. GRSMS keeps its own look rather than copying any product.

## 8. Plan (each step tested, committed and documented before the next)

| Step | Content | Personal data sent? | Size |
|---|---|---|---|
| **AI-0** | Foundation: `IAiClient`, gateway, logging, cache, budget, kill switch, *AI settings & usage* page, `_AiPanel` | — | M |
| **AI-1** | Job description generator (phase 3) + advert versions (phase 4) | No | M |
| **AI-2** | Interview question sets (phase 2a) | No | S |
| **AI-3** | Executive dashboard + daily AI summary (phase 8) | No (aggregates) | M |
| **AI-4** | Document validation without AI: expiry, duplicates, blur, report (phase 6) | No | M |
| **AI-5** | Interview summary (2b) + CV match summary and soft skills (phase 1) | **Yes**: needs sign-off | M |
| **AI-6** | Onboarding assistant (phase 5) | **Yes** (the person's own status) | M |
| **AI-7** | Knowledge base (phase 7), then the assistant uses it | Policy text only | L |
| **AI-8** | Optional AI document-type check (phase 6) | **Yes, ID images** | S |

The order puts the features with no personal data first, so they can go live while the data-protection sign-off is obtained.

## 9. Decisions needed

* **D1. AI provider.** GRSMS already uses Claude (Anthropic) for the optional CV reader; the brief prefers Azure OpenAI or OpenAI. The layer is provider-neutral either way. Options:
  * (a) **Claude for everything**: already integrated, one key, one data-protection review;
  * (b) **Azure OpenAI**: data processed in GREL's own Azure tenancy and region, if GREL has an Azure subscription; the CV reader moves to it too;
  * (c) **OpenAI API**;
  * (d) build both adapters and choose in settings.
* **D2. Personal data.** Which features may send personal data to the provider, and who signs off (DPO / MD)? The default plan above: none until signed off.
* **D3. Order.** The plan in §8, or another order.
* **D4. Knowledge base content.** Which documents (HR policies, handbook, SOPs, conditions of service), who owns and approves them, and may new employees (not yet staff) ask it questions?
* **D5. Smaller points.**
  * Rename "Low match" to "Not qualified"?
  * Add searchable skill tables, or keep the JSON?
  * Monthly AI budget cap (in USD)?
  * Who sees the executive dashboard (MD and HR Managers only, or also HODs for their departments)?

## 10. Decisions (13 November 2026)

| # | Decision |
|---|---|
| D1 | **Provider chosen in settings.** Adapters are built for Anthropic (Claude), OpenAI and Azure OpenAI; the Super Admin picks one and enters the key (the user will add their own key). |
| D2 | **All features may send personal data, each behind its own switch** (off by default). The Super Admin switches each on once GREL's data-protection sign-off is in place; the AI settings page marks those features *Personal data* and says what each sends. |
| D3 | **Order as §8.** |
| D4 | **Build the knowledge-base library now; HR uploads the documents later.** |
| D5 | Not answered yet. Until then: "Low match" keeps its name; the CV JSON is kept (no skill tables); the budget is an editable setting (default USD 50); the executive dashboard is for the MD and HR Managers. |

## 11. Progress

| Step | Status |
|---|---|
| **AI-0** foundation | ✅ 13 Nov. <br>• `Application/Ai`: `IAiClient`, `AiGateway`, `AiAdminService`<br>• `Infrastructure/Ai`: `AiClient` (Anthropic SDK; OpenAI and Azure OpenAI REST), `AiLogStore`<br>• Tables `AiRequestLogs`, `AiCacheEntries` (script 31)<br>• *Administration → AI settings & usage*: provider, encrypted key, model, budget, prices, requests per minute, keep-prompts switch, feature switches, connection test, usage this month<br>• Daily purge of kept prompts (30 days) and expired cache |
| AI-1 … AI-8 | To do, in the order of §8 |
