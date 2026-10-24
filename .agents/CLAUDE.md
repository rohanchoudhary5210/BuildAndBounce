Claude
Planning a parallel build-and-review workflow for two AI agents.
Planning a parallel build-and-review workflow for two AI agents.
Haan, ye setup achha kaam karta hai. Isse “builder + reviewer” pattern kehte hain. Bas ek rule pakka rakhna hai: code sirf ek agent likhega, doosra sirf padhega aur review karega. Agar dono ek hi files edit karenge, toh ek doosre ke changes overwrite honge aur Unity bar-bar recompile karega.

Workflow
Antigravity = builder. Wo AGENTS.md ke hisaab se milestone ka ek task banata hai, aur har chhota task ek alag commit hota hai.

Claude Code = reviewer. Har commit ke baad Claude se kaho: “last commit ka diff review karo.” Wo check karega:

AGENTS.md ke rules follow hue ya nahi (folder structure, Find calls, magic numbers, Input System)
bugs, edge cases, aur per-frame allocations
scope ke bahar koi change toh nahi hua
Claude problems ki list deta hai, aur woh list aap Antigravity ko de dete ho. Claude khud fix nahi karega, taaki code ka ek hi owner rahe.

Aap = tester. Unity mein khel kar dekhte ho. Koi AI aapka game khel kar nahi bata sakta ki jump achha feel hota hai ya nahi. Ye kaam sirf aap kar sakte ho.

Milestone ke end mein final check: Claude poore milestone ka review kare. “Done when” wale criteria check kare, README update kare, aur AGENTS.md mein tick lagaye.

Antigravity: task banao → commit
      ↓
Claude: diff review → issues list
      ↓
Antigravity: fix → commit
      ↓
Aap: Unity mein test → OK? → agla task
Jaldi ship karne ke liye
Git branch per milestone (m1-platformer, m3-builder). Milestone pass ho jaaye, tab main mein merge karo.
Parallel kaam sirf alag-alag areas mein karo. Jaise Antigravity M1 ke scripts banaye, aur Claude usi waqt Builder/ ka serialization banaye, jo scene ko chhoota hi nahi. Lekin ek scene ya prefab par do agent kabhi ek saath kaam na karein. Unity ki scene files merge karna bahut mushkil hota hai.
Unity Editor ek hi khula rakho. Do agents ek project par do jagah se compile karwayenge toh errors aayenge.
Asli speed limit aapka testing time hai, code likhne ka nahi. AI minutes mein code likh deta hai, lekin feel tune karna, bugs pakadna aur levels banana aapke haath mein hai. Realistic timeline: M1 aur M2 mein 1–2 hafte, aur pura game (M1–M5) lagbhag 5–6 hafte, agar roz 2–3 ghante do.
Claude ke liye review prompt (copy karke rakh lo)
Read CLAUDE.md. Review the latest commit diff only (git diff HEAD~1).
Do NOT edit any files. Report:
1) Rule violations from CLAUDE.md
2) Bugs / edge cases
3) Performance issues (allocations, Find calls in Update)
4) Anything outside the current milestone's scope
Give a short numbered fix list I can paste to the builder agent.
Ek baat aur: review mein jo issues aayein, unhe khud bhi padho. Interview mein “maine AI ke code mein ye bug pakda aur aise fix karwaya” bhi achhi story hai, lekin tabhi jab aapko sach mein samajh aaye ki bug kya tha.

antigravityy k liye ky instruction h?

Drafting platform-specific prompts for an automated builder workflow.
Drafting platform-specific prompts for an automated builder workflow.
Antigravity builder hai, toh uske liye teen cheezein chahiye: setup, kaam ka prompt, aur review ke fixes ka prompt.

1. Setup (ek baar)
Unity project folder ko Antigravity mein workspace ki tarah kholo.
AGENTS.md root mein hai, Antigravity use rules ki tarah padh lega. Agar pehle prompt mein woh rules follow karta na dikhe, toh wahi content Antigravity ki workspace rules settings mein bhi daal do.
Settings mein terminal commands aur file changes “ask before running” par rakho, auto-run par nahi. Isse aap har change dekh paoge.
Antigravity ka browser agent Unity game test nahi kar sakta. Testing aap Unity Editor mein khud karoge.
2. Har task ka prompt (builder)
Read AGENTS.md fully. You are the BUILDER for this project.
Current milestone: M1. Task: <ek chhota task, jaise "player controller">.

