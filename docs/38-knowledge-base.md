# 38. Knowledge base: policies and procedures

GREL's policies, handbook, conditions of service and procedures, kept in GRSMS so that:
* staff can read them and ask questions;
* the onboarding assistant can answer new employees from the documents meant for them.

Added 14 November 2026 (docs/36 step AI-7).

---

## 1. Adding documents (HR)

**Administration → Knowledge base.** You need the permission *Maintain the knowledge base*. HR Administrators and HR Managers have it by default, and the Super Admin can give it to other roles.

1. Click **Add a document**.
2. Fill in:
   * the **title** (e.g. *Leave Policy*);
   * the **owner** who approves it;
   * the **version** and the **effective date**;
   * **who may read it**:
     * **Staff:** GREL staff only;
     * **Staff and new employees:** new employees who are still onboarding can open it too, and the onboarding assistant uses it to answer their questions. Use this for the handbook, the code of conduct and first-day rules, not for internal HR procedures.
3. Choose the **file**, a PDF or Word (.docx), then click **Add the document**.

GRSMS reads the text and splits it into **sections** at the headings, each about a page. Open **What GRSMS read from the file** to check them.

* **Use a PDF with a text layer, or the Word file.** A scanned page is an image: GRSMS keeps the file and it can still be opened, but it can't be searched or used in answers. The list shows it as *No text*.
* **Headings help.** Numbered headings ("3. Annual leave"), headings in capitals, and short title-case lines start a new section. Answers cite the section by its heading.

**A new version.** On the document, choose the new file under **Upload a new version**, type the new version number and click **Upload**. The text is read again. The earlier file stays listed as history, and HR can still open it.

**Archive or delete.**
* **Archive** stops a document being searched or used in answers. It stays listed for HR and can be put back in use.
* **Delete** is only for a document added by mistake.

## 2. Reading and asking (all staff)

**Menu → Policies & procedures.**
* **The library:** every document in use. **Read** shows its sections, and you can also **Preview** or **Download** the file.
* **Ask a question**, e.g. *How many days of annual leave do I get?*:
  * **AI answers off:** GRSMS shows the sections that match your words, best first. Click one to read it in place.
  * **AI answers on** (*AI settings & usage → Knowledge base answers*): you get a short answer written **only from those sections**, with numbered sources ([1], [2]) naming the document, version and section. If the documents don't cover the question, it says so and suggests asking HR. The matching sections stay listed underneath.

The AI never uses general knowledge, Ghana's labour law or other companies' rules: only GREL's documents in GRSMS. Check the source before acting on an answer.

## 3. New employees

New employees don't see the library. When both *Onboarding assistant* and *Knowledge base answers* are switched on:
* their questions about rules (working hours, dress code, leave, …) are answered from the documents marked **Staff and new employees**;
* each answer comes with an **Open** button for the document.

Staff-only documents are never used for them.

## 4. What is sent to the AI service

* The question.
* The best matching sections of GREL's documents: up to 6 for staff questions, 4 for the onboarding assistant.

No personal data is sent. When no section matches, nothing is sent at all. Every call is logged and counted against the AI budget, like the other AI features.

## 5. Good to know

* Search works on words, so "annual leave" finds "Annual leave" and "leave days". It doesn't understand synonyms ("vacation"), so try the words GREL's documents use.
* The search runs inside GRSMS, so it ranks the same way on every server.
* Archived and deleted documents disappear from search straight away.
