# Kirkuk Institute ERP — Concept Plan & System Blueprint

**Phase:** Concept and architecture review only  
**Date:** 15 November 2026  
**Prepared for:** Institute ownership / management review  
**Status:** No code, database script, or implementation work has been started.

---

## 0. Executive Summary

The existing application is already a credible **institute management system**. It is not yet a complete ERP, but it is much closer than a blank project: it has real admissions, courses and sections, weekly schedules, attendance, exams, receipts, a double-entry ledger, teacher shares, payroll, rentals, private lessons, teacher/parent/student portals, audit logs, backups, and a local read-only AI assistant.

The gap is not "more screens." The gap is the **control layer that makes paper unnecessary**:

- a student file that is truly digital, with documents, ID card, guardian account, and lifecycle;
- a timetable that produces real sessions, handles cancellations and substitutes, and connects to attendance;
- tuition agreements with installment plans, allocations, refunds, and statements;
- a cashier function with shift closing, receipt verification, and cash variance control;
- teacher contracts with clear share rules and monthly settlement statements;
- exams that roll into term averages, report cards, and certificates;
- parent communication over WhatsApp and SMS, not just an in-app inbox;
- reporting that reconciles operations to the ledger.

The recommended path is to **evolve the current application into a modular monolith**, not rewrite it. The existing permanent student number, ledger, audit trail, role model, bilingual interface, and AI isolation are assets worth preserving.

"Zero paper" should be defined precisely: **paper is no longer the system of record**. A receipt, report card, or attendance sheet may still be printed as a projection or legal archive, but the authoritative record, the number, the approval, and the audit live in the system.

Three structural moves achieve most of the transformation:

1. **Make the student/family record the digital file.** Admission produces a permanent ID, document checklist, ID card with QR, guardian portal invitation, and a 360-degree timeline.
2. **Make money document-driven.** Every enrollment produces an agreement and installment plan; every collection produces an immutable receipt with QR; every day closes with a cash count; every teacher settlement produces a statement.
3. **Make time session-driven.** The timetable produces dated session instances; attendance, substitution, make-up classes, exams, and teacher pay all attach to the session, not to an informal date on a paper sheet.

### Current foundation scorecard

| Area | Current state | Verdict |
|---|---|---|
| Identity and roles | 8 roles, claims-based, role-scoped portals | Strong |
| Admissions | Staff and public registration, permanent IDs, national-ID uniqueness | Strong core, incomplete lifecycle |
| Academic catalog | Stages, grades, subjects, courses, sections | Adequate, no terms/curriculum |
| Scheduling | Weekly recurring slots, hall conflict check | Partial |
| Attendance | Section/date register, change requests, absence notices | Partial |
| Exams | Course-level exams and mark entry | Partial |
| Billing | One due per enrollment, receipts, funds, teacher share | Partial |
| Accounting | Chart of accounts, journals, expenses, payroll, payables | Good base, no statements/close |
| Teachers | Profiles, availability, earnings, portal | Partial, no contracts/attendance |
| HR | Employees, staff attendance, payroll, payslips | Good base |
| Parent engagement | Portal and in-app notifications | Weak reach |
| Reporting | Operational dashboards and reports | Operational, not statutory |
| Platform | Audit, backups, exports, AR/EN, read-only AI | Strong but single-branch |

---

## 1. The Operational Reality of a Kirkuk Private Institute

The design must start from how these institutes actually run, not from generic school-ERP assumptions. A Kirkuk tutoring center, language institute, or exam-preparation academy is typically:

- **cash-first**, with daily cash handed to the owner;
- **WhatsApp-first**, with almost no email use by guardians;
- **seasonal**, with registration peaks, an evening main year, and an intensive summer;
- **teacher-led**, where star teachers bring their own students and negotiate percentages or room rent;
- **paper-heavy at the front desk**, even when teaching is modern;
- **constrained by electricity and internet**, so a purely cloud-dependent tool will fail;
- **family-run**, which makes trust, transparency, and fraud control as important as features;
- **Arabic-first**, with English for some course names and Kurdish spoken by a meaningful share of families.

### 1.1 The annual rhythm

| Period | What happens operationally | What the system must support |
|---|---|---|
| Pre-season (August–September) | Advertising, phone inquiries, walk-ins, placement tests, early registration, discounts, waiting lists | Lead capture, trial sessions, capacity, offers, deposits |
| Main year (September–May) | Evening classes, weekly rhythm, monthly exams, installment collection, parent follow-up | Timetable, sessions, attendance, billing, report cards |
| Exam season (May–June) | Mock ministerial exams, intensive revision, extra halls, temporary teachers | Exam operations, seating, intensive courses, fast enrollment |
| Summer (June–September) | Short intensive courses, new cohorts, hourly teachers, promotional pricing | Short programs, hourly pay, rapid registration, refunds |
| Ramadan and holidays | Compressed timetable, changed hours, lower attendance | Calendar exceptions, make-up sessions, rescheduling |
| Year end | Teacher settlements, retention decisions, promotion, archives | Settlement, rollover, retention reports, archive |

### 1.2 The daily reality at the front desk

A typical day looks like this:

- **Morning:** phone calls, walk-in inquiries, registration, fee collection, schedule questions, teacher availability checks.
- **Afternoon:** printing attendance sheets, calling substitute teachers, preparing halls, confirming classes.
- **Evening:** classes run, teachers arrive, attendance is taken on paper, absent students are called, parents ask about results and fees.
- **Night:** cash is counted, receipts are reconciled, teacher payouts are prepared, tomorrow's schedule is confirmed.

This environment punishes complexity. A cashier must collect a payment in under thirty seconds. A teacher must take attendance in under two minutes on a phone. A supervisor must replace an absent teacher in under five minutes. The system must be Arabic-first, large-button, keyboard-friendly, and printable when needed.

### 1.3 The paper artifacts that actually exist

These are the records that a complete ERP must replace, not abstractly but one by one.

| # | Paper artifact | Who uses it | What goes wrong |
|---|---|---|---|
| 1 | استمارة تسجيل الطالب — registration form | Registrar | Illegible, incomplete, duplicated |
| 2 | سجل الطلاب العام — master student ledger | Registrar | Manual numbering, lost pages |
| 3 | ملف الطالب — student file | Registrar | Missing ID copies, photos, certificates |
| 4 | بطاقة الطالب — paper student card | Student | Easy to forge, no verification |
| 5 | دفتر الوصولات — receipt book | Cashier | Lost books, duplicate numbers, no audit |
| 6 | دفتر الأقساط / الديون — dues notebook | Accountant | Outdated balances, disputed payments |
| 7 | وصل خصم / تخفيض — discount slip | Manager | Unapproved discounts, no trace |
| 8 | الجدول الأسبوعي — timetable board | Supervisor | Clashes, last-minute changes not communicated |
| 9 | كشف الحضور — attendance sheet | Teacher | Lost sheets, late data entry, disputes |
| 10 | سجل الغيابات الشهري — absence register | Supervisor | No early warning, no parent proof |
| 11 | سجل الدرجات — marks ledger | Teacher | Arithmetic errors, rewritten pages |
| 12 | ورقة علامات الامتحان — exam mark sheet | Teacher | Lost marks, no moderation |
| 13 | بطاقة النتائج / شهادة — result card or certificate | Registrar | Handwritten, hard to verify |
| 14 | كشف حساب المدرس — teacher settlement sheet | Accountant/owner | Percentage disputes, unclear deductions |
| 15 | جدول الرواتب — payroll sheet | HR/owner | Manual deductions, no approval trail |
| 16 | دفتر حضور الموظفين — staff attendance book | HR | Buddy punching, lost records |
| 17 | سند صرف / فاتورة — expense voucher | Accountant | Missing receipts, cash leakage |
| 18 | دفتر الصندوق اليومي — daily cash book | Cashier | No reconciliation, no variance record |
| 19 | سجل الأصول والجرد — asset and inventory register | Admin | Missing equipment, no maintenance history |
| 20 | عقود الإيجار — rental contracts | Manager | Renewal missed, payment untracked |
| 21 | دفتر حجز القاعات — hall booking diary | Supervisor | Double bookings, unpaid rentals |
| 22 | قائمة الكتب والملازم — book and lecture-note list | Admin | Stock-outs, untracked sales |
| 23 | دفتر المراسلات مع أولياء الأمور — guardian correspondence book | Supervisor | No proof of notification |
| 24 | محضر الاجتماعات — meeting minutes | Manager | Decisions lost |
| 25 | طلب صيانة — maintenance request | Admin | No prioritization or history |
| 26 | طلب شراء — purchase request | Admin | No approval or vendor comparison |
| 27 | سجل السلف — advances register | HR | Unrecovered advances |
| 28 | سجل الإجازات — leave register | HR | Coverage gaps |
| 29 | استمارة درس خصوصي — private lesson request | Supervisor | Lost requests, slow matching |
| 30 | سجل اتصالات الغياب — absence call log | Supervisor | Parents deny being informed |
| 31 | خطة الامتحانات والجلوس — exam and seating plan | Supervisor | Hall conflicts, crowded exams |
| 32 | ملف الامتثال الوزاري — compliance binder | Owner | Inspection stress, missing documents |
| 33 | استمارة انسحاب / نقل — withdrawal or transfer form | Registrar | No clearance, refund disputes |
| 34 | دفتر الدروس التجريبية — trial class sheet | Marketing | Leads forgotten, no follow-up |

### 1.4 The real failure modes

These are the problems the ERP must eliminate, not just digitize around:

