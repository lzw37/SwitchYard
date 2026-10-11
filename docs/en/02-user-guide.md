# Accounts, interface and courses

[Documentation index](README.md) · [简体中文](../zh-CN/02-user-guide.md)

This guide describes common operations, account permissions and course resources in the repository as of 2026-10-11.
For hump design, checking and simulation, see the [hump guide](03-hump.md); use the index for other modules.
Opening a page and obtaining API authorization are separate checks; a visible page does not imply permission to read or change business data.

## 1. Pages and first use

| Page | Path | Purpose |
| --- | --- | --- |
| Home | `/` | Open modules and select a language |
| Hump | `/hump` | Plan, profile, calculations, checks and rolling simulation |
| Capacity | `/capacity` | Station layouts, routes, operation plans, solving and simulation |
| Capacity introduction | `/capacity-preview` | Module introduction, separate from the working solver page |
| Courses | `/courses` | Teaching videos, PDFs and fill-in-the-blank exercises |
| Sketch layout analysis | `/sketchlayout` | Facilities, workflows, relationships and conflicts; authentication required |
| Login / registration | `/login` / `/createuser` | Start a session or request an account |
| Profile | `/userinfo` | View account details and change email/password |
| User management | `/usermanagement` | Administrator account management |
| Hump instance management | `/hump/instancemanagement` | Administrator management of instances and ownership |
| No permission | `/no-permission` | Message shown when a protected management route is denied |

The legacy `/usermanager` address redirects to `/usermanagement`; `/about` is also registered.
Home and courses are publicly accessible. All four current course-resource endpoints allow anonymous access; courses do not use a student-grade database.
Hump, capacity and other protected business APIs require valid authentication and check permissions such as instance ownership.

Recommended first-use sequence:

1. Have the maintainer prepare the server, database and template data.
2. Register or log in with an administrator-provided account.
3. If a password change is required, complete it and log in again.
4. Open a module from Home and confirm the instance, scheme and language.
5. Explore an existing instance or copy it for a design comparison.
6. Save each edited area successfully before changing instance, scheme or page.

## 2. Login, registration and sessions

Enter a username and password, then click Login or press `Enter` in the password field.
Follow the specific message for incorrect credentials, a disabled account or a network failure.
Requests carry a login token. The frontend handles token refresh; failed refresh requires another login.
A previously opened page or a cached username does not prove that the session is still valid.

The self-registration form has these requirements:

| Input | Current web form requirement |
| --- | --- |
| Username | 3–50 characters; must be unique |
| Password | 6–30 characters |
| Password confirmation | Must match the password |
| Email | Must have a valid email format when supplied |
| Role | `User` or `Admin` |

A self-registered `User` account is enabled by default.
Selecting `Admin` does not create an immediately usable administrator account: it starts disabled and an existing administrator must enable it.
Account creation also initializes data from hump template instance `001`; a missing template or failed copy causes registration to fail.
An empty database does not provide a usable template or an initial administrator. The maintainer must prepare them.

The web client applies SHA-256 before sending a password, as required by the existing API contract.
Users should enter their original password, not a hash they have calculated themselves.
Client-side hashing does not replace HTTPS deployment; consult the API documentation when building another client.

## 3. Profile and required password changes

Choose Profile from the user menu at the top right to open `/userinfo`.
Username and role identify the current account; this page cannot be used to promote your own role.
After changing the email address, save and check the success message.

To change a password:

1. Enter the current password.
2. Enter a new password of 6–30 characters, different from the current password.
3. Confirm the new password and submit.
4. The current session ends after success; log in using the new password.

Accounts created or imported by an administrator, and accounts whose password is reset, require a password change.
After login, the router sends them to Profile; attempts to open another page return to Profile until the change is complete.
If this happens repeatedly, check the required-password-change notice before trying other modules.
Self-registration and administrator-provided accounts therefore have different first-login flows.