Rules:
- Plan first: list the files you will create/change and why. Wait for my OK.
- Work only inside Assets/_Project/. Do not edit .unity or .prefab files;
  give me editor setup steps instead.
- Keep it small: this task only, nothing from later milestones.
- When done, report: files changed, design reasoning (2-4 sentences),
  editor test steps, known limitations. Then stop.
Pehle din ke tasks is order mein do (har ek alag prompt mein):

Assembly definitions aur folder structure
Player controller
Cinemachine camera setup
Static block, checkpoint, kill zone aur respawn
Moving platform (player use ride kare)
Start, finish aur timer
3. Claude ka review aane ke baad (fix prompt)
A reviewer found these issues in your last commit:
<Claude ki numbered list paste karo>

Fix ONLY these issues. Do not change anything else.
Report what you changed for each number.
Kaam ka cycle
Antigravity plan dikhata hai, aap OK karte ho, aur woh code likhta hai.
Aap Unity mein test karte ho aur commit karte ho.
Claude Code diff review karta hai aur issues ki list deta hai.
Antigravity sirf woh issues fix karta hai.
Aap test karte ho, commit karte ho, aur agla task shuru karte ho.
Plan approve karne wala step skip mat karna. Agar plan galat direction mein ja raha ho, toh code likhne se pehle hi pakad loge.

Claude.md?

Claude
Document
·MD
Claude is thinking



Claude is AI and can make mistakes.


Claude · MD
CLAUDE.md — Reviewer instructions for Claude Code
@AGENTS.md

The file imported above is the shared project context. Follow all of it. This file adds Claude's role in this project.

Your role: REVIEWER (not builder)
Antigravity is the builder. It writes the code. You review it. Only one agent writes code at a time, so commits stay clean and Unity does not recompile from two places.

Default rule: do not edit files. Read, review and report only. Edit files only when the developer explicitly says so in the current message (for example "Claude, you fix this one" or "update the README").

Review after every commit
When asked to review, look at the latest commit diff (git diff HEAD~1, or the range the developer gives). Report in this order, as a short numbered list:

Rule violations: anything that breaks AGENTS.md (folder structure, namespaces, public fields, magic numbers, Find/SendMessage, legacy Input.GetKey, new packages, edits to .unity/.prefab/ProjectSettings).
Bugs and edge cases: logic errors, null references, frame-rate dependence, missing Time.deltaTime, physics in Update, state that never resets (for example on respawn).
Performance: allocations in Update/FixedUpdate (LINQ, new, string concatenation, GetComponent per frame), unnecessary per-frame work.
Scope creep: changes outside the current milestone or task.
Interview check: anything too clever or unclear for the developer to explain. Suggest a simpler version.
For each item give: file and line, what is wrong, and a one-line fix instruction. End with a copy-paste block the developer can send straight to Antigravity:

Fix ONLY these issues. Do not change anything else.
1. ...
2. ...
If the diff is clean, say so in one line. Do not invent problems. Mark each issue [must fix] or [nice to have] so the developer can prioritise.

Final check at the end of each milestone
When the developer says a milestone is finished:

Review the whole milestone branch against main, not just the last commit.
Go through that milestone's "Done when" criteria in AGENTS.md one by one. Mark each as met, not met, or needs manual testing by the developer.
Go through the Definition of done checklist in AGENTS.md.
List the manual tests the developer must still do in the Unity editor (feel, gamepad, edge cases). You cannot play the game, so say clearly what you could not verify.
With the developer's OK only: update the README section for this milestone, and tick the milestone in AGENTS.md "Current status".
Teaching mode
The developer must be able to explain every system in an interview. In each review, add one short "Explain it like an interview" note: 2–3 sentences on the key design idea in the reviewed code, in plain language.


Claude is responding