- **Receipt disputes.** A guardian claims payment; the carbon copy is missing; the notebook has no entry.
- **Cash leakage.** Collections are not reconciled to receipts at the end of the day.
- **Teacher share disputes.** The contract was "55%," but nobody agrees whether that is on collected, billed, discounted, or after refunds.
- **Double-booked halls and teachers.** The paper timetable is changed in three places and nobody updates the fourth.
- **Uncovered classes.** A teacher cancels; students arrive; no substitute; parents complain.
- **Lost attendance.** The sheet disappears and the absence cannot be proven.
- **Slow, inaccurate marks.** Marks are copied twice, averages are calculated by hand, report cards are rewritten.
- **Invisible debt.** The dues notebook is weeks behind; the owner does not know real receivables.
- **No profitability truth.** The owner cannot see which course, teacher, subject, or hall actually makes money.
- **Parent communication amnesia.** Calls are made, but there is no log, no delivery proof, no follow-up.
- **Duplicate students.** The same child is registered twice under different spellings or guardian phones.
- **Month-end chaos.** Closing the month takes days and never fully reconciles.

### 1.5 Local constraints and the design response

| Constraint | Design response |
|---|---|
| Cash-first payments | Cash drawer, denomination count, daily close, receipt QR, variance approval |
| WhatsApp is the real communication channel | WhatsApp Business integration, SMS fallback, in-app archive, delivery log |
| Unstable electricity | On-premises server with UPS, low-power clients, printable fallbacks |
| Unstable internet | Offline attendance capture, queued synchronization, retry queues |
| Arabic-first, some Kurdish | Arabic RTL default, English secondary, Kurdish as a later language pack |
| Low digital literacy among some staff | Large controls, guided wizards, role dashboards, one-page SOPs, keyboard shortcuts |
| Price sensitivity | Modular rollout, no per-message surprises, use existing hardware where possible |
| Gender-separated classes or shifts | Section/shift rules, optional teacher-gender constraints, separate timetables |
| Data sovereignty concerns | Local hosting, local AI, encrypted backups, no student data sent to external AI |
| SQL Server Express on the current machine | Plan an edition upgrade or archival strategy before data growth |
| Family-run trust dynamics | Transparent statements, segregation of duties, immutable receipts, audit trail |

---

## 2. As-Is Audit of the Existing Application

### 2.1 Technical baseline

| Layer | Current implementation |
|---|---|
| Framework | ASP.NET Core 9, MVC with Razor views |
| Data | EF Core with SQL Server, `EnsureCreated` plus idempotent upgrade scripts |
| Identity | ASP.NET Identity, 8 roles, claims principal factory |
| UI | Tailwind-based design, Arabic/English, RTL support |
| Documents | ClosedXML for Excel export, QRCoder referenced but not used for printed verification |
| AI | Local Ollama model, read-only database principal, read-only tool registry, API keys, rate limiting |
| Background work | Startup seeding only; no job scheduler for reminders or recurring billing |
| Files | Local uploads for student photos and expense attachments |
| Hosting | Assumes a local Windows/SQL Server Express environment |

### 2.2 What already works and must be preserved

- **Permanent student number.** `KRI-YYYY-NNNN` is allocated atomically per year, protected by a unique index and a database trigger that rejects later changes. This is exactly the kind of invariant a paper ledger never guarantees.
- **Double-entry accounting base.** A seeded chart of accounts, balanced journal entries, cash and bank funds, expense approval, income, payroll postings, and teacher payables.
- **Teacher share accrual.** When a tuition payment is received, the teacher's percentage is calculated and recorded, and the ledger posts the corresponding expense and payable.
- **Attendance change control.** Once attendance is saved, edits require a formal request and leadership approval. This is a strong anti-tampering pattern.
- **Role-scoped portals.** Teachers, parents, and students each have a dedicated portal rather than a generic admin screen.
- **Bilingual and RTL.** Arabic is the default culture, with English switching and Arabic collation in the database.
- **Audit trail.** Significant actions are recorded with user, entity, old/new values, IP address, and timestamp.
- **Backups and exports.** Database backup, download, delete, and dataset exports already exist.
- **Read-only AI isolation.** The assistant uses a separate context, a SELECT-only guard, a dedicated database principal, and local model hosting. This is a meaningful privacy and safety differentiator.
- **Public registration.** The website can capture a registration and allocate the same permanent number as the front desk.

### 2.3 Module-by-module audit

| Area | What exists | Material gap | Severity |
|---|---|---|---|
| Admissions | Staff and public registration, national ID uniqueness, permanent ID, photo, hall/subject/teacher links, Excel export | No application pipeline, document checklist, ID card, guardian account provisioning, duplicate merge, waitlist, sibling/family rollup, capacity enforcement on staff side | High |
| Student record | Profile, enrollments, dues, payments, attendance, results | No digital document vault, timeline, transfer/withdrawal clearance, document expiry | High |
| Academic catalog | Stage, grade, subject, course, section | No academic year, term, curriculum, or program template | Medium |
| Scheduling | Weekly recurring slots, hall conflict check | No teacher or cohort conflict check, no term dates, holidays, session instances, substitutions, make-up classes, published versions, external booking conflict check | High |
| Attendance | Section/date register, change requests, absence in-app notice | No session-level attendance, QR check-in, teacher attendance, absence threshold automation, parent acknowledgement, printable backup, offline mode | High |
| Exams | Course-level exams, mark entry, pass mark, failure notice | No assessment plan, weights, terms, moderation, report cards, ranking, certificates, exam scheduling or seating | High |
| Billing | One due per enrollment, receipts, cash/bank fund, teacher share, journal, in-app notice | No installment plans, fee catalog, discount approval, refunds, credit notes, advance balances, payment allocation across invoices, statements, late-fee policy | High |
| Cash control | Receipt number and payment list | No cashier shift, drawer, blind count, daily close, variance approval, receipt void/reprint control, offline receipt blocks | High |
| Accounting | Chart of accounts, journal, funds, expenses, income, payroll, teacher payables | No trial balance, balance sheet, income statement, cash flow, period lock, bank reconciliation, vendor bills, procurement, asset depreciation postings | High |
| Teachers | Profiles, subjects, grades, availability, ratings, earnings, portal | No employment/contract rules, teacher attendance, advances, deductions, settlement statements, approval, substitute pay, minimum guarantee, workload limits | High |
| HR | Employees, staff attendance, payroll generation, approval, payment, payslip | No leave management, advances, overtime entry, contracts, document expiry, staff tasks | Medium |
| Parent engagement | Parent portal, in-app notifications, announcements | No WhatsApp, SMS, email, push, templates, delivery log, reply inbox, call log, meeting booking, consent, account provisioning at scale | High |
| Reporting | Dashboard, profit by course/teacher/subject/room, attendance, dues, payroll, enrollments | No statutory statements, student/teacher statements, attendance eligibility, exam analytics, funnel, retention, scheduled reports, print/PDF catalogue | Medium-High |
| Private lessons | Request, match, schedule, complete, pay, rate | No recurring series, teacher acceptance, travel fee, safety check-in, cancellation policy, package pricing | Medium |
| Rentals | Hall bookings, cafeteria contract, teacher room rent | No conflict with internal timetable, deposits, invoices, contract documents | Medium |
| Assets and inventory | Asset model only; compliance page mentions assets | No asset screens, labels, assignment, maintenance tickets, depreciation posting, stock, procurement | Medium |
| Platform | Roles, audit, backup, exports, AI, bilingual | No branch management or scoping, no granular permissions, no migration framework, no job scheduler, no restore workflow, no automated tests | High |
| Data integrity | Unique indexes, immutable student number, Arabic collation | Receipt and journal numbering are count-based and can race; no period lock; no idempotency keys | Medium-High |

### 2.4 Critical findings

1. **There is no session instance.** Attendance is stored against a section and a date. That cannot represent two sessions on the same day, a cancelled session, a substitute teacher, a make-up class, or an exam occupying the same slot. This is the single most important structural gap for scheduling and attendance.
2. **There is no installment agreement.** One enrollment creates one due. Real institutes collect two to four installments, sometimes monthly, with different due dates per family and subject.
3. **There is no cash control.** Without a cashier shift and daily close, the system cannot prove that recorded receipts match the cash in the drawer.
4. **There is no teacher contract or settlement.** The percentage is applied when a payment arrives, but there is no contract rule, no monthly statement, no approval, no advances, and no teacher attendance. This is where trust breaks in real institutes.
5. **Parents cannot be reached at scale.** Notifications are in-app only, and most guardians do not have portal accounts. The application itself acknowledges that no external email or SMS is sent.
6. **The QR token is unused.** A QR value is generated for each student, and the QRCoder package is referenced, but no ID card or receipt verification uses it.
7. **Exams do not become report cards.** Marks are stored per exam, but there is no term, weighting, average, rank, report card, or certificate.
8. **Branches exist in the data model but not in operations.** There is no branch management screen, no branch scoping, and no consolidated-versus-branch reporting.
9. **Schema evolution is fragile.** `EnsureCreated` never alters an existing database, so upgrades rely on hand-written idempotent scripts. A full ERP needs a controlled migration baseline.
10. **There are no automated tests** around money, attendance, numbering, or ledger invariants. These are exactly the areas where silent errors destroy trust.

### 2.5 What not to break

- The permanent student number and its immutability trigger.
- The balanced journal and fund balance model.
- The role-scoped portal separation.
- The read-only AI isolation.
- Arabic collation and RTL behavior.
- Whole-dinar money storage; IQD does not use fractional coins in practice.
- The existing audit log and backup foundation.

---

## 3. Target Operating Model

### 3.1 Definition of "zero paper"

Paper is eliminated when all of the following are true:

1. Every operational decision can be made from the system without consulting a paper record.
2. Every document that used to be paper has a digital origin, a unique number, an owner, a status, and a printable projection.
3. Every change to a financial or academic record is auditable and reversible only through a compensating document, never by deletion.
4. Every guardian communication is logged with delivery status.
5. Every cash movement reconciles to a receipt, a journal entry, and a daily cash count.
6. Every schedule change reaches the affected teacher, student, and guardian automatically.

Paper may still exist in four legitimate forms: a printed receipt handed to a guardian, a printed report card or certificate, a scanned legal document, and a signed contract that requires a wet signature. In all four cases, the digital record remains authoritative.

### 3.2 Seven design laws

