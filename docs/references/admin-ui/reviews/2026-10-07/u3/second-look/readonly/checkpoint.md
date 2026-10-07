# U3-Q13(a) shared readonly values — remediation F

App and frozen reference components.css changed together; cmp passes byte-identical. The .ro-value box uses control height,1px readonly date-picker border, input radius, surface-2 background and full text color. Padding keeps a single line exactly the input height; multiline content grows. Hover leaves border/background unchanged, and is-empty remains muted. No other shared rule changed.

Both engines:15 real finalized page cases (Identity,Schedule,Signup setup ×390/494/860/1280/1440) pass family markup/style,no-fade,document/short viewport checks and actual readonly values. Within each case, controlled one-line/multiline/empty probes verify exact box geometry/colors and no hover change. Shared drawer readonly value also passes. Identity declares its real read-only selector because its textarea is absent in this state; no editable assertion removed. CSS-scope7 stylesheets/293 selectors passes both engines. Debug build0warnings/errors; cmp and diffcheck pass.

Reference difference is registered under U3-Q13(a), ui/components.css:695–697 versus readonly date-picker:873. This explicit authorization is the only frozen shared-CSS change. User Signup visual acceptance remains pending.