On a shared computer, use Logout in the user menu or Profile before leaving.
Closing a business tab does not log out of the application.

## 4. Administrator account maintenance

User management and `/api/Admin/...` endpoints require the `Admin` role.
Hiding management menus is a UI behavior; the backend checks roles independently.
Entering a management address directly does not give ordinary users administrator access.

Administrators can search and page through accounts, and perform these actions:

- Create accounts with a role, email address and enabled status.
- Edit account details and enable or disable accounts.
- Reset passwords; the user must change the temporary password at the next login.
- Delete accounts; the UI and service prevent deleting the currently signed-in account.
- Download an Excel template, import accounts and inspect row-by-row results.

Bulk import procedure:

1. Download `user_info_template.xlsx` from User management.
2. Use its first worksheet and retain the column headings.
3. Replace example rows with real accounts; example accounts are not automatically ignored.
4. Fill in `username`, `password`, `role`, `email` and `isActive`.
5. Upload the file and inspect success/failure counts and individual errors.
6. Correct and retry failed rows only, avoiding accounts already created by the first import.

| Column | Meaning |
| --- | --- |
| `username` | Required; the server checks for an existing account |
| `password` | Original temporary password, at least 6 characters; use 6–30 to match the web form |
| `role` | `User` or `Admin`; blank defaults to `User` |
| `email` | Optional email address |
| `isActive` | `1` enabled, `0` disabled; blank defaults to enabled |

Rows with both username and password empty are skipped; a row missing only one is reported as failed.
The import reports each row separately. Partial success is not complete success.
Every new account also requires the default hump template to be available for copying.

## 5. Buttons, panels and language

Shared workspaces such as capacity and courses use small circular action buttons.
Hover for the action name or use `Tab` to move focus. Disabled buttons indicate unmet selection, permission or loading conditions.
The active tab, selected row and nearby fields identify an action's scope.
Hump retains its own text buttons and side panels; the reusable station-layout editor has a separate toolbar.

Panels with a thin divider can be resized by dragging or using the keyboard:

| Action | Result |
| --- | --- |
| Left/right or up/down arrows | Change the corresponding dimension by about 10 px |
| `Shift` + arrow | Change it by about 40 px |
| `Home` / `End` | Set the minimum / maximum allowed size |
| `Enter` or double-click | Restore the initial or page-defined size |

Right-hand and bottom panels change in the reverse direction to left-hand and top panels.
Container size and minimum dimensions constrain resizing; sizes are not persisted as project data.
Some workspaces stack vertically in narrow windows, where their dividers adjust height instead.
Hump profile panels expand and collapse using side arrows; the tables below the hump plan do not use the shared divider.

Language selection is available on Home; hump uses Chinese/English text controls, while capacity uses `EN` / `中`.
The choice is stored in the current browser's `locale` entry; the browser language supplies the initial preference.
Translation covers UI text. Instance names, equipment names, course material, user input and raw server errors are not automatically translated.

## 6. Saving, loading and switching

Edits in a table, changes on a drawing and data saved on the server must be checked separately.
Unless a module explicitly describes automatic saving, use that area's Save action after editing.
Wait for success before refreshing, loading, switching instance or leaving the page.
Refresh and Load read server data; they are not Save actions.
A successful calculation means that calculation returned results, not that all other editing areas were saved.
When multiple windows edit one station scheme, a revision conflict requires reloading and reconciling the changes.

## 7. Taking a course

The course tree is organized as “part → chapter → section,” with an adjustable directory panel.
In narrow windows, the directory is above the content and its height can be adjusted.

1. Expand a part or chapter to view its key and difficult topics on the right.
2. Select a section to open its videos, PDFs and fill-in-the-blank exercises.
3. Use the video tabs to select a clip; playback, volume and seeking use the player's controls.
4. Select a PDF, then browse using page buttons or nearby-page thumbnails.
5. Enter exercise answers and grade them to see the correct count, classification, similarity and reference answers.