1. **Record once.** A student's name, phone, fee, or attendance is entered in one place and referenced everywhere else.
2. **Every number has a document.** A balance is not a number in a table; it is the sum of invoices, payments, credits, and refunds.
3. **Every document has an identity.** Receipts, invoices, report cards, certificates, contracts, and settlements have numbers, QR verification, status, and version.
4. **Never delete history.** Void, reverse, credit, or supersede. Deletion is reserved for drafts that were never issued.
5. **Every workflow has an owner and an SLA.** An unassigned request is a lost request.
6. **Every guardian interaction is logged.** A phone call is a record, not a memory.
7. **Every module reconciles.** Enrollment reconciles to billing, billing to cash, cash to the ledger, teacher earnings to settlements, attendance to sessions.

### 3.3 The paperless operating loop

```
Lead / Inquiry
   → Application + Documents
      → Admission Offer + Deposit
         → Enrollment Agreement + Installment Plan
            → Section + Timetable + Session Instances
               → Attendance + Absence Communication
                  → Assessments + Report Cards
                     → Billing + Receipts + Cash Close
                        → Teacher Accrual + Settlement + Payout
                           → Ledger + Statements + Analytics
                              → Retention / Renewal / Alumni
```

Each arrow is a handoff that used to be a paper sheet passed between people. In the ERP, each arrow is a typed event with a status, an owner, and an audit trail.

### 3.4 Print equivalence

The system must be able to produce every document the institute hands to a person, in the format that person expects.

| Document | Typical format | Digital identity |
|---|---|---|
| Tuition receipt | A5 or 80 mm thermal | Receipt number + QR verification |
| Invoice / installment notice | A5 or WhatsApp PDF | Invoice number + due date |
| Student ID card | PVC or laminated card | Permanent student number + QR |
| Attendance sheet backup | A4 | Section + session ID |
| Report card | A4 | Student + term + verification code |
| Completion certificate | A4 or A5 | Certificate number + QR |
| Teacher settlement statement | A4 | Settlement number + period |
| Payslip | A5 | Payroll period + employee code |
| Enrollment agreement | A4 | Agreement number + signatures |
| Official letter | Letterhead A4 | Reference number + registry |
| Withdrawal clearance | A4 | Clearance number |

---

## 4. Module Blueprint

The target is **16 modules** grouped into four layers. The eight areas the owner explicitly named are treated as core and described in depth; the remaining modules close the paper gaps that surround them.

| Layer | Module | Primary paper replaced | Priority |
|---|---|---|---|
| Front office | M1 CRM, Admissions and Permanent Identity | Registration form, master ledger, student file, ID card | Core |
| Front office | M2 Student and Family 360 | Student file, correspondence book | Core |
| Academic | M3 Academic Structure and Calendar | Year plan, curriculum sheets | Core |
| Academic | M4 Halls, Resources and Timetable | Timetable board, hall diary | Core |
| Academic | M5 Attendance and Session Operations | Attendance sheets, absence register | Core |
| Academic | M6 Exams, Gradebook and Certification | Marks ledger, result cards, certificates | Core |
| Financial | M7 Enrollment, Tuition and Installments | Fee agreement, dues notebook | Core |
| Financial | M8 Cashier, Receipts and Treasury Control | Receipt book, daily cash book | Core |
| Financial | M9 Accounting, Procurement and Assets | Ledgers, expense vouchers, asset register | Core |
| People | M10 Teachers, Contracts and Settlement | Teacher settlement sheet | Core |
| People | M11 HR and Staff Operations | Staff attendance, payroll sheet, leave register | Core |
| Engagement | M12 Guardian and Student Engagement | Call log, notices, WhatsApp groups | Core |
| Growth | M13 Private Lessons and Home Tutoring | Private lesson request sheet | Extended |
| Growth | M14 Ancillary Revenue and Retail | Hall diary, cafeteria contract, booklist | Extended |
| Operations | M15 Inventory, Assets and Maintenance | Asset register, maintenance requests | Extended |
| Platform | M16 Reporting, Compliance, Security and AI | Inspection binder, management reports | Core |

### M1. CRM, Admissions and Permanent Identity

**Purpose:** Turn every inquiry into a tracked opportunity and every accepted student into a complete digital file with a permanent identity.

**Paper replaced:** registration form, master student ledger, student file, paper ID card, trial-class sheet, waiting list.

**Core workflows:**

1. **Inquiry capture.** Walk-in, phone, WhatsApp, Instagram/Facebook, referral, or existing-family referral. Capture name, guardian phone, grade, subjects, preferred teacher, area, source, and desired start date. Detect likely duplicates by phone, national ID, and name plus birth date.
2. **Trial and follow-up.** Schedule a trial session or placement test. Assign a follow-up owner and due date. Record outcome. Convert to application or mark lost with a reason.
3. **Application.** Guided form for student and guardian, previous school, national ID or birth certificate, photos, medical or emergency notes, and consent. A document checklist shows what is missing and blocks activation until the required items are present.
4. **Placement and offer.** For language institutes, record the placement level. For tutoring, record the grade, school, and subject gaps. Issue an offer with program, section, fee, discount, deposit, and expiry date.
5. **Seat reservation.** A deposit or a time-limited reservation holds the seat. Expired reservations release capacity automatically.
6. **Registration.** Allocate the permanent student number, create the family record, link siblings, assign the section, generate the enrollment agreement and installment plan, issue the digital ID card with QR, invite the guardian to the portal, and send a welcome message.
7. **Lifecycle.** Active, suspended, withdrawn, transferred, graduated, alumni. Each transition has a workflow, a reason, an approval, and a clearance step.

**Rules that make paper unnecessary:**

- National ID is unique when present.
- A student cannot hold two active enrollments in the same course.
- Capacity is enforced by section, not only by course.
- A waitlist is a first-class state, not a note on a paper.
- Required documents must be complete before the student is marked active.
- The permanent number is never reused or edited.
- Siblings are linked to one family, enabling family statements and sibling discounts.
- Every status change is audited with a reason and an approver.
- Duplicate candidates can be merged without losing history.

**Key screens:** inquiry board, application wizard, document checklist, duplicate review, offer and deposit, registration completion, ID card print, family 360.

**KPIs:** inquiry-to-registration conversion, time to register, missing-document rate, duplicate rate, trial-to-paid conversion, waitlist conversion, source effectiveness.

### M2. Student and Family 360

**Purpose:** Replace the physical student file with a single timeline that shows identity, family, documents, enrollments, attendance, results, money, communication, and incidents.

**Paper replaced:** student file, correspondence book, transfer and withdrawal forms, clearance sheet.

**Core workflows:**

- Upload or scan documents; track issue and expiry dates; alert before expiry.
- View a chronological timeline of every event: registration, enrollment, payment, absence, exam result, message, meeting, complaint, schedule change.
- Print a student ID card with QR verification.
- Manage guardians and authorized pickup persons.
- Record notes with visibility rules: management-only, teacher-visible, guardian-visible.
- Withdrawal: check outstanding balance, return books, collect ID card, issue clearance, preserve the record.
- Transfer: produce a transfer certificate and a transcript of attendance and results.

**Rules:** one student, one active identity; guardians can be shared across siblings; documents are versioned, not overwritten; every access to sensitive documents is audited.

**KPIs:** file completeness, document expiry compliance, withdrawal clearance time, family consolidation rate.

### M3. Academic Structure and Calendar

**Purpose:** Give the institute a stable academic backbone so that courses, fees, schedules, and reports do not drift between seasons.

**Paper replaced:** year plan, curriculum sheets, course lists.

**Core concepts:**

- **Academic year** with start and end dates, terms, and a status: planning, active, closed.
- **Term or cycle** for report cards, fees, and scheduling.
- **Stage, grade, and branch of study** already exist and should be extended with program type: tutoring, language, exam preparation, skills.
- **Subject and course template** reusable across years.
- **Program offering** for a specific year and term, with fee, capacity, and required subjects.
- **Calendar** with holidays, Ramadan hours, exam periods, and blackout dates.

**Rules:** a course belongs to one academic year and term; a new year copies templates but not transactions; closing a year locks its financial and academic records; fees and teacher-share rules are versioned by effective date.

**Key screens:** year and term setup, calendar editor, program catalog, course template copy, year rollover.

### M4. Halls, Resources and Timetable

**Purpose:** Replace the whiteboard timetable and hall diary with a conflict-free, published, versioned schedule that produces real sessions.

**Paper replaced:** weekly timetable board, hall booking diary, substitution calls, exam seating plan.

**Core concepts:**

- **Resource:** hall, lab, equipment, or shared space, with capacity, type, features, and status.
- **Availability window:** when a resource can be used, including Ramadan and summer hours.
- **Timetable pattern:** the recurring weekly intent.
- **Session instance:** the dated, real occurrence of a class, with a teacher, hall, start, end, status, and substitute.
- **Session change:** cancellation, reschedule, room change, teacher change, or make-up.
- **External booking:** a rental that competes for the same resource.
- **Maintenance block:** a period when a resource is unavailable.

**Core workflows:**

1. Build the term calendar and operating hours.
2. Create sections and assign teachers, subjects, and expected enrollment.
3. Place weekly slots on a drag-and-drop grid with three views: by hall, by teacher, by section.
4. Run conflict detection across hall, teacher, section, and student cohort. Warn on capacity, teacher maximum hours, travel time between branches, and gender or shift constraints.
5. Publish a schedule version. Teachers and guardians see only the published version.
6. Generate session instances for the term, excluding holidays.
7. Handle daily exceptions: cancel, reschedule, substitute, or merge with another session. Notify everyone affected.
8. Schedule a make-up session linked to the original cancelled or missed session.
9. Check external rental requests against internal sessions, maintenance, and other bookings before accepting.
10. Print a daily operations sheet and a weekly board.

**Rules that prevent paper:**

- No double-booking of hall, teacher, or section.
- A published schedule is immutable; changes create a new version.
- Every cancellation has a reason and an owner.
- A substitute must be qualified for the subject and available in the slot.
- A make-up session must reference the session it replaces.
- An external booking cannot override an internal class without an approved exception.
- Session instances are the only place attendance can be recorded.

