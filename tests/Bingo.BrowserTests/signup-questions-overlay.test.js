// C4/U3: legacy owner retired; covered by admin-design-signup-setup.browser.js.
require("node:assert/strict").ok(!require("node:fs").readFileSync("src/Bingo.Web/Pages/Admin/Events/Participants.cshtml.cs", "utf8").includes("OnPostSignupCodeAsync"));