PDFs are rendered page by page in the browser; large files may take longer and use more memory.
Video playback depends on the actual codec and browser support. An extension in the manifest does not guarantee decoding.
The current course page has no upload, course-editing, teacher-grade-management or server-grade-submission function.
Exercise state is handled within the page and is not persistently restored after a refresh.

Grading uses character similarity: it ignores case, whitespace and some punctuation, then compares overlapping character bigrams.
Similarity `≥0.9` is correct; `≥0.5` and below `0.9` is partial; lower scores are wrong. Only correct items contribute to the correct count.
This is a self-study aid, not semantic grading; synonyms and short answers may need manual comparison with the reference answer.

## 8. Organizing teaching resources

Resources come from server directories configured by `Course:VideoDir` and `Course:DocDir`.
If those directories contain `站场视频` or `教学文档` subdirectories, the server prefers the respective child directory.
If `DocDir` is not configured, `VideoDir` is used as the candidate teaching-document root.
The server lists files recursively and ignores temporary files beginning with `~$`.

Recommended structure; retain the recognized naming markers:

```text
教学文档/第一篇/学习指导.doc
教学文档/第一篇/第一章/学习指导.doc
教学文档/第一篇/第一章/第一节/课件.pdf
教学文档/第一篇/第一章/第一节/文字教材.doc
教学文档/第一篇/第一章/第一节/习题测试.doc
站场视频/第一篇/第一章/1.1.1.mp4
站场视频/第一篇/第一章/1.1.2.mp4
```

Video names start with “chapter.section[.clip]” numbers; Arabic numerals and some Chinese number forms are recognized.
The part name `绪论` has special handling. Other videos need parseable chapter/section numbers and part folders corresponding to the documents.
Normal section documents use the four-level relative path “part/chapter/section/file”; deeper folders do not create additional teaching levels.
PDFs directly within a part or chapter also have compatibility handling, creating a section from the filename.
`学习指导`, `文字教材` and `习题测试` are current filename markers and must remain even with the English UI selected.

The server lists `.pdf`, `.doc` and `.docx`, plus `.mp4`, `.webm`, `.m4v`, `.mov`, `.avi` and `.mkv` videos.
Word text extraction currently uses byte-decoding heuristics, not a full Word renderer; arbitrary `.docx` files in particular are not guaranteed to parse.
Study guides use Chinese headings such as `重点知识` and `难点知识` to identify content.
Exercises separate questions and answers with `答案:` or `参考答案:`; answer continuation lines begin with `(number)` or `（number）`.
Validate representative sections for display and text extraction before publishing a complete new resource collection.

## 9. Troubleshooting and further reading

| Symptom | What to check |
| --- | --- |
| Login repeatedly returns to Profile | Complete the required password change and log in again |
| Registration fails or the default instance is absent | Ask the maintainer to inspect template `001` and initialization logs |
| An instance is hidden or access is denied | Check account, role, enabled status and ownership |
| The course tree is empty | Check configuration, directories and resource hierarchy; an empty manifest need not be a login problem |
| Files exist but exercises or key topics are absent | Check Chinese naming markers, answer format and Word extraction limitations |
| A listed video will not play | Check codec, request response and browser support |
| Chinese text remains in the English UI | Distinguish source material/user data from untranslated interface text |
| Edits disappear when reopening | Check save success and that the same instance/scheme is selected |

Retained documentation: [original user guide](../archive/guides/User-Guide.md) and [original hump guide](../archive/guides/Hump.md).
Developers can continue with [development and station-layout integration](09-development-integration.md).
Source references: [routes](../../switchyard-vue/src/router/index.ts), [authentication controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs), [administrator controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs), [course-resource controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs), [course page](../../switchyard-vue/src/course/CourseMain.vue) and [grading component](../../switchyard-vue/src/course/CourseFillBlankQuiz.vue).