**Key screens:** weekly grid, conflict inspector, daily operations board, substitution board, exam seating planner, utilization heat map.

**KPIs:** hall occupancy, teacher utilization, clash count, substitution coverage, cancelled-session rate, make-up completion rate, revenue per hall-hour.

### M5. Attendance and Session Operations

**Purpose:** Make attendance a by-product of the session, captured in seconds, with automatic guardian follow-up and reliable evidence.

**Paper replaced:** attendance sheet, monthly absence register, absence call log, excusal notes.

**Core concepts:**

- **Session roster:** the students expected in a session instance.
- **Attendance record:** present, absent, late, excused, with minutes late and a note.
- **Attendance policy:** thresholds, excused rules, and consequences.
- **Guardian acknowledgement:** proof that the absence message was delivered and seen.
- **Teacher check-in:** separate record for teacher arrival, late, absence, and substitute coverage.
- **Make-up attendance:** credit linked to a make-up session.

**Core workflows:**

1. The teacher opens "My sessions today" on a phone or the front-desk tablet.
2. The roster appears with student photos, names, and permanent numbers.
3. The teacher marks present, absent, late, or excused; bulk-mark the remainder as present.
4. Optional QR scan of the student ID card or a kiosk PIN at the hall door.
5. Save. If the device is offline, the record is queued and synchronized when connectivity returns.
6. Absence triggers a guardian message within a configurable window, with delivery status.
7. A second or third absence triggers an escalation task for the supervisor.
8. When the absence threshold is reached, the student is flagged as at risk; the system warns the guardian and blocks exam eligibility if the policy requires it.
9. A change request after the session closes requires supervisor approval and is audited.
10. A make-up session can credit the missed session without erasing the original absence.

**Rules:**

- One attendance record per student per session.
- No attendance for a future session.
- No silent edits after the session closes.
- Late minutes are recorded, not inferred.
- Excused absence requires a reason and optionally a document.
- Absence messages are logged even if delivery fails.
- Teacher attendance is separate from student attendance.
- Attendance is never deleted; corrections are versioned.

**Key screens:** teacher today, take attendance, kiosk mode, supervisor attendance board, absence follow-up queue, monthly register, eligibility list.

**KPIs:** attendance capture rate within 24 hours, absence rate, late rate, message delivery rate, at-risk students, make-up completion, teacher punctuality.

### M6. Exams, Gradebook and Certification

**Purpose:** Turn scattered marks into a controlled assessment system that produces report cards, rankings, certificates, and academic insight.

**Paper replaced:** marks ledger, exam mark sheet, result card, certificate, exam seating plan.

**Core concepts:**

- **Assessment plan:** the set of quizzes, monthly exams, midterms, mocks, and finals for a course and term, with weights.
- **Assessment:** a specific exam with date, max mark, pass mark, scope, and responsible teacher.
- **Score:** a student's mark, with absence or exemption status.
- **Moderation:** review and approval before publication.
- **Grade period:** term or cycle used for aggregation.
- **Report card:** the published, versioned result document.
- **Rank:** optional, privacy-controlled ranking.
- **Certificate:** completion or achievement document with QR verification.
- **Exam session:** hall, date, time, invigilator, and seating.

**Core workflows:**

1. Define the assessment plan per course and term. Weights must sum to the required total.
2. Schedule assessments and check hall and cohort conflicts.
3. Generate a seating plan and an attendance sheet for each exam.
4. Enter marks in a grid, by Excel import, or by scanning paper mark sheets. Validate range and duplicates.
5. Moderate: a supervisor reviews distributions, outliers, and missing marks.
6. Publish. The system computes weighted averages, pass/fail, and optional rank.
7. Generate report cards as PDF with a verification code. Notify guardians.
8. Issue certificates for completed programs.
9. Analyze: pass rate, average, distribution, subject weakness, teacher comparison, improvement over time.

**Rules:**

- One score per student per assessment.
- A mark cannot exceed the maximum or fall below zero.
- Only the assigned teacher or an academic supervisor can enter marks.
- Published results are locked; changes require a moderation request.
- Weights are snapshotted at publication.
- Absent students are distinguished from zero-mark students.
- Report cards are versioned; reissues are marked as reissues.
- Public ranking is opt-in and can be disabled for minors.

**Key screens:** assessment plan builder, mark entry grid, moderation queue, report card designer, exam operations, results analytics, certificate register.

**KPIs:** mark entry turnaround, report card turnaround, moderation exceptions, pass rate, average improvement, parent result reach rate.

### M7. Enrollment, Tuition and Installments

**Purpose:** Replace the fee agreement and dues notebook with a documented financial contract that generates invoices, receipts, reminders, and statements automatically.

**Paper replaced:** fee agreement, dues notebook, discount slip, installment reminders, student statement.

**Core concepts:**

- **Fee catalog:** price per program, subject, grade, and delivery mode.
- **Enrollment agreement:** a snapshot of the fee, discount, net amount, installment plan, and teacher-share basis at the moment of enrollment.
- **Discount or scholarship policy:** sibling, early registration, full payment, staff child, orphan, martyr family, social case, referral.
- **Approval request:** required when a discount or refund exceeds a threshold.
- **Installment plan:** template or custom schedule with amounts and due dates.
- **Invoice / due:** a payable amount with a due date and status.
- **Payment:** cash, bank transfer, card, or wallet, with receipt and allocation.
- **Payment allocation:** which invoice or invoices a payment settles.
- **Credit balance:** an overpayment or prepayment available for future invoices.
- **Refund or credit note:** a formal reversal with approval.
- **Promise to pay:** a guardian commitment with a follow-up date.
- **Late-fee rule:** optional, configurable, and disclosed in the agreement.

**Core workflows:**

1. **Quote.** Select program, subjects, sections, and add-ons. The system calculates the list price, eligible discounts, deposit, and installment options.
2. **Approval.** If the discount exceeds the staff threshold, route to the manager with a reason.
3. **Agreement.** Record total fee, discount, net fee, deposit, installment schedule, due dates, refund policy, and teacher-share basis. Print or e-sign.
4. **Invoicing.** Generate installments automatically, aligned to the institute's collection calendar.
5. **Reminders.** Send scheduled notices before the due date, on the due date, and after it becomes overdue. Record delivery.
6. **Collection.** Accept full or partial payment; allocate to the oldest open invoice by default; allow manual allocation.
7. **Overpayment.** Hold as a credit balance and apply to the next invoice.
8. **Withdrawal.** Apply the refund policy, calculate unused sessions or months, issue a credit note or refund, reverse the teacher accrual, and close the agreement.
9. **Add or drop a subject.** Recalculate the plan and schedule; pro-rate according to policy.
10. **Statement.** Produce a family statement showing invoices, payments, credits, refunds, and balance.

**Rules that make paper unnecessary:**

- No payment without an invoice, or an explicit prepayment document.
- A receipt is immutable; a correction is a void plus a reissue.
- Allocation cannot exceed the invoice balance unless it becomes a credit.
- Discounts require approval above the threshold.
- Refunds follow a configured policy and require approval.
- The teacher-share basis is frozen in the agreement; later policy changes do not rewrite history.
- No deletion of financial records; only reversal.
- Closed periods cannot be edited.

**Key screens:** fee catalog, quote builder, agreement, installment board, collection screen, refund wizard, family statement, aging report.

**KPIs:** collection rate, days sales outstanding, overdue percentage, discount percentage, refund percentage, installment adherence, average time to collect.

### M8. Cashier, Receipts and Treasury Control

**Purpose:** Make cash provable. This is the module that eliminates the receipt book and the daily cash notebook.

**Paper replaced:** receipt book, daily cash book, cash handover note, void slip.

**Core concepts:**

- **Cashier shift:** a named cashier, an opening float, a start time, and a closing time.
- **Cash drawer:** the physical drawer or safe assigned to the shift.
- **Denomination count:** 25,000, 10,000, 5,000, 1,000, 500, and 250 IQD counts.
- **Daily close:** expected cash versus counted cash, with variance.
- **Receipt void:** a formal reversal with reason and approval.
- **Bank deposit:** cash transferred to the bank, with reference.
- **Reconciliation:** receipts, drawer, deposits, and ledger must agree.

**Core workflows:**

1. Open a shift with a counted opening float.
2. Collect payments. Every receipt is numbered, QR-stamped, printable, and available to send via WhatsApp.
3. Reprint a receipt only as a marked copy; never issue a second original.
4. Void a receipt with a reason and approval; the system reverses the allocation, the ledger, and the teacher accrual.
5. Close the shift with a blind denomination count. The system shows expected versus counted and requires approval for any variance.
6. Record the bank deposit and reconcile it against the bank statement.
7. Produce the daily cash report, cashier performance, void report, and variance report.
8. Hand over the drawer to the next cashier with a signed digital handover.

**Rules:**

- One open shift per cashier.
- Receipt numbers are sequential and unique per branch and year.
- A receipt cannot be deleted.
- A void requires approval and a reason.
- Backdated receipts after a closed shift are not allowed without a formal adjustment.
- Cashier and accountant duties are separated.
- Blind count means the cashier counts before seeing the expected figure.
- Offline receipt blocks are reserved per device to avoid duplicate numbers; they are reconciled when connectivity returns.

**Key screens:** cashier mode, shift open and close, denomination count, variance approval, receipt reprint and void, daily cash report, deposit reconciliation.

**KPIs:** cash variance, unclosed shifts, void rate, same-day deposit rate, time to close, receipt-to-ledger match.

### M9. Accounting, Procurement and Assets

**Purpose:** Turn the existing bookkeeping base into a controlled financial system that can produce statements, manage payables and procurement, and account for assets.

**Paper replaced:** expense vouchers, purchase requests, asset register, bank reconciliation sheets, financial statements.

**Existing strength:** chart of accounts, journals, funds, expenses, income, payroll, teacher payables.

**Additional concepts:**

- **Accounts receivable** from student invoices.
- **Accounts payable** from vendor bills, teacher settlements, and rent contracts.
- **Vendor** and **purchase order** with approval.
- **Bill** and **payment** for vendors.
- **Fixed asset** with assignment, condition, warranty, and depreciation.
- **Budget** by branch, department, or course.
- **Period lock** for closed months.
- **Bank reconciliation**.
- **Financial statements:** trial balance, income statement, balance sheet, cash flow.

**Core workflows:**

1. Expense request: department submits, manager approves, finance pays, ledger posts, receipt image is attached.
2. Procurement: request, compare quotes, approve, receive, bill, pay, update stock or asset.
3. Asset: capitalize, label with QR, assign to a room or person, maintain, depreciate, dispose with approval.
4. Month-end: reconcile banks, accrue unpaid teacher shares if using accrual accounting, review AR and AP aging, lock the period, produce statements.
5. Branch reporting: separate branch P&L plus consolidated ownership view.

**Rules:**

- Every journal is balanced.
- Every posting has a source document.
- Closed periods are locked.
- No hard deletion of financial records.
- Segregation between requester, approver, and payer.
- Asset depreciation follows a defined policy and posts automatically.
- Bank reconciliation is a controlled workflow, not a spreadsheet.

**Key screens:** journal, expense and vendor bills, purchase orders, asset register, bank reconciliation, trial balance, income statement, balance sheet, cash flow, budget vs actual.

**KPIs:** cash variance, expense approval time, AP aging, asset utilization, budget variance, close time, statement accuracy.

### M10. Teachers, Contracts and Settlement

**Purpose:** Replace the informal percentage agreement and the monthly argument with a documented contract and a transparent settlement statement.

**Paper replaced:** teacher settlement sheet, advances register, teacher attendance book, private agreement notes.

**Core concepts:**

- **Teacher profile:** qualifications, subjects, grades, areas, availability, documents, rating.
- **Teacher contract:** effective dates, type, and rules.
- **Supported compensation models:**
  - percentage of tuition collected;
  - percentage of tuition billed;
  - percentage of net after discount;
  - fixed monthly salary;
  - per session or per hour;
  - hybrid base plus percentage;
  - minimum guarantee with top-up;
  - room rent paid by the teacher to the institute;
  - revenue share with a cap or floor.
- **Workload:** assigned sections, weekly hours, maximum hours.
- **Teacher attendance:** check-in, late, absent, substituted.
- **Earning accrual:** created when the contract rule is triggered, with a snapshot of the basis.
- **Advance:** requested, approved, and recovered from future settlements.
- **Deduction or adjustment:** absence, late, material, penalty, or correction, with reason.
- **Settlement statement:** gross earnings, deductions, advances, net payable, period, and approval.
- **Payout:** cash or bank, with reference and ledger posting.

**Core workflows:**

1. Create the teacher and contract with effective dates and compensation rules.
2. Assign courses and sections; the system checks workload and availability.
3. Accrue earnings continuously:
   - percentage contracts accrue on each collected payment, using the basis frozen in the enrollment agreement;
   - per-session contracts accrue when the session is completed;
   - fixed contracts accrue monthly.
4. Teacher sees a live statement in the portal, with the source of every amount.
5. Request an advance; manager approves; recovery schedule is set.
6. At the settlement date, generate the statement: gross, advances, deductions, adjustments, net.
7. Teacher acknowledges; manager approves; finance pays; ledger posts; notification is sent.
8. A refund or void after settlement creates a negative adjustment in the next settlement, never a silent edit.
9. A substitute teacher receives the configured share of the session; the original teacher's accrual is adjusted.

**Rules:**

- No payout without an approved statement.
- Contract rules are snapshotted at accrual.
- A teacher cannot see another teacher's earnings.
- Advances cannot exceed the configured cap.
- Deductions require a reason and evidence.
- Settlement corrections are new documents.
- Teacher attendance affects pay only according to the contract.

**Key screens:** contract builder, workload board, live earnings, settlement statement, advances, deductions, payout, teacher scorecard.

**KPIs:** teacher cost ratio, unpaid balance, settlement cycle time, dispute count, advance recovery rate, retention by teacher, attendance and punctuality.

### M11. HR and Staff Operations

**Purpose:** Extend the existing employee and payroll base into a complete staff file.

**Paper replaced:** staff attendance book, leave register, payroll sheet, advance register.

**Additions:**

- Leave types and balances: annual, sick, unpaid, emergency.
- Leave request and approval with coverage planning.
- Overtime and bonus entry.
- Staff contracts and document expiry.
- Staff attendance by QR, PIN, or supervisor entry.
- Advances and deductions integrated with payroll.
- End-of-service calculation.
- Staff tasks and reminders.
- Unified view of teachers who are also employees.

**Rules:** payroll cannot be paid twice for the same month; approved leave affects deductions; payroll approval and payment are separated; payslips are immutable.

### M12. Guardian and Student Engagement

**Purpose:** Replace phone calls, WhatsApp groups, and paper notices with a logged, measurable communication system.

**Paper replaced:** call log, guardian notices, correspondence book, meeting invitations.

**Core concepts:**

- **Guardian contact:** phone, WhatsApp number, preferred language, opt-in.
- **Channel:** WhatsApp, SMS, in-app, phone call, printed letter.
- **Template:** Arabic and English, with variables such as student name, amount, due date, and class.
- **Campaign:** a broadcast to a grade, section, branch, or custom list.
- **Delivery log:** sent, delivered, read, failed, replied.
- **Reply inbox:** guardian replies assigned to staff with an SLA.
- **Meeting:** slots, booking, reminders, attendance.
- **Consent:** communication preferences, photo permission, data consent.
- **Escalation:** unpaid fees, repeated absence, failing marks, behavior incidents.

**Core workflows:**

- Automatic messages: registration welcome, receipt, installment due, overdue reminder, absence, exam result, report card, schedule change, teacher change, meeting invitation.
- Manual campaigns: holiday notice, exam timetable, fee deadline, summer registration.
- Two-way replies: the guardian replies on WhatsApp; the message appears in the shared inbox; a staff member responds within the SLA.
- Call logging: a staff member records the call outcome and sets a follow-up task.
- Statements: secure links to the student account, attendance, and results.
- Meetings: parents book slots; reminders are sent; attendance is recorded.

**Rules:** opt-in and quiet hours; no duplicate sends; delivery status is recorded; message templates are approved; sensitive financial details are not sent to the wrong guardian; all communication is retained according to policy.

**KPIs:** delivery rate, reply rate, absence response time, collection reminder effect, meeting attendance, contact-data completeness.

### M13. Private Lessons and Home Tutoring

**Purpose:** Extend the existing private-lesson marketplace into a safe, recurring, financially integrated service.

**Additions:** recurring lesson series, teacher acceptance, travel fee, home-visit check-in and check-out, cancellation policy, package pricing, safety notes, guardian tracking, integrated teacher payout, ratings moderation.

**Paper replaced:** private lesson request sheet, home visit log, teacher commission notes.

### M14. Ancillary Revenue and Retail

**Purpose:** Capture the side revenues that currently live in separate notebooks.

**Includes:**

- Hall rental to external teachers and events.
- Cafeteria annual contract and monthly payments.
- Teacher room rent, especially for popular sixth-preparatory teachers.
- Lecture notes and book sales (ملازم), with stock, price, and receipts.
- Stationery or uniform sales, if applicable.
- Transport subscriptions, if applicable.

**Paper replaced:** hall diary, cafeteria contract, booklist, separate receipts.

**Rules:** every sale produces a receipt; stock movements are recorded; rentals check the timetable; contracts have renewal alerts.

### M15. Inventory, Assets and Maintenance

**Purpose:** Replace the asset register, maintenance requests, and purchase requests.

**Core concepts:** asset, asset category, location, custodian, condition, warranty, maintenance ticket, preventive schedule, inventory item, stock movement, purchase request, vendor.

**Workflows:** label assets with QR; assign to room or person; report a fault; approve repair; record cost; close ticket; depreciate; dispose with approval; count stock; reorder.

### M16. Reporting, Compliance, Security and AI

**Purpose:** Give ownership a single truth and give the institute a defensible compliance file.

**Reporting catalogue:**

| Audience | Reports |
|---|---|
| Owner / manager | Daily cash, revenue by branch, P&L, balance sheet, cash flow, course profitability, teacher cost, AR aging, enrollment funnel, retention |
| Registrar | New registrations, missing documents, capacity, waitlist, withdrawals, transfers |
| Accountant | Receipts, voids, daily close, expenses, vendor bills, teacher settlements, payroll, bank reconciliation |
| Academic supervisor | Timetable utilization, attendance, at-risk students, exam results, teacher workload, report cards |
| Teacher | My schedule, my attendance, my earnings, my settlement, my students' results |
| Guardian | Child's attendance, results, report cards, statement, receipts, messages |
| Compliance | License and registration file, staff qualifications, safety register, inspection pack |

**Compliance concepts:** license and ministry code, staff document expiry, student document completeness, safety checks, incident register, data retention, consent register, audit export.

**Security:** role-based access plus branch scope, MFA for leadership, session control, file access control, PII encryption, immutable audit, backup and restore drills.

**AI:** keep the assistant read-only for operational data. Extend it to natural-language reporting, anomaly detection (cash variance, attendance drop, fee leakage), and guided navigation. Any action that changes data must be a human-approved workflow, not an autonomous AI operation.

---

## 5. How the Modules Interconnect

The interconnection is the actual product. A set of disconnected screens does not eliminate paper; a chain of documents and events does.

### 5.1 The dependency chain

```
M1 Admissions
  └─ creates Student, Family, Documents, ID Card
       └─ M2 Student/Family 360
            └─ M7 Enrollment Agreement + Installment Plan
                 ├─ M8 Cashier → Receipt → M9 Accounting
                 ├─ M10 Teacher Accrual → Settlement → M9 Accounting
                 └─ M4 Section Assignment → Timetable → Session Instances
                      └─ M5 Attendance → M12 Guardian Messages
                           └─ M6 Assessments → Report Cards → M12 Guardian Messages

M10 Teacher Contracts
  └─ M4 Workload and Availability
  └─ M5 Teacher Attendance
  └─ M9 Payables and Payouts

M14 Rentals / M15 Assets
  └─ M4 Resource Calendar
  └─ M9 Accounting
```

### 5.2 Event-to-effect map

| Business event | Automatic effects across modules |
|---|---|
| Lead created | CRM task, follow-up reminder, source analytics |
| Application submitted | Document checklist, duplicate check, registrar notification |
| Offer accepted | Seat reservation, deposit invoice, expiry timer |
| Student registered | Permanent ID, family link, portal invitation, ID card, welcome message |
| Enrollment activated | Section assignment, installment generation, timetable visibility, teacher accrual plan, guardian message |
| Timetable published | Session instances, teacher and guardian notifications, attendance rosters |
| Session cancelled | Make-up task, substitution option, guardian message, teacher-pay adjustment |
| Attendance saved | Absence messages, at-risk flag, monthly register, teacher attendance record |
| Payment received | Receipt, invoice allocation, cash drawer, journal, teacher accrual, guardian message, dashboard |
| Receipt voided | Reversal of allocation, journal, and accrual; approval; guardian message; audit |
| Assessment published | Report card, guardian message, rank, analytics |
| Teacher settlement approved | Payable, payout, journal, teacher notification, cost report |
| Refund approved | Credit note, reversal of revenue and teacher accrual, guardian message, audit |
| Student withdrawn | Clearance, refund calculation, schedule removal, ID card return, retention analysis |
| Term closed | Period lock, report card archive, year rollover, statements, retention snapshot |

### 5.3 Ownership rules that prevent double entry

- **Admissions owns identity.** It does not calculate balances.
- **Billing owns the agreement and the invoice.** It does not post journals.
- **Cashier owns the receipt and the drawer.** It does not change the invoice status directly; it posts an allocation.
- **Accounting owns the ledger.** It receives documents, not raw numbers.
- **Timetable owns the session.** Attendance cannot exist without a session.
- **Exams own the score.** Academic records own the report card.
- **Communication owns delivery.** No module sends a message directly; it raises an event.
- **Teachers own their profile; settlement owns their pay.**
- **Reporting reads; it never writes.**

This separation is what removes the paper handoffs: each handoff that used to be a sheet passed between people becomes a typed event with a status, an owner, and an audit trail.

### 5.4 Worked example: a single student, end to end

1. A guardian calls about the sixth scientific mathematics course. The receptionist creates a lead with the guardian's WhatsApp number and the student's school.
2. The student attends a trial session. The teacher marks the trial attendance.
3. The guardian accepts the offer. A deposit invoice is issued and collected.
4. The registrar completes the application, scans the national ID and photo, and assigns the section.
5. The system allocates `KRI-2026-0147`, creates the family record, issues the ID card with QR, and invites the guardian to the portal.
6. The enrollment agreement records the fee of 300,000 IQD, a 20,000 IQD sibling discount, a net of 280,000 IQD, and three installments of 100,000, 90,000, and 90,000 IQD.
7. The timetable adds the student to the Saturday/Monday/Wednesday 16:00 section in Hall 101.
8. The first installment is collected. A receipt is printed and sent to WhatsApp. The teacher's 55% share is accrued on the collected amount. The ledger posts cash, revenue, teacher expense, and teacher payable. The drawer balance increases.
9. On Monday, the teacher takes attendance on a phone. The student is absent. The guardian receives a WhatsApp message within five minutes. The supervisor sees the absence in the follow-up queue.
10. The monthly exam is entered. The report card is generated, and the guardian receives a link. The student's average and attendance are visible in the parent portal.
11. At month end, the teacher's settlement statement shows the accrued share, a 25,000 IQD advance, and the net payable. The manager approves; finance pays; the ledger posts the payable settlement.
12. The cashier closes the shift. The denomination count matches the expected cash. The daily report reconciles receipts, drawer, and ledger.

No paper is required at any step. Printouts exist only as projections.

---

## 6. Paper Elimination Matrix

This matrix is the direct answer to "replace every single paper record, receipt, attendance sheet, and manual schedule."

| Paper artifact | Digital replacement | Module | Print fallback | Phase |
|---|---|---|---|---|
| Registration form | Guided application + document checklist | M1 | A4 application summary | 1 |
| Master student ledger | Permanent ID registry with search and export | M1 | A4 registry extract | 1 |
| Student file | Digital document vault + timeline | M2 | A4 file summary | 1 |
| Paper student card | QR ID card | M1/M2 | PVC or laminated card | 1 |
| Receipt book | Numbered receipt + QR verification | M8 | A5 or 80 mm thermal | 1 |
| Dues notebook | Installment plan + invoice ledger + aging | M7 | A4 statement | 1 |
| Discount slip | Discount request + approval + audit | M7 | A5 approval note | 1 |
| Timetable board | Published timetable versions + portal | M4 | A3 weekly board | 2 |
| Hall booking diary | Resource calendar + external bookings | M4/M14 | A4 daily sheet | 2 |
| Attendance sheet | Session attendance on phone/tablet | M5 | A4 backup sheet | 2 |
| Monthly absence register | Attendance analytics + at-risk list | M5 | A4 register | 2 |
| Absence call log | Communication log + delivery status | M12 | A4 log | 2 |
| Marks ledger | Gradebook + assessment plan | M6 | A4 mark sheet | 3 |
| Exam mark sheet | Mark entry grid + import | M6 | A4 sheet | 3 |
| Result card | Report card PDF + verification code | M6 | A4 report card | 3 |
| Certificate | Certificate register + QR | M6 | A4/A5 certificate | 3 |
| Exam seating plan | Exam session + seat allocation | M6 | A4 seating chart | 3 |
| Teacher settlement sheet | Contract + accrual + statement + approval | M10 | A4 statement | 4 |
| Payroll sheet | Payroll run + approval + payslip | M11 | A5 payslip | 4 |
| Staff attendance book | QR/PIN attendance + supervisor entry | M11 | A4 register | 4 |
| Expense voucher | Expense request + approval + attachment | M9 | A5 voucher | 4 |
| Daily cash book | Cashier shift + daily close + variance | M8 | A4 daily report | 1 |
| Asset register | Asset register + QR labels + maintenance | M15 | A4 register | 4 |
| Rental contract | Contract record + renewal alerts + payments | M14 | A4 contract | 4 |
| Book and lecture-note list | Product catalog + stock + POS receipt | M14 | A5 receipt | 5 |
| Guardian correspondence book | Communication log + templates + delivery | M12 | A4 log | 2 |
| Meeting minutes | Meeting record + decisions + tasks | M16 | A4 minutes | 5 |
| Maintenance request | Maintenance ticket + approval + history | M15 | A5 ticket | 4 |
| Purchase request | Purchase request + approval + vendor bill | M9 | A4 request | 4 |
| Advances register | Advance request + recovery schedule | M10/M11 | A5 statement | 4 |
| Leave register | Leave request + balance + coverage | M11 | A4 register | 4 |
| Private lesson request | Request + matching + schedule + payout | M13 | A5 confirmation | 5 |
| Withdrawal or transfer form | Lifecycle workflow + clearance + certificate | M2 | A4 clearance | 3 |
| Trial class sheet | Lead + trial session + follow-up | M1 | A5 trial note | 5 |
| Compliance binder | Compliance dashboard + document expiry | M16 | A4 inspection pack | 5 |

---

## 7. Conceptual Data Architecture

This is a domain model, not a database design. It defines the concepts the system must own and the invariants it must protect.

### 7.1 Domain boundaries

| Domain | Aggregate roots | Owns |
|---|---|---|
| Identity and access | User, Role, Permission | Authentication, roles, branch scope |
| Institution | Institute, Branch, AcademicYear, Term | Configuration, calendar, numbering |
| Admissions | Lead, Application, Offer | Pipeline, documents, decisions |
| People | Student, Family, Guardian, Teacher, Employee | Identity, relationships, contracts |
| Academic catalog | Program, Course, Section | Curriculum, capacity, assignment |
| Scheduling | Resource, Timetable, SessionInstance | Time, place, conflict, substitution |
| Attendance | AttendanceSession, AttendanceRecord | Presence, absence, thresholds |
| Assessment | AssessmentPlan, Assessment, Score, ReportCard | Marks, weights, certification |
| Billing | EnrollmentAgreement, Invoice, PaymentAllocation, Refund | Fees, installments, balances |
| Treasury | CashierShift, Receipt, CashCount, Deposit | Cash control |
| Accounting | JournalEntry, Fund, VendorBill, Asset, Period | Ledger, statements, control |
| Payroll | TeacherContract, EarningAccrual, Settlement, Payout | Teacher and staff pay |
| Communication | Contact, Template, Message, Delivery, Meeting | Reach, proof, follow-up |
| Operations | Asset, MaintenanceTicket, InventoryItem, PurchaseRequest | Facilities and stock |
| Platform | Document, Approval, AuditEvent, Notification | Cross-cutting control |

### 7.2 Key invariants

1. A student has one permanent number for life, even across years and branches.
2. A student cannot have two active enrollments in the same course and term.
3. Every invoice belongs to exactly one enrollment agreement.
4. Every payment is allocated to one or more invoices or to a credit balance; the sum of allocations equals the payment amount.
5. A receipt is immutable after issue. A void is a separate document.
6. Every journal entry is balanced; every line references an account and, where applicable, a source document.
7. A teacher earning has a contract rule snapshot and a source event.
8. A settlement cannot be paid without approval.
9. Attendance cannot exist without a session instance.
10. A report card is a published snapshot; later mark changes create a new version.
11. A closed period cannot be edited.
12. Every document has a number, a status, an owner, and an audit trail.

### 7.3 Historical snapshots

Paper records are trusted because they are hard to change. The ERP must earn the same trust through deliberate snapshots:

- The fee and discount are frozen in the enrollment agreement.
- The teacher-share basis is frozen at accrual.
- The assessment weights are frozen at report card publication.
- The timetable version is frozen at publication.
- The payroll and settlement amounts are frozen at approval.
- The receipt and invoice numbers are never reused.

### 7.4 Branches and tenancy

The current application has a `Branch` concept but no operational branch scoping and a single institute settings row. The recommended model for the first release is:

- **Single institute, multiple branches.**
- Every transactional record carries a branch.
- Staff are assigned to one or more branches.
- Owners and general managers see consolidated results; branch managers see their branch.
- Numbering is per branch, for example `KRI1-RC-2026-000123`.
- The database remains single-tenant, but the design should keep a tenant boundary in mind in case the product is later offered to other institutes.

A multi-tenant SaaS model is a different product decision and should not be assumed. It adds isolation, onboarding, billing, and support complexity that would slow down the first release.

### 7.5 Reporting model

Operational reporting can read directly from the transactional tables at the current scale. As data grows:

- Add read-optimized summary tables for daily cash, receivables, attendance, and course profitability.
- Snapshot monthly financials at close.
- Keep an immutable audit stream separate from operational tables.
- Avoid loading entire tables into memory for dashboards; aggregate in the database.

### 7.6 Data quality and migration

The biggest migration risk is not the software; it is the paper data. A realistic migration plan includes:

- A controlled student import template with duplicate detection.
- Guardian deduplication by phone.
- Opening balances per student, not just a total.
- Legacy receipt numbers mapped to new receipt numbers.
- Teacher contract mapping, including the basis of the percentage.
- Timetable import in a structured grid.
- A reconciliation report signed by the owner before cutover.
- A two-week parallel run, then a paper sunset date.

---

## 8. Technical Architecture Blueprint

No implementation is proposed here, only the architectural shape.

### 8.1 Keep the modular monolith

The current application is a single ASP.NET Core MVC project with clean separation between controllers, services, models, data, and views. That is the right foundation for an institute ERP.

- Keep one deployable application and one database.
- Organize code by business module, not by technical layer alone.
- Keep cross-module effects explicit through domain events or a transactional outbox.
- Avoid microservices at this scale; they add operational cost without solving a real problem.
- Avoid a separate frontend SPA initially; server-rendered pages plus progressive enhancement are faster to build, print better, and work on low-end devices.

### 8.2 Add a background job layer

Several ERP functions cannot depend on a user clicking a button:

- installment reminders;
- overdue escalation;
- recurring invoices;
- WhatsApp retry and delivery status;
- session generation;
- absence threshold checks;
- report card publication;
- backup scheduling;
- month-end snapshots;
- document expiry alerts;
- teacher settlement reminders.

A job scheduler with a visible job history is required.

### 8.3 Offline and resilience

- **Attendance** should work offline on a phone or tablet and synchronize later.
- **Cashier** should not silently work offline with uncoordinated receipt numbers. If offline collection is required, reserve receipt number blocks per device and reconcile on reconnect.
- **WhatsApp and SMS** should be queued and retried.
- **Printing** should degrade gracefully: if the thermal printer fails, the receipt can be printed on A4 or sent as a PDF.
- **Power** requires a UPS on the server and front-desk devices.

### 8.4 Integration layer

| Integration | Purpose | Priority |
|---|---|---|
| WhatsApp Business API or local provider | Guardian messages, receipts, reminders, replies | Critical |
| SMS aggregator | Fallback for guardians without WhatsApp | High |
| SMTP email | Staff and formal correspondence | Low |
| Payment providers (ZainCash, FastPay, Qi Card) | Online deposits and installments | Later |
| Thermal and A4 printing | Receipts, statements, report cards | Critical |
| QR and barcode | Student cards, receipts, assets, certificates | High |
| Local Ollama model | Private AI assistant | Existing |
| Bank statement import | Reconciliation | Later |

### 8.5 Security and control

- Role-based access plus branch scope.
- MFA for owner, general manager, and administrator accounts.
- Segregation of duties: cashier cannot approve voids; accountant cannot create receipts; teacher cannot edit published marks.
- Immutable audit of financial and academic changes.
- File access control; student photos and national IDs are sensitive.
- Encrypted backups, offsite copy, and quarterly restore drills.
- Period locking.
- Rate limiting on external APIs, already present for AI.
- No student data sent to external AI services; keep the local model.

### 8.6 Localization and accessibility

- Arabic is the default; English is available.
- Kurdish can be added as a language pack for guardian messages and key screens.
- Right-to-left layout must remain correct on all new screens.
- Use large touch targets for teachers and cashiers.
- Use keyboard shortcuts for high-volume front-desk work.
- Format money as whole IQD with Arabic or English grouping.
- Use Arabic names and collation correctly.

### 8.7 Schema evolution

The current `EnsureCreated` plus hand-written upgrade scripts approach will not scale to a 16-module ERP. The recommendation is:

1. Establish a baseline migration from the current database.
2. Move all future schema changes into versioned migrations.
3. Keep the existing idempotent upgrades only as a bridge for installed databases.
4. Add a migration dry-run and backup-before-upgrade step.
5. Test upgrades against a copy of production data, not an empty database.

### 8.8 Performance and hosting

- SQL Server Express has a 10 GB database limit and limited memory. At current scale it is adequate, but the ERP will add documents, audit volume, and message logs.
- Plan a path to SQL Server Standard, or an archival strategy for old years.
- Store large files outside the database, with metadata in the database.
- Index by branch, date, student, section, and status.
- Avoid loading full tables into memory for dashboards; move to database aggregation.
- Host on-premises with a UPS and a nightly backup, or in a regional cloud with a local cache if connectivity is reliable.

### 8.9 Testing strategy

The most valuable tests are not UI tests; they are invariant tests:

- student number allocation under concurrency;
- receipt numbering under concurrency;
- payment allocation never exceeds the invoice;
- void reverses the ledger and teacher accrual exactly;
- attendance uniqueness per student and session;
- settlement totals equal the sum of accruals minus advances and deductions;
- report card averages match the weights;
- period lock prevents backdated edits;
- branch scoping prevents cross-branch leakage.

---

## 9. Roles, Permissions and Approval Matrix

### 9.1 Recommended roles

| Role | Scope |
|---|---|
| Owner | All branches, all financials, configuration, approvals |
| General Manager | All branches, operations and financials, limited configuration |
| Branch Manager | One branch, all operations within it |
| Registrar | Admissions, students, documents, enrollment |
| Cashier | Collection, receipts, shift close; no approvals |
| Accountant | Ledger, expenses, payables, reconciliation; no receipt creation |
| Academic Supervisor | Timetable, attendance oversight, exams, report cards |
| HR | Staff records, attendance, leave, payroll preparation |
| Teacher | Own schedule, attendance, marks, earnings, statements |
| Assistant Teacher | Assigned sessions, attendance, marks if permitted |
| Parent / Guardian | Own children only |
| Student | Own record only |
| Auditor | Read-only across modules, no operations |

### 9.2 Approval matrix

| Action | Requester | Approver | Notes |
|---|---|---|---|
| Discount above threshold | Registrar | Branch Manager or Owner | Reason required |
| Refund | Registrar or Accountant | Owner | Policy-based calculation |
| Receipt void | Cashier | Accountant or Manager | Reason and audit |
| Cash variance | Cashier | Accountant or Manager | Blind count evidence |
| Expense | Department | Branch Manager | Amount-based escalation |
| Purchase order | Admin | Branch Manager or Owner | Quote comparison |
| Teacher settlement | Accountant | Manager or Owner | Teacher acknowledgement |
| Payroll | HR | Manager or Owner | Finance pays separately |
| Mark change after publication | Teacher | Academic Supervisor | Moderation record |
| Timetable change | Supervisor | Branch Manager | Guardian notification |
| Student withdrawal | Registrar | Branch Manager | Clearance required |
| Advance to teacher or staff | Employee | Manager | Recovery schedule |

---

## 10. Delivery Roadmap and Migration

Indicative only. The sequence is driven by dependency and by where paper hurts most.

### Phase 0 — Discovery and control design (2–3 weeks)

- Audit the current paper artifacts and owners.
- Agree on fee catalog, discount policy, refund policy, and installment defaults.
- Agree on teacher compensation models and settlement cycle.
- Agree on branch and academic-year structure.
- Choose WhatsApp provider and number.
- Choose receipt and ID card hardware.
- Map legacy data and define the migration template.
- Define the paper sunset policy.

**Exit criteria:** signed policy decisions, data map, and hardware list.

### Phase 1 — Foundation and money trust (6–9 weeks)

- Branch scoping, academic year and term, permissions.
- Document vault and ID card with QR.
- Fee catalog, enrollment agreement, installment plans.
- Receipt QR, void workflow, cashier shift, daily close.
- Student and family statements.
- WhatsApp foundation for receipts and reminders.
- Migration baseline and backup/restore drill.

**Paper removed:** registration form, student file, paper ID, receipt book, dues notebook, daily cash book, discount slips.

### Phase 2 — Time and presence (5–7 weeks)

- Session instances, timetable versions, conflict engine.
- Teacher, hall, and section views.
- Substitution and make-up workflows.
- Offline attendance on phone and tablet.
- Absence messages, thresholds, at-risk list.
- Teacher attendance.

**Paper removed:** timetable board, hall diary, attendance sheet, absence register, call log.

### Phase 3 — Academic results (4–6 weeks)

- Assessment plans, weights, terms.
- Mark entry, moderation, publication.
- Report cards, ranking controls, certificates.
- Exam scheduling and seating.
- Guardian result notifications.

**Paper removed:** marks ledger, exam sheets, result cards, certificates, seating plans.

### Phase 4 — Teacher and staff settlement (4–6 weeks)

- Teacher contracts and compensation rules.
- Accruals, advances, deductions, statements.
- Settlement approval and payout.
- Leave, overtime, staff contracts.
- Teacher scorecard.

**Paper removed:** teacher settlement sheets, advances register, payroll sheet, staff attendance book, leave register.

### Phase 5 — Reach and growth (4–6 weeks)

- CRM and lead pipeline.
- Parent portal provisioning at scale.
- Campaigns, reply inbox, meeting booking.
- Private lesson upgrades.
- Retail and ancillary revenue.

**Paper removed:** inquiry sheets, correspondence book, private lesson sheets, booklists.

### Phase 6 — Control and optimization (ongoing)

- Financial statements, bank reconciliation, procurement, assets, maintenance.
- Compliance dashboard and inspection pack.
- Analytics, retention, profitability.
- AI natural-language reporting and anomaly detection.
- Kurdish language pack.
- Online payment integration.

**Paper removed:** expense vouchers, purchase requests, asset register, maintenance requests, compliance binder.

### 10.1 Migration and cutover principles

- Migrate in this order: branches and settings, academic structure, staff, teachers, students and guardians, courses and sections, schedules, enrollments and opening balances, open installments, teacher contracts.
- Never migrate a total balance without its underlying invoices or opening-balance document.
- Reconcile migrated receivables with the owner before cutover.
- Run in parallel for two weeks, then set a paper sunset date.
- Keep legacy scans attached to the student file for reference.
- Train by role, not by module.
- Identify a champion in each department.
- Measure adoption weekly.

### 10.2 Hardware checklist

- Server with UPS and nightly backup.
- Front-desk PCs with keyboard shortcuts and receipt printing.
- A5 laser printer for statements and report cards.
- 80 mm thermal printer for receipts.
- Barcode or QR scanner for student cards.
- Two tablets for attendance and kiosk mode.
- Card printing or lamination capability for student IDs.
- Redundant internet or a mobile hotspot for WhatsApp fallback.

---

## 11. Success Metrics and Zero-Paper Acceptance Tests

### 11.1 Metrics

| Metric | Target |
|---|---|
| Active enrollments with a digital agreement and installment plan | 100% |
| Receipts issued digitally | 100% |
| Sessions with attendance captured within 24 hours | ≥ 95% |
| Guardian WhatsApp reachability | ≥ 90% |
| Cash variance per day | < 0.5% of collections |
| Duplicate student rate | < 0.5% |
| Report card turnaround after marks are final | < 48 hours |
| Teacher settlement disputes per cycle | < 1 |
| Month-end close | < 2 working days |
| Days sales outstanding | Reduced by 30% |
| Staff adoption of daily workflows | ≥ 90% by phase end |
| Backup restore drill | Passed quarterly |

### 11.2 The paper blackout test

The strongest acceptance test is operational, not technical:

> For one full week, remove every paper form, receipt book, attendance sheet, and timetable board from the front desk and classrooms. If any workflow stops, the ERP is not finished.

Run this test at the end of each phase on the workflows that phase claims to cover. Record every workaround. Each workaround is a missing feature, not a training problem.

### 11.3 Definition of done for a module

A module is done only when:

- it has a digital document or record with a number;
- it has a permission model and an approval path;
- it has a print projection;
- it has an audit trail;
- it has a report;
- it has a migration or opening-balance path;
- it has an offline or failure fallback where relevant;
- it has been tested against the paper blackout.

---

## 12. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Unstable internet or power | Operations stop | On-prem server, UPS, offline attendance, printable fallbacks, queued sync |
| Staff resistance | Adoption fails | Role-based training, champions, simple screens, paper sunset policy |
| WhatsApp policy or number ban | Parent communication breaks | Official Business API, SMS fallback, in-app archive, multiple numbers |
| Poor legacy data | Wrong balances and duplicates | Migration template, duplicate detection, owner reconciliation, parallel run |
| Scope creep | Delays and fatigue | Phase gates, explicit non-goals, owner sign-off per phase |
| SQL Server Express limits | Performance and growth ceiling | Edition upgrade path, archival, database aggregation |
| Teacher distrust of calculations | Settlement disputes | Transparent live statements, frozen contract rules, teacher acknowledgement, dispute workflow |
| Cash fraud | Financial loss | Segregation, blind count, variance approval, immutable receipts, audit |
| Privacy incident | Reputation and legal risk | RBAC, encryption, file access control, consent, retention, local AI |
| AI hallucination | Wrong decisions | Read-only, source citations, human review, no autonomous money actions |
| Over-customization for one institute | Product rigidity | Configuration over code, policies as data, branch-level settings |
| Single-vendor dependence | Lock-in | Abstract messaging and payment providers behind an internal interface |

### 12.1 Explicit non-goals for the first release

- A full learning management system with video lessons and homework.
- Online proctoring or remote exam surveillance.
- Biometric attendance hardware.
- Native mobile apps for iOS and Android.
- Multi-tenant SaaS billing and onboarding.
- Government system integration.
- Fully automated AI actions on money or student records.

These can be considered later, but including them now would dilute the zero-paper core.

---

## 13. Decisions Required Before Development

These are the decisions that change the design, so they should be made before implementation.

1. **Single institute or product for the market?** Recommendation: build single-tenant, multi-branch first, with tenant-ready boundaries.
2. **How many branches in the first release?** One branch is simpler; two or more require scoping and consolidated reporting from day one.
3. **Which teacher compensation models must be supported first?** Recommendation: percentage of collected, fixed monthly, per session, and room rent.
4. **What is the default installment pattern and late-fee policy?**
5. **What is the refund and withdrawal policy?**
6. **Who approves discounts, refunds, voids, and cash variances?**
7. **Which WhatsApp provider and business number will be used?**
8. **Receipt format:** A5 laser, 80 mm thermal, or both?
9. **Student ID format:** PVC card, laminated card, or paper with QR?
10. **Is Kurdish required for guardians in the first year?**
11. **Are online payments required now, or later?**
12. **Hosting:** on-premises, regional cloud, or hybrid?
13. **What legacy data will be migrated, and who will verify balances?**
14. **Should teacher earnings accrue on collection or on billing?** This affects cash flow, teacher trust, and accounting.
15. **What is the academic calendar:** two terms, four quarters, or continuous course cycles?

---

## 14. Glossary

| Arabic | English | Meaning in this blueprint |
|---|---|---|
| معهد | Institute | The private educational or training center |
| دورة | Course / program | A sellable educational offering |
| شعبة | Section | A group within a course with its own schedule |
| قاعة | Hall / classroom | A physical teaching resource |
| مرحلة | Stage | Primary, intermediate, preparatory |
| صف | Grade | A specific year within a stage |
| مادة | Subject | Mathematics, English, physics, etc. |
| مدرس | Teacher | A contracted instructor |
| ولي أمر | Guardian | Parent or responsible adult |
| طالب | Student | The enrolled learner |
| قسط | Installment | A scheduled tuition payment |
| سند قبض | Receipt | Proof of payment |
| وصل | Voucher / slip | A small document, often a receipt or approval |
| خصم | Discount | A reduction in fee |
| إعفاء | Waiver | A full or partial forgiveness of a fee |
| استرداد | Refund | Return of collected money |
| سلفة | Advance | Money paid before it is earned and recovered later |
| كشف حساب | Statement | A detailed account of charges and payments |
| ملازم | Lecture notes / booklets | Printed study material sold to students |
| مراجعة مركزة | Intensive revision | Exam-focused review course |
| امتحان وزاري | Ministerial exam | Official government exam |
| امتحان تجريبي | Mock exam | Practice exam modeled on the official one |
| دفتر الوصولات | Receipt book | The paper receipt ledger |
| سجل الطلاب | Student register | The master paper ledger |
| بطاقة الطالب | Student card | Paper or plastic student identity |
| دوام | Working hours / shift | Institute operating schedule |
| حصة | Session / class period | One scheduled teaching occurrence |
| غياب بعذر | Excused absence | Absence with an accepted reason |
| تأخير | Lateness | Arrival after the session start |
| إجازة | Leave / holiday | Staff leave or institute holiday |
| راتب | Salary | Fixed staff pay |
| نسبة | Percentage | Teacher share of tuition |
| إيجار قاعة | Hall rent | Rent paid for a teaching space |

---

## Appendix A — Current Codebase Map

| Area | Files and responsibilities |
|---|---|
| Startup | `Program.cs`: EF Core SQL Server, Identity, localization, AI, rate limiting, seeding, schema upgrades |
| Data | `Data/AppDbContext.cs` (45+ entity sets, relationships, indexes, Arabic collation), `Data/DbSeeder.cs` (demo institute data), `Data/StudentRegistrationSchema.cs` (idempotent upgrades, permanent number trigger) |
| Core models | `Models/Core.cs`: users, branches, settings, audit, notifications |
| People models | `Models/People.cs`: parents, students, teachers, certificates, availability, ratings, earnings, rent contracts, employees, attendance, payroll |
| Academic models | `Models/Academics.cs`: stages, grades, subjects, courses, sections, schedules, exams, results |
| Finance models | `Models/Finance.cs`: accounts, journals, funds, dues, payments, expenses, income, assets |
| Operations models | `Models/Operations.cs`: classrooms, bookings, cafeteria, enrollments, attendance, private lessons |
| Services | Accounting, Billing, Dashboard, Identity, Localizer, Matching, Audit/Notification/FileStorage, StudentExport, StudentNumber |
| Controllers | 24 controllers covering admissions, academics, attendance, exams, finance, HR, rentals, portals, reports, admin, AI |
| Views | Razor pages for staff, teacher, parent, student, public site, receipts, payslips |
| AI | `Ai/`: read-only context, command guard, reader provisioning, tool registry, Ollama client, route catalog, system prompt |
| Dependencies | ClosedXML, Identity EF Core, EF Core SQL Server, QRCoder |

## Appendix B — Review Method and Limits

- The review was performed statically against the source code, models, controllers, services, views, configuration, and project files.
- The application was not executed, and the live database was not inspected, because this phase is explicitly concept-only.
- Findings about numbering races, missing modules, and unused QR are based on the code paths present in the repository.
- No files were modified except this blueprint document.
- All recommendations are conceptual; no schema, migration, endpoint, or UI implementation has begun.
